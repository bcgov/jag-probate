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

  it.each([true, false])(
    'persists the latest early visibility override: %s',
    (visible) => {
      store.setAllVisibility({
        hiddenSteps: {},
        hiddenSubsteps: visible ? { p10: true } : {},
      });
      store.setSubstepVisibilityOverride('p10', visible);
      store.init('p9', { hiddenSteps: {}, hiddenSubsteps: {} }, {}, {});
      expect(!!store.navState.hiddenSubsteps.p10).toBe(!visible);
      const saved = {
        hiddenSteps: { ...store.navState.hiddenSteps },
        hiddenSubsteps: { ...store.navState.hiddenSubsteps },
      };
      store.reset();
      store.init('p9', saved, {}, {});
      expect(store.isSubstepHidden('p10')).toBe(!visible);
    }
  );

  it('keeps visibility overrides across init (resume / re-seed)', () => {
    store.init('p9', { hiddenSteps: {}, hiddenSubsteps: {} }, {}, {});
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

  it('does not revive config calls dropped by the queue limit when seeding', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    store.banPathwayAfter('step1-a', true);
    for (let index = 0; index < 50; index++)
      store.banPathwayAfter('missing', true);
    store.setSteps(steps);
    store.init('step1-a', { hiddenSteps: {}, hiddenSubsteps: {} }, {}, {});
    expect(store.disabledMap).toEqual({});
    expect(warn).toHaveBeenCalledOnce();
    warn.mockRestore();
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

  it('replays initialization writes including clears and bulk replacements over saved state', () => {
    store.setStepStatus('p10', 'completed');
    store.setAllStatuses({ p9: 'completed' });
    store.setStepStatus('p9', null);
    store.setStepStatus('step1-a', 'incomplete');
    store.setStepClickable('p10', false);
    store.setStepClickable('p10', true);
    store.setStepVisibility('docs', true);
    store.setAllVisibility({
      hiddenSteps: { step1: true },
      hiddenSubsteps: {},
    });
    store.setStepVisibility('step1', true);
    store.setSubstepVisibility('p10', false);
    store.init(
      'p9',
      { hiddenSteps: { docs: true }, hiddenSubsteps: { p9: true } },
      { p9: 'error', p10: 'completed' },
      { p10: true, p9: true }
    );

    expect(store.statusMap).toEqual({ 'step1-a': 'incomplete' });
    expect(store.disabledMap).toEqual({ p9: true });
    expect(store.navState).toEqual({
      hiddenSteps: {},
      hiddenSubsteps: { p10: true },
    });

    store.init(
      'p10',
      { hiddenSteps: {}, hiddenSubsteps: {} },
      { p10: 'completed' },
      {}
    );
    expect(store.statusMap).toEqual({ p10: 'completed' });
    expect(store.disabledMap).toEqual({});
  });

  it('keeps ban then unban ordering across config and initialization', () => {
    store.banPathwayAfter('step1-a', true);
    store.banPathwayAfter('step1-a', false);
    store.setSteps(steps);
    store.init(
      'step1-a',
      { hiddenSteps: {}, hiddenSubsteps: {} },
      {},
      {
        'step1-b': true,
        docs: true,
      }
    );
    expect(store.disabledMap).toEqual({});
  });

  it('clears initialization writes between application sessions', () => {
    store.setStepStatus('p10', 'completed');
    store.init('p9', { hiddenSteps: {}, hiddenSubsteps: {} }, {}, {});
    store.reset();
    store.init('p9', { hiddenSteps: {}, hiddenSubsteps: {} }, {}, {});
    expect(store.statusMap).toEqual({});
    expect(store.steps).toEqual([]);
  });

  it('keeps childless roots, config order and disabled-but-visible substeps in derived state', () => {
    store.setSteps([
      ...steps,
      {
        key: 'finish',
        number: 3,
        title: 'Finish',
        defaultSubstep: 'finish',
        substeps: [],
      },
    ]);
    store.setStepClickable('p10', false);
    store.setStepVisibility('finish', false);
    expect(store.parentOf('finish')).toBeNull();
    expect(store.orderedSubstepKeys).toEqual([
      'step1-a',
      'step1-b',
      'p9',
      'p10',
      'finish',
    ]);
    expect(store.visibleSubstepsOrdered).toEqual(store.orderedSubstepKeys);
    store.setSubstepVisibilityOverride('p10', false);
    expect(store.visibleSubstepsOrdered).toEqual([
      'step1-a',
      'step1-b',
      'p9',
      'finish',
    ]);
  });
});
