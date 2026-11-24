import { deriveBaselineNavState } from '@/config/wizardSteps';
import {
  installWizardBridge,
  uninstallWizardBridgeForTests,
} from '@/stores/wizardBridge';
import { useWizardDataStore } from '@/stores/WizardDataStore';
import { useWizardStateStore } from '@/stores/WizardStateStore';
import { createPinia, type Pinia, setActivePinia } from 'pinia';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { effectScope, type EffectScope } from 'vue';
import { completeApplication, STEPS } from './fixtures';
import { resetPathwayMachineModeForTests, setPathwayMachineMode } from './mode';
import { usePathwayController } from './usePathwayController';

const flush = () => new Promise((r) => setTimeout(r, 0));

describe('usePathwayController', () => {
  let pinia: Pinia;
  let scope: EffectScope;

  beforeEach(() => {
    pinia = createPinia();
    setActivePinia(pinia);
    setPathwayMachineMode('on');
    uninstallWizardBridgeForTests();
    installWizardBridge(pinia);
    scope = effectScope();
    scope.run(() => usePathwayController(pinia));
  });

  afterEach(() => {
    scope.stop();
    uninstallWizardBridgeForTests();
    resetPathwayMachineModeForTests();
  });

  function seed(data: Record<string, any>) {
    const state = useWizardStateStore(pinia);
    useWizardDataStore(pinia).setStepData('form-testamentary-docs', data);
    state.setSteps(STEPS);
    state.init('form-deceased', deriveBaselineNavState(STEPS), {}, {});
    return state;
  }

  it('waits for the store to be seeded', async () => {
    const state = useWizardStateStore(pinia);
    useWizardDataStore(pinia).setStepData('form-x', completeApplication());
    state.setSteps(STEPS);
    await flush();
    expect(state.isStepHidden('form-testamentary-docs')).toBe(false);
    expect(state.navState.hiddenSteps).toEqual({});
  });

  it('keeps Step 7 open when Step 2 runs its script again', async () => {
    const state = seed(completeApplication());
    await flush();
    expect(state.isStepHidden('form-application-documents')).toBe(false);

    // guidedPathController's transient `show === false` branch.
    window.wizardSetStepVisibility?.('form-application-documents', false);
    window.wizardSetStepVisibility?.('form-assets', false);
    window.wizardBanPathway?.('form-spouse', true);
    useWizardDataStore(pinia).setStepData(
      'form-testamentary-docs',
      completeApplication()
    );
    await flush();

    expect(state.isStepHidden('form-application-documents')).toBe(false);
    expect(state.isStepHidden('form-assets')).toBe(false);
    expect(state.disabledMap).toEqual({});
    expect(window.__wizardBridgeLog?.map((c) => c.fn)).toEqual([
      'wizardSetStepVisibility',
      'wizardSetStepVisibility',
      'wizardBanPathway',
    ]);
  });

  it('closes and reopens the pathway with the Step 2 answer', async () => {
    const state = seed(completeApplication());
    await flush();

    const data = useWizardDataStore(pinia);
    data.setStepData('form-testamentary-docs', {
      ...completeApplication(),
      willExists: 'yes',
    });
    await flush();
    expect(state.isStepHidden('form-related-people')).toBe(true);
    expect(state.isStepHidden('form-application-documents')).toBe(true);

    data.setStepData('form-testamentary-docs', completeApplication());
    await flush();
    expect(state.isStepHidden('form-application-documents')).toBe(false);
  });

  it('replaces a persisted resume state with the derived one after each seed', async () => {
    const state = seed({});
    await flush();
    expect(state.isStepHidden('form-testamentary-docs')).toBe(true);

    state.init(
      'form-deceased',
      { hiddenSteps: {}, hiddenSubsteps: {} },
      {},
      { 'form-notify': true }
    );
    await flush();
    expect(state.isStepHidden('form-testamentary-docs')).toBe(true);
    expect(state.disabledMap).toEqual({});
  });

  it('owns the review status once the review has been visited', async () => {
    const data = completeApplication();
    delete data.deceasedDeathDate;
    const state = seed(data);
    await flush();
    expect(state.statusMap['form-full-review']).toBeUndefined();

    state.setStepStatus('form-full-review', 'incomplete');
    await flush();
    expect(state.statusMap['form-full-review']).toBe('error');

    // The form's DOM-badge validation no longer decides it.
    window.wizardSetStepStatus?.('form-full-review', 'completed');
    expect(state.statusMap['form-full-review']).toBe('error');

    useWizardDataStore(pinia).setStepData(
      'form-testamentary-docs',
      completeApplication()
    );
    await flush();
    expect(state.statusMap['form-full-review']).toBe('completed');
  });

  it('only observes in observe mode', async () => {
    setPathwayMachineMode('observe');
    const state = seed(completeApplication());
    await flush();
    expect(state.isStepHidden('form-application-documents')).toBe(true);

    window.wizardSetStepVisibility?.('form-application-documents', true);
    expect(state.isStepHidden('form-application-documents')).toBe(false);
  });
});
