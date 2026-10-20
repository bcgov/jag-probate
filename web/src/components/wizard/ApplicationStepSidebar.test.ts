import { initWizardState, useWizardState } from '@/composables/useWizardState';
import type { WizardStep } from '@/types/applicationStep';
import { pinia } from '@/stores/pinia';
import { useWizardStateStore } from '@/stores/WizardStateStore';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createApp, h, nextTick, ref } from 'vue';
import ApplicationStepSidebar from './ApplicationStepSidebar.vue';

describe('ApplicationStepSidebar', () => {
  beforeEach(() => {
    useWizardStateStore(pinia).reset();
    initWizardState('', { hiddenSteps: {}, hiddenSubsteps: {} }, {}, {});
  });

  it('shows statuses restored after the primary sidebar has initialized', async () => {
    const steps: WizardStep[] = [
      {
        key: 'step1',
        number: 1,
        title: 'First',
        defaultSubstep: 'step1-a',
        substeps: [{ key: 'step1-a', label: 'First A' }],
      },
      {
        key: 'step4',
        number: 4,
        title: 'Fourth',
        defaultSubstep: 'step4-a',
        substeps: [{ key: 'step4-a', label: 'Fourth A' }],
      },
    ];
    const host = document.createElement('div');
    document.body.appendChild(host);
    const app = createApp(ApplicationStepSidebar, {
      steps,
      initialStep: 'step1-a',
    });
    app.config.warnHandler = () => {};
    app.mount(host);

    initWizardState(
      'step4-a',
      { hiddenSteps: {}, hiddenSubsteps: {} },
      { 'step1-a': 'completed', 'step4-a': 'incomplete' },
      {}
    );
    await nextTick();

    expect(useWizardState().statusMap['step1-a']).toBe('completed');
    expect(useWizardState().statusMap['step4-a']).toBe('incomplete');

    app.unmount();
    host.remove();
  });

  it('does not reset shared resume state when a secondary sidebar mounts', async () => {
    const steps: WizardStep[] = [
      {
        key: 'step1',
        number: 1,
        title: 'First',
        defaultSubstep: 'step1-a',
        substeps: [{ key: 'step1-a', label: 'First A' }],
      },
      {
        key: 'step4',
        number: 4,
        title: 'Fourth',
        defaultSubstep: 'step4-a',
        substeps: [
          { key: 'step4-a', label: 'Fourth A' },
          { key: 'step4-b', label: 'Fourth B' },
        ],
      },
    ];
    initWizardState('step4-b', { hiddenSteps: {}, hiddenSubsteps: {} }, {}, {});

    const host = document.createElement('div');
    document.body.appendChild(host);
    const app = createApp(ApplicationStepSidebar, {
      steps,
      initialStep: 'step4',
      registerBridge: false,
    });
    app.config.warnHandler = () => {};

    app.mount(host);
    await nextTick();

    expect(useWizardState().activeSubstep.value).toBe('step4-b');

    app.unmount();
    host.remove();
  });

  it('accepts active and legacy bridge calls but rejects inactive sources', async () => {
    const steps: WizardStep[] = [
      {
        key: 'step1',
        number: 1,
        title: 'First',
        defaultSubstep: 'step1-a',
        substeps: [{ key: 'step1-a', label: 'First A' }],
      },
      {
        key: 'step4',
        number: 4,
        title: 'Fourth',
        defaultSubstep: 'step4-a',
        substeps: [{ key: 'step4-a', label: 'Fourth A' }],
      },
    ];
    useWizardStateStore(pinia).setSteps(steps);
    initWizardState('step4-a', { hiddenSteps: {}, hiddenSubsteps: {} }, {}, {});
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const host = document.createElement('div');
    document.body.appendChild(host);
    const app = createApp(ApplicationStepSidebar, {
      steps,
      // Primary (registerBridge: true) sidebars are always seeded with a
      // substep key in production (see ApplicationManager's
      // firstWizardSubstepKey) — a bare step key here isn't resolvable by
      // getFallbackSubstep and would seed the wrong active substep.
      initialStep: 'step4-a',
    });
    app.config.warnHandler = () => {};
    app.mount(host);
    await nextTick();

    window.wizardSetStepStatus?.('step4-a', 'completed', 'step4-a');
    window.wizardSetStepVisibility?.('step4', false, 'step1-a');
    window.wizardSetStepClickable?.('step1', false);

    const state = useWizardState();
    expect(state.statusMap['step4-a']).toBe('completed');
    expect(state.navState.hiddenSteps['step4']).toBeUndefined();
    expect(state.activeSubstep.value).toBe('step4-a');
    expect(state.disabledMap['step1']).toBe(true);
    expect(warn).toHaveBeenCalledOnce();

    app.unmount();
    host.remove();
    warn.mockRestore();
  });

  it('ghosts hidden substeps by default and omits configured substeps', async () => {
    const steps: WizardStep[] = [
      {
        key: 'step7',
        number: 7,
        title: 'Application Documents',
        defaultSubstep: 'form-preview-p9',
        substeps: [
          { key: 'form-preview-p9', label: 'Preview P9' },
          {
            key: 'form-preview-p10',
            label: 'Preview P10',
            hiddenIsGhost: false,
          },
          { key: 'form-preview-p11', label: 'Preview P11' },
        ],
      },
    ];
    initWizardState(
      'form-preview-p9',
      { hiddenSteps: {}, hiddenSubsteps: {} },
      {},
      {}
    );

    const host = document.createElement('div');
    document.body.appendChild(host);
    const app = createApp(ApplicationStepSidebar, {
      steps,
      initialStep: 'form-preview-p9',
    });
    app.config.warnHandler = () => {};
    app.mount(host);
    await nextTick();

    window.wizardSetSubstepVisibility?.(
      'form-preview-p10',
      false,
      'form-preview-p9'
    );
    window.wizardSetSubstepVisibility?.(
      'form-preview-p11',
      false,
      'form-preview-p9'
    );
    await nextTick();
    expect(host.textContent).not.toContain('Preview P10');
    expect(host.textContent).toContain('Preview P11');
    const p11Entry = Array.from(host.querySelectorAll('.wiz-subitem')).find(
      (entry) => entry.textContent?.includes('Preview P11')
    );
    expect(p11Entry?.classList.contains('is-ghost')).toBe(true);

    window.wizardSetSubstepVisibility?.(
      'form-preview-p10',
      true,
      'form-preview-p9'
    );
    await nextTick();
    expect(host.textContent).toContain('Preview P10');

    app.unmount();
    host.remove();
  });

  it('bans the pathway from the exact substep, sparing the current step', async () => {
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
        key: 'step2',
        number: 2,
        title: 'Second',
        defaultSubstep: 'step2-a',
        substeps: [{ key: 'step2-a', label: 'Second A' }],
      },
    ];
    useWizardStateStore(pinia).setSteps(steps);
    initWizardState('step1-a', { hiddenSteps: {}, hiddenSubsteps: {} }, {}, {});

    const host = document.createElement('div');
    document.body.appendChild(host);
    const app = createApp(ApplicationStepSidebar, {
      steps,
      initialStep: 'step1-a',
    });
    app.config.warnHandler = () => {};
    app.mount(host);
    await nextTick();

    window.wizardBanPathway?.('step1-a', true, 'step1-a');

    const state = useWizardState();
    expect(state.disabledMap['step1-b']).toBe(true);
    expect(state.disabledMap['step2-a']).toBe(true);
    expect(state.disabledMap['step2']).toBe(true);
    // The step the caller is still on must stay reachable.
    expect(state.disabledMap['step1']).toBeUndefined();
    expect(state.disabledMap['step1-a']).toBeUndefined();

    window.wizardBanPathway?.('step1-a', false, 'step1-a');
    expect(state.disabledMap['step1-b']).toBeUndefined();
    expect(state.disabledMap['step2-a']).toBeUndefined();
    expect(state.disabledMap['step2']).toBeUndefined();

    app.unmount();
    host.remove();
  });

  it('bans after the final substep when given a top-level step key', async () => {
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
        key: 'step2',
        number: 2,
        title: 'Second',
        defaultSubstep: 'step2-a',
        substeps: [{ key: 'step2-a', label: 'Second A' }],
      },
    ];
    useWizardStateStore(pinia).setSteps(steps);
    initWizardState('step1-a', { hiddenSteps: {}, hiddenSubsteps: {} }, {}, {});

    const host = document.createElement('div');
    document.body.appendChild(host);
    const app = createApp(ApplicationStepSidebar, {
      steps,
      initialStep: 'step1-a',
    });
    app.config.warnHandler = () => {};
    app.mount(host);
    await nextTick();

    window.wizardBanPathway?.('step1', true, 'step1-a');

    const state = useWizardState();
    expect(state.disabledMap['step1-b']).toBeUndefined();
    expect(state.disabledMap['step2-a']).toBe(true);
    expect(state.disabledMap['step2']).toBe(true);

    app.unmount();
    host.remove();
  });

  it('applies rule visibility set before the sidebar mounts and keeps it after unmount', async () => {
    const steps: WizardStep[] = [
      {
        key: 'docs',
        number: 1,
        title: 'Docs',
        defaultSubstep: 'p9',
        substeps: [
          { key: 'p9', label: 'Preview P9' },
          { key: 'p10', label: 'Preview P10', hiddenIsGhost: false },
        ],
      },
    ];
    // Bridge is installed at bootstrap; the CHEFS rule runs before mount.
    const { installWizardBridge } = await import('@/stores/wizardBridge');
    installWizardBridge();
    window.wizardSetSubstepVisibility?.('p10', false, 'docs');

    const host = document.createElement('div');
    document.body.appendChild(host);
    const app = createApp(ApplicationStepSidebar, {
      steps,
      initialStep: 'p9',
    });
    app.config.warnHandler = () => {};
    app.mount(host);
    await nextTick();

    expect(host.textContent).not.toContain('Preview P10');

    app.unmount();
    expect(typeof window.wizardSetSubstepVisibility).toBe('function');
    window.wizardSetSubstepVisibility?.('p10', true, 'docs');
    expect(useWizardStateStore(pinia).isSubstepHidden('p10')).toBe(false);
    host.remove();
  });

  it('moves off the active substep when a rule hides it after mount', async () => {
    const steps: WizardStep[] = [
      {
        key: 'docs',
        number: 1,
        title: 'Docs',
        defaultSubstep: 'p9',
        substeps: [
          { key: 'p9', label: 'Preview P9' },
          { key: 'p10', label: 'Preview P10' },
        ],
      },
    ];
    const host = document.createElement('div');
    document.body.appendChild(host);
    const navigated: string[] = [];
    const app = createApp(ApplicationStepSidebar, {
      steps,
      initialStep: 'p10',
      onNavigate: (key: string) => navigated.push(key),
    });
    app.config.warnHandler = () => {};
    app.mount(host);
    await nextTick();
    expect(useWizardState().activeSubstep.value).toBe('p10');

    window.wizardSetSubstepVisibility?.('p10', false, 'docs');
    await nextTick();

    expect(useWizardState().activeSubstep.value).toBe('p9');
    expect(navigated).toEqual(['p9']);

    app.unmount();
    host.remove();
  });

  it('derives secondary navigation from reactive props without replacing store configuration', async () => {
    const store = useWizardStateStore(pinia);
    store.setSteps([
      {
        key: 'other',
        number: 1,
        title: 'Other',
        defaultSubstep: 'other',
        substeps: [],
      },
    ]);
    store.init(
      'solo',
      { hiddenSteps: {}, hiddenSubsteps: {} },
      {},
      { p10: true }
    );
    const steps = ref<WizardStep[]>([
      {
        key: 'solo',
        number: 1,
        title: 'Solo',
        defaultSubstep: 'solo',
        substeps: [],
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
    ]);
    const sidebar = ref<InstanceType<typeof ApplicationStepSidebar>>();
    const host = document.createElement('div');
    document.body.appendChild(host);
    const app = createApp(() =>
      h(ApplicationStepSidebar, {
        ref: sidebar,
        steps: steps.value,
        registerBridge: false,
      })
    );
    app.config.warnHandler = () => {};
    app.mount(host);
    await nextTick();

    expect(sidebar.value?.hasNext).toBe(true);
    store.setStepVisibility('docs', false);
    await nextTick();
    expect(sidebar.value?.hasNext).toBe(false);
    store.setStepVisibility('docs', true);
    store.setSubstepVisibilityOverride('p9', false);
    await nextTick();
    expect(sidebar.value?.hasNext).toBe(true);

    steps.value = [steps.value[0]];
    await nextTick();
    expect(sidebar.value?.hasNext).toBe(false);
    expect(sidebar.value?.isLastStep).toBe(true);
    expect(store.orderedSubstepKeys).toEqual(['other']);
    app.unmount();
    host.remove();
  });
});
