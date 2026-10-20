import { useLayoutStore } from '@/stores/LayoutStore';
import { pinia } from '@/stores/pinia';
import { useWizardStateStore } from '@/stores/WizardStateStore';
import {
  installWizardBridge,
  uninstallWizardBridgeForTests,
} from '@/stores/wizardBridge';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createApp, nextTick } from 'vue';
import ApplicationManager from './ApplicationManager.vue';

const viewer = vi.hoisted(() => ({
  emit: null as null | ((event: string, ...args: unknown[]) => void),
  submissionId: '',
}));

vi.mock('vue-router', async (importOriginal) => ({
  ...(await importOriginal<typeof import('vue-router')>()),
  useRoute: () => ({ params: { id: viewer.submissionId || undefined } }),
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/components/wizard/StepFormViewer.vue', async () => {
  const { defineComponent } = await import('vue');
  return {
    default: defineComponent({
      setup(_, { emit }) {
        viewer.emit = emit;
        return () => null;
      },
    }),
  };
});

const sidebarDtos = [
  { key: 'survey', title: 'Survey', order: 0, children: [] },
  {
    key: 'step1',
    title: 'First',
    order: 1,
    children: [
      { key: 'step1-a', title: 'First A' },
      { key: 'step1-b', title: 'First B' },
    ],
  },
  {
    key: 'step2',
    title: 'Second',
    order: 2,
    children: [{ key: 'step2-a', title: 'Second A' }],
  },
];

async function settle() {
  await Promise.resolve();
  await nextTick();
  await nextTick();
}

describe('ApplicationManager wizard initialization', () => {
  let app: ReturnType<typeof createApp> | null;
  let host: HTMLDivElement;
  let resolveConfig: (value: typeof sidebarDtos) => void;

  beforeEach(() => {
    useWizardStateStore(pinia).reset();
    uninstallWizardBridgeForTests();
    installWizardBridge(pinia);
    viewer.submissionId = '';
    viewer.emit = null;
    app = null;
    host = document.createElement('div');
    document.body.appendChild(host);
  });

  afterEach(() => {
    app?.unmount();
    host.remove();
    uninstallWizardBridgeForTests();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  function mountManager() {
    const config = new Promise<typeof sidebarDtos>((resolve) => {
      resolveConfig = resolve;
    });
    app = createApp(ApplicationManager);
    app.use(pinia);
    app.config.warnHandler = () => {};
    app.provide('chefsService', {
      getSidebarStructure: vi.fn(() => config),
    });
    app.mount(host);
  }

  async function enterWizard(ready = false) {
    resolveConfig(sidebarDtos);
    viewer.emit?.('hydration-complete');
    await settle();
    viewer.emit?.('survey-complete');
    if (ready) viewer.emit?.('step-ready', 'step1');
    await settle();
  }

  it('keeps accepted pre-mount bans and statuses through config and both sidebars', async () => {
    const store = useWizardStateStore(pinia);
    const seed = vi.spyOn(store, 'init');
    mountManager();
    window.wizardBanPathway?.('step1-a', true);
    window.wizardSetStepStatus?.('step1-b', 'completed');
    await enterWizard();
    expect(host.querySelectorAll('.wiz-sidebar')).toHaveLength(1);
    expect(store.disabledMap['step1-b']).toBe(true);
    expect(store.disabledMap.step2).toBe(true);
    expect(store.statusMap['step1-b']).toBe('completed');

    viewer.emit?.('step-ready', 'step1');
    await settle();
    expect(document.querySelectorAll('.wiz-sidebar')).toHaveLength(2);
    useLayoutStore(pinia).openMobileNav();
    await settle();
    useLayoutStore(pinia).closeMobileNav();
    await settle();
    expect(store.disabledMap['step1-b']).toBe(true);
    expect(store.statusMap['step1-b']).toBe('completed');
    expect(seed).toHaveBeenCalledOnce();
    app?.unmount();
    app = null;
    expect(store.statusMap).toEqual({});
    expect(store.steps).toEqual([]);
  });

  it.each(['config', 'hydration'])(
    'seeds saved state after %s arrives first',
    async (first) => {
      viewer.submissionId = 'saved-application';
      mountManager();
      if (first === 'config') resolveConfig(sidebarDtos);
      else viewer.emit?.('hydration-complete');
      await settle();
      expect(host.querySelector('.wiz-sidebar')).toBeNull();

      window.wizardSetStepStatus?.('step1-b', 'completed', 'step1');
      window.wizardSetStepClickable?.('step1-b', true, 'step1');
      window.wizardSetSubstepVisibility?.('step2-a', false, 'step1');
      viewer.emit?.('resume-wizard-state', {
        activeStep: 'step1',
        activeSubstep: 'step1-a',
        hiddenSteps: { step2: true },
        hiddenSubsteps: {},
        statusMap: { 'step1-a': 'completed', 'step1-b': 'error' },
        disabledMap: { 'step1-b': true, 'step2-a': true },
      });
      if (first === 'config') viewer.emit?.('hydration-complete');
      else resolveConfig(sidebarDtos);
      await settle();

      const store = useWizardStateStore(pinia);
      expect(store.activeSubstep).toBe('step1-a');
      expect(store.statusMap).toEqual({
        'step1-a': 'completed',
        'step1-b': 'completed',
      });
      expect(store.disabledMap['step1-b']).toBeUndefined();
      expect(store.disabledMap['step2-a']).toBe(true);
      expect(store.isSubstepHidden('step2-a')).toBe(true);
      expect(host.querySelectorAll('.wiz-sidebar')).toHaveLength(1);
    }
  );

  it('seeds before replaying navigation and does not replay it when mobile mounts', async () => {
    mountManager();
    window.wizardUpdateSidebar?.('step1-b');
    await enterWizard();
    expect(useWizardStateStore(pinia).activeSubstep).toBe('step1-b');
    viewer.emit?.('step-ready', 'step1');
    await settle();
    expect(useWizardStateStore(pinia).activeSubstep).toBe('step1-b');
    expect(document.querySelectorAll('.wiz-sidebar')).toHaveLength(2);
  });

  it('ignores config from an unmounted application while the next one initializes', async () => {
    mountManager();
    const resolveOldConfig = resolveConfig;
    app?.unmount();
    mountManager();
    window.wizardSetStepStatus?.('step1-b', 'completed');

    resolveOldConfig([
      sidebarDtos[0],
      { key: 'stale', title: 'Stale', order: 1, children: [] },
    ]);
    await settle();
    expect(useWizardStateStore(pinia).steps).toEqual([]);

    await enterWizard();
    expect(useWizardStateStore(pinia).statusMap['step1-b']).toBe('completed');
    expect(useWizardStateStore(pinia).steps.map((step) => step.key)).toEqual([
      'step1',
      'step2',
    ]);
  });

  it('keeps current bridge state and queued navigation through a survey round trip', async () => {
    mountManager();
    await enterWizard(true);
    window.wizardSetStepStatus?.('step1-b', 'completed');
    host.querySelector<HTMLButtonElement>('.wiz-survey-btn')?.click();
    await settle();
    expect(document.querySelectorAll('.wiz-sidebar')).toHaveLength(0);

    window.wizardSetStepStatus?.('step1-b', 'error');
    window.wizardSetStepClickable?.('step2-a', false);
    window.wizardUpdateSidebar?.('step1-b');
    viewer.emit?.('survey-complete');
    await settle();
    const store = useWizardStateStore(pinia);
    expect(document.querySelectorAll('.wiz-sidebar')).toHaveLength(2);
    expect(store.activeSubstep).toBe('step1-b');
    expect(store.statusMap['step1-b']).toBe('error');
    expect(store.disabledMap['step2-a']).toBe(true);
  });

  it('re-seeds late resume state without validating or saving a departure', async () => {
    const validate = vi.fn();
    const save = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('wizardValidateStep', validate);
    vi.stubGlobal('wizardSaveStep', save);
    mountManager();
    await enterWizard();

    viewer.emit?.('resume-wizard-state', {
      activeStep: 'step2',
      activeSubstep: 'step2-a',
      hiddenSteps: {},
      hiddenSubsteps: {},
      statusMap: { 'step1-a': 'completed', 'step2-a': 'completed' },
      disabledMap: {},
    });
    await settle();
    expect(useWizardStateStore(pinia).activeSubstep).toBe('step2-a');
    expect(useWizardStateStore(pinia).statusMap['step2-a']).toBe('completed');
    expect(validate).not.toHaveBeenCalled();
    expect(save).not.toHaveBeenCalled();
  });
});
