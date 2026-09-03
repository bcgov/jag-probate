import { describe, it, expect, beforeEach } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useWizardDataStore } from './WizardDataStore';

describe('WizardDataStore accumulatedData', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it('exposes container-nested fields flat so later steps can read them', () => {
    const store = useWizardDataStore();

    // form-applicant stores applicantCourthouseName nested inside its
    // "applicant" container (normalizeContainerCapture strips the flat alias
    // before saving) — accumulatedData must still surface it flat for
    // injection into other steps (e.g. form-filing's read-only summary).
    store.setStepData('form-applicant', {
      applicant: {
        applicantCourthouse: 6011,
        applicantCourthouseName: 'Vancouver Law Courts',
      },
    });

    expect(store.accumulatedData.applicantCourthouseName).toBe(
      'Vancouver Law Courts'
    );
    expect(store.accumulatedData.applicantCourthouse).toBe(6011);
    // The container itself remains available too, for steps with a matching container.
    expect(store.accumulatedData.applicant).toEqual({
      applicantCourthouse: 6011,
      applicantCourthouseName: 'Vancouver Law Courts',
    });
  });

  it('lets a later step overwrite an earlier step for the same flat key', () => {
    const store = useWizardDataStore();

    store.setStepData('form-a', { shared: { value: 1 } });
    store.setStepData('form-b', { shared: { value: 2 } });

    expect(store.accumulatedData.value).toBe(2);
  });

  it('excludes wizard-managed and transient keys from the merge', () => {
    const store = useWizardDataStore();

    store.setStepData('form-x', {
      currentStep: 'form-x',
      _internal: { foo: 'bar' },
      realField: 'keepMe',
    });

    expect(store.accumulatedData.currentStep).toBeUndefined();
    expect(store.accumulatedData._internal).toBeUndefined();
    expect(store.accumulatedData.realField).toBe('keepMe');
  });
});
