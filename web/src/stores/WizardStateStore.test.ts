import { pinia } from '@/stores/pinia';
import type { WizardStep } from '@/types/applicationStep';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useWizardStateStore } from './WizardStateStore';

const steps: WizardStep[] = [
  {
    key: 'step1',
    number: 1,
    title: 'First',
    defaultSubstep: 'step1-a',
    substeps: [
      { key: 'step1-a', label: 'First A' },
      { key: 'step1-b', label: 'First B' },
    ],
  },
  {
    key: 'docs',
    number: 2,
    title: 'Docs',
    defaultSubstep: 'p9',
    substeps: [
      { key: 'p9', label: 'P9' },
      { key: 'p10', label: 'P10' },
    ],
  },
];

describe('WizardStateStore', () => {
  let store: ReturnType<typeof useWizardStateStore>;

  beforeEach(() => {
    store = useWizardStateStore(pinia);
    store.reset();
  });

  it('keeps visibility overrides across init (resume / re-seed)', () => {
    store.setSubstepVisibilityOverride('p10', false);
    store.init('p9', { hiddenSteps: {}, hiddenSubsteps: {} }, {}, {});

    expect(store.isSubstepHidden('p10')).toBe(true);
    expect(store.navState.hiddenSubsteps['p10']).toBeUndefined();
  });

  it('lets an override reveal a substep hidden by config/saved state', () => {
    store.init(
      'p9',
      { hiddenSteps: {}, hiddenSubsteps: { p10: true } },
      {},
      {}
    );
    expect(store.isSubstepHidden('p10')).toBe(true);

    store.setSubstepVisibilityOverride('p10', true);
    expect(store.isSubstepHidden('p10')).toBe(false);
    // Written through so it is persisted with the host wizard state.
    expect(store.navState.hiddenSubsteps['p10']).toBeUndefined();
  });

  it('only bumps the visibility revision when something changes', () => {
    store.setSubstepVisibilityOverride('p10', false);
    const rev = store.visibilityRevision;
    store.setSubstepVisibilityOverride('p10', false);
    expect(store.visibilityRevision).toBe(rev);
    store.setSubstepVisibilityOverride('p10', true);
    expect(store.visibilityRevision).toBe(rev + 1);
  });

  it('respects hidden parent steps in the visible ordering', () => {
    store.setSteps(steps);
    store.setStepVisibility('docs', false);
    expect(store.visibleSubstepsOrdered).toEqual(['step1-a', 'step1-b']);
  });

  it('queues banPathway until the step config is loaded', () => {
    store.banPathwayAfter('step1-a', true);
    expect(store.disabledMap['step1-b']).toBeUndefined();

    store.setSteps(steps);
    expect(store.disabledMap['step1-b']).toBe(true);
    expect(store.disabledMap['docs']).toBe(true);
    expect(store.disabledMap['step1']).toBeUndefined();
  });

  it('accepts bridge sources before config and rejects inactive ones after', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    expect(store.isActiveBridgeSource('docs')).toBe(true);

    store.setSteps(steps);
    store.init('step1-a', { hiddenSteps: {}, hiddenSubsteps: {} }, {}, {});
    expect(store.isActiveBridgeSource('step1-b')).toBe(true);
    expect(store.isActiveBridgeSource('docs')).toBe(false);
    expect(warn).toHaveBeenCalledOnce();
    warn.mockRestore();
  });
});
