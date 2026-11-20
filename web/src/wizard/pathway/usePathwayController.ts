/**
 * Runs the pathway machine whenever the accumulated form data, the step
 * config or the seeded state changes, and writes the result to
 * WizardStateStore (mode 'on') or logs the difference from the legacy state
 * (mode 'observe'). Call once from the wizard host component's setup; the
 * watchers stop with that component.
 *
 * Only runs after the store has been seeded (init() happens after
 * StepFormViewer's hydration-complete), so the persisted resume state is used
 * for first paint and then replaced by the derived state.
 */
import { pinia as appPinia } from '@/stores/pinia';
import { useWizardDataStore } from '@/stores/WizardDataStore';
import { useWizardStateStore } from '@/stores/WizardStateStore';
import type { Pinia } from 'pinia';
import { watch } from 'vue';
import { deriveFacts, type PathwayFacts } from './facts';
import { getPathwayMachineMode } from './mode';
import { evaluatePathway, type PathwayResult } from './pathwayMachine';

export const REVIEW_SUBSTEP = 'form-full-review';

export function usePathwayController(pinia: Pinia = appPinia) {
  const dataStore = useWizardDataStore(pinia);
  const stateStore = useWizardStateStore(pinia);
  let scheduled = false;

  function run() {
    scheduled = false;
    const mode = getPathwayMachineMode();
    if (mode === 'off') return;
    if (!stateStore.configReady || stateStore.seedRevision === 0) return;

    const facts = deriveFacts(dataStore.accumulatedData);
    const result = evaluatePathway(stateStore.steps, facts);

    if (mode === 'observe') {
      logShadowDiff(result, facts);
      return;
    }
    stateStore.applyPathway(result);
    syncReviewStatus(facts);
  }

  /** Review status is written only once the review has been visited. */
  function syncReviewStatus(facts: PathwayFacts) {
    if (!stateStore.statusMap[REVIEW_SUBSTEP]) return;
    const next = facts.reviewValid ? 'completed' : 'error';
    if (stateStore.statusMap[REVIEW_SUBSTEP] !== next) {
      stateStore.setStepStatus(REVIEW_SUBSTEP, next);
    }
  }

  function logShadowDiff(result: PathwayResult, facts: PathwayFacts) {
    if (!import.meta.env.DEV) return;
    const diff = (
      label: string,
      legacy: Record<string, boolean>,
      machine: Record<string, boolean>
    ) => {
      const keys = new Set([...Object.keys(legacy), ...Object.keys(machine)]);
      const changed = [...keys].filter((k) => !!legacy[k] !== !!machine[k]);
      return changed.length
        ? [
            `${label}: ${changed.map((k) => `${k} legacy=${!!legacy[k]} machine=${!!machine[k]}`).join(', ')}`,
          ]
        : [];
    };
    const legacyHiddenSubsteps: Record<string, boolean> = {};
    for (const key of stateStore.orderedSubstepKeys) {
      if (stateStore.isSubstepHidden(key)) legacyHiddenSubsteps[key] = true;
    }
    const lines = [
      ...diff(
        'hiddenSteps',
        stateStore.navState.hiddenSteps,
        result.hiddenSteps
      ),
      ...diff('hiddenSubsteps', legacyHiddenSubsteps, result.hiddenSubsteps),
      ...diff('disabled', stateStore.disabledMap, result.disabledMap),
    ];
    if (lines.length) {
      console.debug(
        `[pathway:observe] state=${result.state} bans=${result.activeBans.join('|') || '-'}\n  ${lines.join('\n  ')}`
      );
    }
    // Review rules vs. the badges the CHEFS review rendered (when on screen).
    setTimeout(() => {
      const panel = document.querySelector<HTMLElement>(
        '.wiz-panel-step5-review-your-answers'
      );
      if (!panel || panel.offsetParent === null) return;
      const badges = [
        ...panel.querySelectorAll<HTMLElement>('.required-badge'),
      ].filter((el) => el.offsetParent !== null).length;
      if (badges !== facts.reviewMissing.length) {
        console.debug(
          `[pathway:observe] review badges=${badges} machine=${facts.reviewMissing.length}`,
          facts.reviewMissing
        );
      }
    }, 100);
  }

  function schedule() {
    if (scheduled) return;
    scheduled = true;
    queueMicrotask(run);
  }

  watch(
    [
      () => dataStore.accumulatedData,
      () => stateStore.steps,
      () => stateStore.seedRevision,
      () => stateStore.statusMap[REVIEW_SUBSTEP],
    ],
    schedule,
    { immediate: true }
  );

  return { evaluateNow: run };
}
