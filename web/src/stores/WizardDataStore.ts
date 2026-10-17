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
   * - Later steps overwrite earlier steps for the same key (last-write wins),
   *   except that a non-empty container field beats another step's top-level
   *   copy of the same key, whichever order the steps are in.
   * - Container fields (e.g. `applicant.applicantCourthouseName`) are also
   *   exposed flat, since normalizeContainerCapture() strips their flat
   *   alias before saving, and other steps expect the flat key on inject.
   */
  const accumulatedData = computed<Record<string, any>>(() => {
    const result: Record<string, any> = {};
    const origin: Record<string, { step: string; nested: boolean }> = {};

    // A step's container field is authoritative over a top-level copy of the
    // same key held by a different step (typically a stale copy that step
    // captured from injected data). Empty container values don't override.
    const write = (step: string, key: string, value: any, nested: boolean) => {
      const prev = origin[key];
      if (prev && prev.step !== step && prev.nested !== nested) {
        const nestedValue = nested ? value : result[key];
        const topValue = nested ? result[key] : value;
        const winner = isEmptyValue(nestedValue)
          ? isEmptyValue(topValue)
            ? 'last'
            : 'top'
          : 'nested';
        if (winner === 'nested' && !nested) return;
        if (winner === 'top' && nested) return;
      }
      result[key] = value;
      origin[key] = { step, nested };
    };

    for (const [step, data] of Object.entries(stepData.value)) {
      for (const [key, value] of Object.entries(data)) {
        if (isExcludedKey(key)) continue;
        write(step, key, value, false);

        if (isContainerValue(value)) {
          for (const [childKey, childValue] of Object.entries(value)) {
            if (isExcludedKey(childKey)) continue;
            write(step, childKey, childValue, true);
          }
        }
      }
    }
    return result;
  });

  /**
   * Keys other steps currently hold, both top-level and as container
   * children (the same shape accumulatedData exposes flat). A step must not
   * claim these as its own when they show up in its form data.
   */
  function keysHeldByOtherSteps(stepKey: string): Set<string> {
    const held = new Set<string>();
    for (const [step, data] of Object.entries(stepData.value)) {
      if (step === stepKey) continue;
      for (const key of collectHeldKeys(data)) held.add(key);
    }
    return held;
  }

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

  return {
    stepData,
    accumulatedData,
    keysHeldByOtherSteps,
    setStepData,
    getStepData,
    reset,
  };
});

function isExcludedKey(key: string): boolean {
  return key.startsWith('_') || WIZARD_MANAGED_KEYS.has(key);
}

function isContainerValue(value: unknown): value is Record<string, any> {
  return !!value && typeof value === 'object' && !Array.isArray(value);
}

function isEmptyValue(value: unknown): boolean {
  return value === undefined || value === null || value === '';
}

/** Top-level keys plus one level of container child keys. */
export function collectHeldKeys(data: Record<string, any>): Set<string> {
  const keys = new Set<string>();
  for (const [key, value] of Object.entries(data ?? {})) {
    if (isExcludedKey(key)) continue;
    keys.add(key);
    if (isContainerValue(value)) {
      for (const childKey of Object.keys(value)) {
        if (!isExcludedKey(childKey)) keys.add(childKey);
      }
    }
  }
  return keys;
}
