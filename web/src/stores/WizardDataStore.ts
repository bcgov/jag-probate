import { defineStore } from 'pinia';
import { computed, ref } from 'vue';

/**
 * Keys that are managed by the Vue wizard layer and should NOT be propagated
 * from one step's CHEFS form data into another step's form.
 */
const WIZARD_MANAGED_KEYS = new Set([
  'currentStep',
  'currentSubstep',
  'wizardStatusState',
  'wizardNavState',
  'wizardDisabledSteps',
  'showStepFunction',
  'showSurvey2',
  'animationDebounceScript',
  'hostWizardState',
]);

/**
 * Holds per-step CHEFS form data and exposes a flat accumulated model
 * that can be injected into each step on load — enabling cross-step data sharing.
 *
 * Example shared fields:
 *   step1 → deceasedName, deceasedDeathDate, firstNameGivenName, ...
 *   step3 → notifyPeopleData, spouse, child, creditor, ...
 *   step4 → applicant, citorData, ...
 */
export const useWizardDataStore = defineStore('wizardData', () => {
  /** Raw data captured from each step's CHEFS form instance. */
  const stepData = ref<Record<string, Record<string, any>>>({});

  /**
   * Flat merged model across all steps.
   * - Keys starting with `_` (transient form-controller objects) are excluded.
   * - Wizard-managed keys are excluded.
   * - Later steps overwrite earlier steps for the same key (last-write wins).
   * - Container fields (e.g. `applicant.applicantCourthouseName`) are also
   *   exposed flat, since normalizeContainerCapture() strips their flat
   *   alias before saving, and other steps expect the flat key on inject.
   */
  const accumulatedData = computed<Record<string, any>>(() => {
    const result: Record<string, any> = {};
    for (const data of Object.values(stepData.value)) {
      for (const [key, value] of Object.entries(data)) {
        if (key.startsWith('_') || WIZARD_MANAGED_KEYS.has(key)) continue;
        result[key] = value;

        if (value && typeof value === 'object' && !Array.isArray(value)) {
          for (const [childKey, childValue] of Object.entries(value)) {
            if (childKey.startsWith('_') || WIZARD_MANAGED_KEYS.has(childKey))
              continue;
            result[childKey] = childValue;
          }
        }
      }
    }
    return result;
  });

  /**
   * Store (or replace) the data snapshot for a given step.
   * Call this on every `formio:change` event.
   */
  function setStepData(stepKey: string, data: Record<string, any>): void {
    // Shallow-copy so mutations to the CHEFS object don't silently corrupt the store.
    stepData.value[stepKey] = { ...data };
  }

  /** Returns the last-captured data for a step, or an empty object. */
  function getStepData(stepKey: string): Record<string, any> {
    return stepData.value[stepKey] ?? {};
  }

  /** Clear all captured data (e.g. on logout or new application). */
  function reset(): void {
    stepData.value = {};
  }

  return { stepData, accumulatedData, setStepData, getStepData, reset };
});
