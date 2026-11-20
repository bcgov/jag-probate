import {
  getKeysBannedAfter,
  getOrderedSubstepKeys,
  getVisibleSubstepKeys,
} from '@/config/wizardSteps';
import type {
  StepStatus,
  WizardNavState,
  WizardStep,
} from '@/types/applicationStep';
import { defineStore } from 'pinia';
import { computed, reactive, ref } from 'vue';

/**
 * Wizard sidebar state (visibility, status, clickability, active substep),
 * independent of any component. CHEFS schema scripts mutate it through the
 * window.wizard* bridge (see wizardBridge.ts), which is installed at app
 * bootstrap, so calls made before the sidebar mounts are never lost.
 *
 * Substep visibility has two layers:
 * - navState.hiddenSubsteps: progressive/default/resumed state, persisted.
 * - substepVisibilityOverrides: the latest explicit value a CHEFS form asked
 *   for this session. It takes precedence over navState and is never reset by
 *   init()/resume/setAllVisibility, so a form's rule result can't be wiped by
 *   a later state seed.
 *
 * When the pathway machine owns availability (wizard/pathway), it replaces
 * hiddenSteps/hiddenSubsteps/disabledMap wholesale via applyPathway() and the
 * override layer stays empty.
 */
export const useWizardStateStore = defineStore('wizardState', () => {
  const activeSubstep = ref('');
  const navState = reactive<WizardNavState>({
    hiddenSteps: {},
    hiddenSubsteps: {},
  });
  /** true = force visible, false = force hidden. */
  const substepVisibilityOverrides = reactive<Record<string, boolean>>({});
  const statusMap = reactive<Record<string, StepStatus>>({});
  const disabledMap = reactive<Record<string, boolean>>({});
  const steps = ref<WizardStep[]>([]);
  let pendingSeedMutations: Array<() => void> | null = [];
  /**
   * Bumped by every visibility mutation except init(). The primary sidebar
   * watches it to move off the active substep once that becomes hidden.
   */
  const visibilityRevision = ref(0);
  /** Bumped by every init(); the pathway controller re-applies after a seed. */
  const seedRevision = ref(0);
  /** Keys the pathway machine last banned; setStepClickable can't lift them. */
  let pathwayBans = new Set<string>();

  const configReady = computed(() => steps.value.length > 0);

  /** Flat ordered list of ALL substep keys derived from config. */
  const orderedSubstepKeys = computed(() => getOrderedSubstepKeys(steps.value));

  const parentByKey = computed<Record<string, string>>(() => {
    const map: Record<string, string> = {};
    for (const step of steps.value) {
      for (const sub of step.substeps) map[sub.key] = step.key;
    }
    return map;
  });

  function parentOf(substepKey: string): string | null {
    return parentByKey.value[substepKey] ?? null;
  }

  function isStepHidden(stepKey: string): boolean {
    return !!navState.hiddenSteps[stepKey];
  }

  function isSubstepHidden(substepKey: string): boolean {
    const override = substepVisibilityOverrides[substepKey];
    if (override !== undefined) return !override;
    return !!navState.hiddenSubsteps[substepKey];
  }

  /** Visible substeps in config order. */
  const visibleSubstepsOrdered = computed<string[]>(() =>
    getVisibleSubstepKeys(
      orderedSubstepKeys.value,
      parentOf,
      isStepHidden,
      isSubstepHidden
    )
  );

  function replaceRecord<T>(
    target: Record<string, T>,
    next?: Record<string, T>
  ) {
    Object.keys(target).forEach((k) => delete target[k]);
    Object.assign(target, next ?? {});
  }

  function mutate(update: () => void) {
    pendingSeedMutations?.push(update);
    update();
  }

  /** Seeds state (initial load / resume). Leaves visibility overrides intact. */
  function init(
    initialStep: string,
    initialNavState: WizardNavState,
    initialStatusMap: Record<string, StepStatus>,
    initialDisabledMap: Record<string, boolean>
  ) {
    const pending = pendingSeedMutations;
    pendingSeedMutations = null;
    activeSubstep.value = initialStep;
    replaceRecord(navState.hiddenSteps, initialNavState.hiddenSteps);
    replaceRecord(navState.hiddenSubsteps, initialNavState.hiddenSubsteps);
    replaceRecord(statusMap, initialStatusMap);
    replaceRecord(disabledMap, initialDisabledMap);
    pending?.forEach((update) => update());
    seedRevision.value++;
  }

  // ── Step config + deferred calls ──────────────────────────────────────────
  // banPathwayAfter needs the step order; calls made before the config loads
  // are held and replayed in order once setSteps() runs.
  const pendingUntilConfig: Array<() => void> = [];
  const MAX_PENDING = 50;

  function whenConfigReady(fn: () => void) {
    if (configReady.value) {
      fn();
      return;
    }
    if (pendingUntilConfig.length >= MAX_PENDING) {
      const dropped = pendingUntilConfig.shift();
      pendingSeedMutations =
        pendingSeedMutations?.filter((update) => update !== dropped) ?? null;
      console.warn(
        '[WizardStateStore] Dropped oldest queued call; step config not loaded.'
      );
    }
    pendingUntilConfig.push(fn);
  }

  function setSteps(nextSteps: WizardStep[]) {
    steps.value = nextSteps;
    if (!configReady.value) return;
    const queued = pendingUntilConfig.splice(0);
    queued.forEach((fn) => fn());
  }

  // ── Mutations ─────────────────────────────────────────────────────────────

  function setStepStatus(substepKey: string, status: StepStatus | null) {
    mutate(() => {
      if (status) statusMap[substepKey] = status;
      else delete statusMap[substepKey];
    });
  }

  function setAllStatuses(map: Record<string, StepStatus>) {
    const snapshot = { ...map };
    mutate(() => replaceRecord(statusMap, snapshot));
  }

  function setStepVisibility(stepKey: string, isVisible: boolean) {
    mutate(() => {
      if (isVisible) delete navState.hiddenSteps[stepKey];
      else navState.hiddenSteps[stepKey] = true;
    });
    visibilityRevision.value++;
  }

  /** Writes the persisted layer only (dev controls / legacy direct use). */
  function setSubstepVisibility(substepKey: string, isVisible: boolean) {
    mutate(() => {
      if (isVisible) delete navState.hiddenSubsteps[substepKey];
      else navState.hiddenSubsteps[substepKey] = true;
    });
    visibilityRevision.value++;
  }

  /**
   * Visibility requested by a CHEFS form. Recorded as an override (wins over
   * navState, survives init/resume) and written through to navState so it is
   * persisted with the rest of the host wizard state as before.
   */
  function setSubstepVisibilityOverride(
    substepKey: string,
    isVisible: boolean
  ) {
    const persistedHidden = !!navState.hiddenSubsteps[substepKey];
    if (
      substepVisibilityOverrides[substepKey] === isVisible &&
      persistedHidden === !isVisible
    ) {
      return;
    }
    mutate(() => {
      substepVisibilityOverrides[substepKey] = isVisible;
      if (isVisible) delete navState.hiddenSubsteps[substepKey];
      else navState.hiddenSubsteps[substepKey] = true;
    });
    visibilityRevision.value++;
  }

  function clearSubstepVisibilityOverrides() {
    mutate(() => replaceRecord(substepVisibilityOverrides, {}));
    visibilityRevision.value++;
  }

  function setAllVisibility(nextState: WizardNavState) {
    const hiddenSteps = { ...nextState.hiddenSteps };
    const hiddenSubsteps = { ...nextState.hiddenSubsteps };
    mutate(() => {
      replaceRecord(navState.hiddenSteps, hiddenSteps);
      replaceRecord(navState.hiddenSubsteps, hiddenSubsteps);
    });
    visibilityRevision.value++;
  }

  function setStepClickable(stepKey: string, isClickable: boolean) {
    if (isClickable && pathwayBans.has(stepKey)) return;
    mutate(() => {
      if (isClickable) delete disabledMap[stepKey];
      else disabledMap[stepKey] = true;
    });
  }

  /**
   * Disables (isBanned true) or re-enables (false) every step/substep that
   * comes after stepKey in config order. A substep key bans everything after
   * that exact substep (including its own later siblings); a top-level step
   * key bans everything after that step's final substep. Can re-lock substeps
   * already unlocked, so a substep's own watcher can retroactively ban the
   * pathway once it discovers it's invalid and un-ban it once fixed.
   */
  function banPathwayAfter(stepKey: string, isBanned: boolean) {
    const update = () => {
      const keys = getKeysBannedAfter(
        orderedSubstepKeys.value,
        parentOf,
        stepKey
      );
      for (const key of keys) {
        if (isBanned) disabledMap[key] = true;
        else delete disabledMap[key];
      }
    };
    pendingSeedMutations?.push(update);
    whenConfigReady(update);
  }

  /**
   * Replaces visibility and bans with the pathway machine's result in one
   * mutation. Bumps visibilityRevision only when visibility actually changed
   * (the sidebar uses it to move off a now-hidden active substep). Returns
   * whether anything changed.
   */
  function applyPathway(result: {
    hiddenSteps: Record<string, boolean>;
    hiddenSubsteps: Record<string, boolean>;
    disabledMap: Record<string, boolean>;
  }): boolean {
    const visibilityChanged =
      !sameFlags(navState.hiddenSteps, result.hiddenSteps) ||
      !sameFlags(navState.hiddenSubsteps, result.hiddenSubsteps) ||
      Object.keys(substepVisibilityOverrides).length > 0;
    const bansChanged = !sameFlags(disabledMap, result.disabledMap);
    pathwayBans = new Set(
      Object.keys(result.disabledMap).filter((k) => result.disabledMap[k])
    );
    if (!visibilityChanged && !bansChanged) return false;

    if (visibilityChanged) {
      replaceRecord(substepVisibilityOverrides, {});
      replaceRecord(navState.hiddenSteps, { ...result.hiddenSteps });
      replaceRecord(navState.hiddenSubsteps, { ...result.hiddenSubsteps });
    }
    if (bansChanged) replaceRecord(disabledMap, { ...result.disabledMap });
    if (visibilityChanged) visibilityRevision.value++;
    return true;
  }

  /**
   * Rejects bridge calls from a CHEFS form that isn't the active step (e.g. a
   * still-mounted earlier step recalculating). Before the step config has
   * loaded there's nothing to compare against, so calls are accepted.
   */
  function isActiveBridgeSource(sourceStepKey?: string): boolean {
    if (!sourceStepKey || !configReady.value || pendingSeedMutations !== null)
      return true;
    const activeRoot = parentOf(activeSubstep.value) ?? activeSubstep.value;
    const sourceRoot = parentOf(sourceStepKey) ?? sourceStepKey;
    if (sourceRoot === activeRoot) return true;
    console.warn(
      `[WizardStateStore] Ignored bridge call from inactive step "${sourceRoot}".`
    );
    return false;
  }

  /** Full reset (new session / tests). */
  function reset() {
    pendingSeedMutations = null;
    init('', { hiddenSteps: {}, hiddenSubsteps: {} }, {}, {});
    pendingSeedMutations = [];
    replaceRecord(substepVisibilityOverrides, {});
    pendingUntilConfig.splice(0);
    steps.value = [];
    visibilityRevision.value = 0;
    seedRevision.value = 0;
    pathwayBans = new Set();
  }

  return {
    activeSubstep,
    navState,
    substepVisibilityOverrides,
    statusMap,
    disabledMap,
    steps,
    visibilityRevision,
    seedRevision,
    configReady,
    orderedSubstepKeys,
    visibleSubstepsOrdered,
    parentOf,
    isStepHidden,
    isSubstepHidden,
    init,
    setSteps,
    setStepStatus,
    setAllStatuses,
    setStepVisibility,
    setSubstepVisibility,
    setSubstepVisibilityOverride,
    clearSubstepVisibilityOverrides,
    setAllVisibility,
    setStepClickable,
    banPathwayAfter,
    applyPathway,
    isActiveBridgeSource,
    reset,
  };
});

function sameFlags(
  a: Record<string, boolean>,
  b: Record<string, boolean>
): boolean {
  const on = (r: Record<string, boolean>) =>
    Object.keys(r)
      .filter((k) => r[k])
      .sort();
  const ka = on(a);
  const kb = on(b);
  return ka.length === kb.length && ka.every((k, i) => k === kb[i]);
}
