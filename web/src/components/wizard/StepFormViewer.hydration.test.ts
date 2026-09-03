import { initWizardState } from '@/composables/useWizardState';
import { useWizardDataStore } from '@/stores/WizardDataStore';
import { createPinia, setActivePinia } from 'pinia';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createApp, nextTick } from 'vue';
import StepFormViewer from './StepFormViewer.vue';

/**
 * Minimal stand-in for form.io's Webform instance, exposing just enough of
 * the on/off/emit + data contract that StepFormViewer's injectData()/
 * syncCrossStepData() logic depends on.
 */
class FakeFormioInstance {
  data: Record<string, any> = {};
  private listeners: Record<string, Array<(detail?: any) => void>> = {};
  on(event: string, cb: (detail?: any) => void) {
    (this.listeners[event] ??= []).push(cb);
  }
  off(event: string, cb: (detail?: any) => void) {
    this.listeners[event] = (this.listeners[event] ?? []).filter(
      (f) => f !== cb
    );
  }
  emit(event: string, detail?: any) {
    [...(this.listeners[event] ?? [])].forEach((cb) => cb(detail));
  }
  triggerChange() {
    this.emit('change', { data: this.data });
  }
  redraw() {
    /* no-op */
  }
  checkValidity() {
    return true;
  }
}

/**
 * Fake <chefs-form-viewer> custom element. Unlike the real vendor element,
 * `load()` is a no-op — the test drives `formio:ready` / formio `change`
 * events manually so the exact race being tested is fully deterministic.
 */
class FakeChefsFormViewer extends HTMLElement {
  formioInstance = new FakeFormioInstance();
  load() {
    /* no-op — test drives readiness manually */
  }
}

if (!customElements.get('chefs-form-viewer')) {
  customElements.define('chefs-form-viewer', FakeChefsFormViewer);
}

/**
 * happy-dom throws synchronously ("JavaScript file loading is disabled")
 * when a real <script src> is appended to the document, so
 * loadWebComponentScript()'s own script tag can never load in tests.
 * Swap document.createElement('script') for a plain element that fires
 * whatever `.onload` StepFormViewer assigns as soon as `.src` is set —
 * the custom element itself is already registered above (matching what
 * the real vendor script does as a side effect of loading), so this is
 * enough to unblock initStep()'s `await loadWebComponentScript(...)`.
 */
function stubOutChefsScriptLoading(): void {
  const realCreateElement = document.createElement.bind(document);
  vi.spyOn(document, 'createElement').mockImplementation(
    (tagName: string, options?: ElementCreationOptions) => {
      if (tagName.toLowerCase() !== 'script') {
        return realCreateElement(tagName, options);
      }
      const fake = realCreateElement('div') as unknown as HTMLScriptElement;
      Object.defineProperty(fake, 'src', {
        configurable: true,
        set() {
          queueMicrotask(() => {
            (fake.onload as (() => void) | null)?.();
          });
        },
        get() {
          return '';
        },
      });
      return fake;
    }
  );
}

function makeChefsService(getAllStepData: () => Promise<any[]>) {
  return {
    getAuthToken: vi.fn(async (formKey: string) => ({
      token: 'tok',
      formId: `formid-${formKey}`,
      baseUrl: 'http://fake-chefs.local',
    })),
    getAllStepData: vi.fn(getAllStepData),
  };
}

describe('StepFormViewer cross-step hydration on resume', () => {
  let host: HTMLDivElement;
  let app: ReturnType<typeof createApp> | null = null;

  beforeEach(() => {
    setActivePinia(createPinia());
    initWizardState(
      'form-filing',
      { hiddenSteps: {}, hiddenSubsteps: {} },
      {},
      {}
    );
    stubOutChefsScriptLoading();
    host = document.createElement('div');
    document.body.appendChild(host);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    app?.unmount();
    host.remove();
    app = null;
  });

  function mountViewer(chefsService: unknown) {
    app = createApp(StepFormViewer, {
      activeStep: 'form-filing',
      stepKeys: ['survey', 'form-applicant', 'form-filing'],
      surveyStepKey: 'survey',
      submissionPublicId: 'sub-123',
    });
    app.config.warnHandler = () => {};
    app.provide('chefsService', chefsService);
    app.provide('reportService', {});
    app.provide('courtLocationService', undefined);
    app.mount(host);
  }

  async function getFakeFilingElement(): Promise<FakeChefsFormViewer> {
    for (let i = 0; i < 50; i++) {
      const el = host.querySelector('chefs-form-viewer');
      if (el) return el as FakeChefsFormViewer;
      await nextTick();
      await Promise.resolve();
    }
    throw new Error('chefs-form-viewer element was never created');
  }

  it('injects a courthouse name loaded from another step on the very first render (no revisit needed)', async () => {
    // Resume flow: the DB already has form-applicant's data by the time
    // getAllStepData resolves — this is the exact scenario reported: land
    // straight on form-filing (step 8) and see the courthouse name without
    // navigating away and back.
    const chefsService = makeChefsService(async () => [
      {
        formId: 'form-applicant',
        data: JSON.stringify({
          applicant: {
            applicantCourthouse: 6011,
            applicantCourthouseName: 'Vancouver Law Courts',
          },
        }),
        updatedAt: '2026-01-01T00:00:00Z',
        createdAt: '2026-01-01T00:00:00Z',
      },
    ]);

    mountViewer(chefsService);

    const el = await getFakeFilingElement();
    expect(el.formioInstance.data).toEqual({});

    // form.io fires formio:ready before its component tree has built the
    // default data object on a cached load — data is still empty here.
    el.dispatchEvent(new CustomEvent('formio:ready'));

    // form.io then populates its own default fields and fires a change
    // event once the data object exists.
    el.formioInstance.data.someFilingOwnField = '';
    el.formioInstance.emit('change', { data: el.formioInstance.data });
    await nextTick();

    expect(el.formioInstance.data.applicantCourthouseName).toBe(
      'Vancouver Law Courts'
    );
    expect(el.formioInstance.data.applicantCourthouse).toBe(6011);
  });

  it('injects data that only lands in the store between formio:ready and the deferred change event', async () => {
    // Regression guard for the stale-snapshot bug: injectData() must read
    // wizardDataStore.accumulatedData fresh at change-time, not capture it
    // before waiting for the deferred change event.
    const chefsService = makeChefsService(async () => []);

    mountViewer(chefsService);

    const el = await getFakeFilingElement();
    el.dispatchEvent(new CustomEvent('formio:ready'));
    await nextTick();

    // Simulate data arriving (e.g. a slower parallel hydration source)
    // after formio:ready fired but before form.io's default-data change
    // event — the window the old, stale-captured `accumulated` snapshot
    // would have missed.
    const wizardDataStore = useWizardDataStore();
    wizardDataStore.setStepData('form-applicant', {
      applicant: {
        applicantCourthouse: 6011,
        applicantCourthouseName: 'Vancouver Law Courts',
      },
    });

    el.formioInstance.data.someFilingOwnField = '';
    el.formioInstance.emit('change', { data: el.formioInstance.data });
    await nextTick();

    expect(el.formioInstance.data.applicantCourthouseName).toBe(
      'Vancouver Law Courts'
    );
  });

  it('injects data even when the form never fires a change event (e.g. a static step with no input components)', async () => {
    // Regression guard: schemas with no input components (e.g. the
    // instructional-only Filing Instructions step) never fire a 'change'
    // event at all, since form.io has no default field values to set.
    // injectData() must not wait for 'change' forever in that case.
    vi.useFakeTimers();
    try {
      const chefsService = makeChefsService(async () => [
        {
          formId: 'form-applicant',
          data: JSON.stringify({
            applicant: {
              applicantCourthouse: 6011,
              applicantCourthouseName: 'Vancouver Law Courts',
            },
          }),
          updatedAt: '2026-01-01T00:00:00Z',
          createdAt: '2026-01-01T00:00:00Z',
        },
      ]);

      mountViewer(chefsService);

      const el = await getFakeFilingElement();
      el.dispatchEvent(new CustomEvent('formio:ready'));
      expect(el.formioInstance.data).toEqual({});

      // No 'change' event ever fires — only the bounded fallback timer
      // should trigger injection.
      await vi.advanceTimersByTimeAsync(600);

      expect(el.formioInstance.data.applicantCourthouseName).toBe(
        'Vancouver Law Courts'
      );
    } finally {
      vi.useRealTimers();
    }
  });
});
