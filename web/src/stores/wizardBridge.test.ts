import { pinia } from '@/stores/pinia';
import type { WizardStep } from '@/types/applicationStep';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  attachNavigationHandlers,
  installWizardBridge,
  uninstallWizardBridgeForTests,
} from './wizardBridge';
import { useWizardStateStore } from './WizardStateStore';

const steps: WizardStep[] = [
  {
    key: 'docs',
    number: 1,
    title: 'Docs',
    defaultSubstep: 'p9',
    substeps: [
      { key: 'p9', label: 'P9' },
      { key: 'p10', label: 'P10' },
    ],
  },
  {
    key: 'filing',
    number: 2,
    title: 'Filing',
    defaultSubstep: 'filing-a',
    substeps: [{ key: 'filing-a', label: 'Filing A' }],
  },
];

describe('wizardBridge', () => {
  beforeEach(() => {
    uninstallWizardBridgeForTests();
    useWizardStateStore(pinia).reset();
  });

  afterEach(() => {
    uninstallWizardBridgeForTests();
  });

  it('exposes the state setters without any component mounted', () => {
    installWizardBridge();
    const store = useWizardStateStore(pinia);

    window.wizardSetSubstepVisibility?.('p10', false, 'docs');
    window.wizardSetStepStatus?.('p9', 'completed');

    expect(store.isSubstepHidden('p10')).toBe(true);
    expect(store.statusMap['p9']).toBe('completed');
  });

  it('applies substep visibility from an inactive source (rule result)', () => {
    installWizardBridge();
    const store = useWizardStateStore(pinia);
    store.setSteps(steps);
    store.init('filing-a', { hiddenSteps: {}, hiddenSubsteps: {} }, {}, {});

    window.wizardSetSubstepVisibility?.('p10', false, 'docs');
    expect(store.isSubstepHidden('p10')).toBe(true);
  });

  it('queues navigation calls until handlers attach, then replays in order', () => {
    installWizardBridge();
    const calls: string[] = [];

    window.wizardNavigateNext?.();
    window.wizardUpdateSidebar?.('p10');
    window.wizardNavigatePrevious?.();
    expect(calls).toEqual([]);

    const detach = attachNavigationHandlers({
      updateSidebar: (key) => calls.push(`update:${key}`),
      navigateNext: () => calls.push('next'),
      navigatePrevious: () => calls.push('previous'),
    });
    expect(calls).toEqual(['next', 'update:p10', 'previous']);

    window.wizardNavigateNext?.();
    expect(calls).toHaveLength(4);

    detach();
    window.wizardNavigateNext?.();
    expect(calls).toHaveLength(4);
  });

  it('bounds the navigation queue', () => {
    installWizardBridge();
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    for (let i = 0; i < 60; i++) window.wizardUpdateSidebar?.(`k${i}`);

    const keys: string[] = [];
    attachNavigationHandlers({
      updateSidebar: (key) => keys.push(key),
      navigateNext: () => undefined,
      navigatePrevious: () => undefined,
    });
    expect(keys).toHaveLength(50);
    expect(keys[0]).toBe('k10');
    warn.mockRestore();
  });

  it('is idempotent', () => {
    installWizardBridge();
    const fn = window.wizardSetSubstepVisibility;
    installWizardBridge();
    expect(window.wizardSetSubstepVisibility).toBe(fn);
  });
});
