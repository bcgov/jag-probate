import type { Pinia } from 'pinia';
import { pinia as appPinia } from './pinia';
import { useWizardStateStore } from './WizardStateStore';

/**
 * The window.wizard* bridge called from CHEFS schema scripts.
 *
 * Installed once at bootstrap and never removed, so it exists whether or not
 * the wizard sidebar is mounted:
 * - State setters write straight to WizardStateStore.
 * - Navigation needs the sidebar component; calls made while no sidebar has
 *   attached its handlers are queued and replayed when one attaches.
 *
 * wizardValidateStep / wizardSaveStep / wizardFlushAll / wizardGoToField /
 * wizardGetPersistedPayload stay owned by StepFormViewer (they need the
 * Form.io instances) and are not touched here.
 */

export interface WizardNavigationHandlers {
  updateSidebar: (stepKey: string) => void;
  navigateNext: () => void;
  navigatePrevious: () => void;
}

type QueuedNavCall =
  | { kind: 'update'; stepKey: string; source?: string }
  | { kind: 'next'; source?: string }
  | { kind: 'previous'; source?: string };

const MAX_QUEUED_NAV_CALLS = 50;

let installedPinia: Pinia | null = null;
let navHandlers: WizardNavigationHandlers | null = null;
const navQueue: QueuedNavCall[] = [];

function store() {
  return useWizardStateStore(installedPinia ?? appPinia);
}

function runNavCall(call: QueuedNavCall) {
  if (!navHandlers || !store().isActiveBridgeSource(call.source)) return;
  if (call.kind === 'update') navHandlers.updateSidebar(call.stepKey);
  else if (call.kind === 'next') navHandlers.navigateNext();
  else navHandlers.navigatePrevious();
}

function dispatchNavCall(call: QueuedNavCall) {
  if (navHandlers) {
    runNavCall(call);
    return;
  }
  if (navQueue.length >= MAX_QUEUED_NAV_CALLS) {
    navQueue.shift();
    console.warn(
      '[wizardBridge] Dropped oldest queued navigation call; no sidebar attached.'
    );
  }
  navQueue.push(call);
}

/**
 * Called by the primary sidebar. Replays queued navigation calls in order,
 * then routes live calls to the handlers. Returns a detach function.
 */
export function attachNavigationHandlers(
  handlers: WizardNavigationHandlers
): () => void {
  navHandlers = handlers;
  const queued = navQueue.splice(0);
  queued.forEach(runNavCall);
  return () => {
    if (navHandlers === handlers) navHandlers = null;
  };
}

/** Idempotent: safe to call from bootstrap, components and tests. */
export function installWizardBridge(pinia: Pinia = appPinia): void {
  if (installedPinia) return;
  installedPinia = pinia;

  window.wizardUpdateSidebar = (stepKey, sourceStepKey) =>
    dispatchNavCall({ kind: 'update', stepKey, source: sourceStepKey });
  window.wizardNavigateNext = (sourceStepKey) =>
    dispatchNavCall({ kind: 'next', source: sourceStepKey });
  window.wizardNavigatePrevious = (sourceStepKey) =>
    dispatchNavCall({ kind: 'previous', source: sourceStepKey });

  window.wizardSetStepStatus = (substepKey, status, sourceStepKey) => {
    const s = store();
    if (s.isActiveBridgeSource(sourceStepKey))
      s.setStepStatus(substepKey, status);
  };
  window.wizardSetAllStatuses = (map, sourceStepKey) => {
    const s = store();
    if (s.isActiveBridgeSource(sourceStepKey)) s.setAllStatuses(map);
  };
  window.wizardSetStepVisibility = (stepKey, isVisible, sourceStepKey) => {
    const s = store();
    if (s.isActiveBridgeSource(sourceStepKey))
      s.setStepVisibility(stepKey, isVisible);
  };
  // No active-source check: the call states the visibility a form's data
  // implies (e.g. formActivationTest), which is valid whenever it's computed.
  window.wizardSetSubstepVisibility = (substepKey, isVisible) => {
    store().setSubstepVisibilityOverride(substepKey, isVisible);
  };
  window.wizardSetAllVisibility = (nextState, sourceStepKey) => {
    const s = store();
    if (s.isActiveBridgeSource(sourceStepKey)) s.setAllVisibility(nextState);
  };
  window.wizardSetStepClickable = (stepKey, isClickable, sourceStepKey) => {
    const s = store();
    if (s.isActiveBridgeSource(sourceStepKey))
      s.setStepClickable(stepKey, isClickable);
  };
  window.wizardBanPathway = (stepKey, isBanned, sourceStepKey) => {
    const s = store();
    if (s.isActiveBridgeSource(sourceStepKey))
      s.banPathwayAfter(stepKey, isBanned);
  };
}

/** Test helper: removes the bridge and clears queued calls/handlers. */
export function uninstallWizardBridgeForTests(): void {
  installedPinia = null;
  navHandlers = null;
  navQueue.splice(0);
  const fns = [
    'wizardUpdateSidebar',
    'wizardNavigateNext',
    'wizardNavigatePrevious',
    'wizardSetStepStatus',
    'wizardSetAllStatuses',
    'wizardSetStepVisibility',
    'wizardSetSubstepVisibility',
    'wizardSetAllVisibility',
    'wizardSetStepClickable',
    'wizardBanPathway',
  ] as const;
  fns.forEach((fn) => delete window[fn]);
}
