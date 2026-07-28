import { reactive, ref } from 'vue';
import type { WizardNavState, StepStatus } from '@/types/applicationStep';

/**
 * Module-level singleton reactive state shared across ALL ApplicationStepSidebar
 * instances on the page (desktop sidebar + mobile drawer).
 *
 * Any update — whether from the window bridge (CHEFS form JS) or a user click —
 * is immediately reflected everywhere because every instance reads the same refs.
 */

const _navState = reactive<WizardNavState>({
  hiddenSteps: {},
  hiddenSubsteps: {},
});
const _statusMap = reactive<Record<string, StepStatus>>({});
const _disabledMap = reactive<Record<string, boolean>>({});
const _activeSubstep = ref('');

export function useWizardState() {
  return {
    navState: _navState,
    statusMap: _statusMap,
    disabledMap: _disabledMap,
    activeSubstep: _activeSubstep,
  };
}

/** Called by the primary sidebar instance on mount to seed initial values. */
export function initWizardState(
  initialStep: string,
  initialNavState: WizardNavState,
  initialStatusMap: Record<string, StepStatus>,
  initialDisabledMap: Record<string, boolean>
) {
  _activeSubstep.value = initialStep;

  // Replace hiddenSteps / hiddenSubsteps in-place (keeps reactivity)
  Object.keys(_navState.hiddenSteps).forEach(
    (k) => delete _navState.hiddenSteps[k]
  );
  Object.assign(_navState.hiddenSteps, initialNavState.hiddenSteps ?? {});

  Object.keys(_navState.hiddenSubsteps).forEach(
    (k) => delete _navState.hiddenSubsteps[k]
  );
  Object.assign(_navState.hiddenSubsteps, initialNavState.hiddenSubsteps ?? {});

  Object.keys(_statusMap).forEach((k) => delete _statusMap[k]);
  Object.assign(_statusMap, initialStatusMap);

  Object.keys(_disabledMap).forEach((k) => delete _disabledMap[k]);
  Object.assign(_disabledMap, initialDisabledMap);
}
