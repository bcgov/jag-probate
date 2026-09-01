import { initWizardState, useWizardState } from '@/composables/useWizardState';
import type { WizardStep } from '@/types/applicationStep';
import { describe, expect, it } from 'vitest';
import { createApp, nextTick } from 'vue';
import ApplicationStepSidebar from './ApplicationStepSidebar.vue';

describe('ApplicationStepSidebar', () => {
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
});
