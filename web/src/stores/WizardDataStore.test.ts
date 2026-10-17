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

  it("prefers a step's container field over another step's stale top-level copy", () => {
    const store = useWizardDataStore();

    store.setStepData('form-assets', {
      realProperty: { realPropertyEntries: [{ marketValue: 1234567 }] },
    });
    // Later step holding a stale flat copy it captured from injected data.
    store.setStepData('form-application-documents', {
      realPropertyEntries: [{ marketValue: 2000000 }],
    });

    expect(store.accumulatedData.realPropertyEntries).toEqual([
      { marketValue: 1234567 },
    ]);
  });

  it('does not let an empty container field override a non-empty copy', () => {
    const store = useWizardDataStore();

    store.setStepData('form-applicant', {
      applicant: { applicantCourthouseName: '' },
    });
    store.setStepData('form-notify', {
      applicantCourthouseName: 'Chilliwack Law Courts',
    });

    expect(store.accumulatedData.applicantCourthouseName).toBe(
      'Chilliwack Law Courts'
    );
  });

  it('keeps last-write-wins within a single step', () => {
    const store = useWizardDataStore();

    store.setStepData('form-x', { checks: { flag: true }, flag: false });

    expect(store.accumulatedData.flag).toBe(false);
  });
});

describe('WizardDataStore keysHeldByOtherSteps', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it('returns other steps top-level and container child keys only', () => {
    const store = useWizardDataStore();

    store.setStepData('form-assets', {
      realProperty: { realPropertyEntries: [] },
      _transient: { hidden: 1 },
    });
    store.setStepData('form-application-documents', { ownField: 'x' });

    const held = store.keysHeldByOtherSteps('form-application-documents');
    expect([...held].sort()).toEqual(['realProperty', 'realPropertyEntries']);
  });
});
