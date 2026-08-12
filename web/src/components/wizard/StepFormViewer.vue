<template>
  <div class="step-form-viewer" ref="viewerEl">
    <!--
      One slot per step. Each is created once on first activation and kept
      alive with v-show — navigating away does NOT destroy the instance.
    -->
    <template v-for="stepKey in props.stepKeys" :key="stepKey">
      <div v-if="initializedSteps.has(stepKey)" v-show="activeStep === stepKey">
        <!-- Per-step loading -->
        <div v-if="stepStates[stepKey] === 'loading'" class="step-form-loading">
          <div class="spinner-border text-primary" role="status">
            <span class="sr-only">Loading, please wait…</span>
          </div>
          <p class="mt-2 text-muted">Loading form…</p>
        </div>

        <!-- Per-step error -->
        <div
          v-else-if="stepStates[stepKey] === 'error'"
          class="alert alert-danger"
          role="alert"
        >
          <strong>Failed to load the form.</strong>
          {{ stepErrors[stepKey] }}
          <div class="mt-2">
            <button
              class="btn btn-sm btn-outline-danger"
              @click="initStep(stepKey)"
            >
              Retry
            </button>
          </div>
        </div>

        <!-- CHEFS form mount point — persists in DOM once created -->
        <div
          v-show="stepStates[stepKey] === 'ready'"
          :ref="(el) => setContainer(stepKey, el as HTMLElement | null)"
          class="chefs-form-viewer"
        ></div>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
  import chefsFormStylesUrl from '@/styles/chefs-form.scss?url';
  import ChefsService from '@/services/ChefsService';
  import CourtLocationService from '@/services/CourtLocationService';
  import ReportService from '@/services/ReportService';
  import { useWizardState } from '@/composables/useWizardState';
  import { useWizardDataStore } from '@/stores/WizardDataStore';
  import {
    inject,
    nextTick,
    onMounted,
    onUnmounted,
    reactive,
    ref,
    watch,
  } from 'vue';

  const {
    activeSubstep: wizardActiveSubstep,
    navState,
    statusMap,
    disabledMap,
  } = useWizardState();
  const wizardDataStore = useWizardDataStore();

  // ── Template ref ──────────────────────────────────────────────────────
  const viewerEl = ref<HTMLElement | null>(null);

  function scrollToTop() {
    nextTick(() => {
      viewerEl.value?.scrollIntoView({ behavior: 'auto', block: 'start' });
    });
  }

  // ── Props ─────────────────────────────────────────────────────────────────
  interface Props {
    /** The active step key, e.g. "step1", "step3". Controls which form is shown. */
    activeStep: string;
    /** Read-only mode – disables auto-save and submit. */
    readOnly?: boolean;
    /** Auto-save debounce in ms. 0 disables auto-save. Default: 3000. */
    autoSaveThrottle?: number;
    /**
     * Logical step keys that map to CHEFS forms on the backend, in display order.
     * Supplied from the backend sidebar structure (GET /api/Chefs/Sidebar).
     */
    stepKeys: string[];
    /** Raw submission_data JSON loaded from DB when resuming. */
    initialSubmissionData?: string | null;
    /** Logical key for the survey/pre-qualifying form step. */
    surveyStepKey?: string;
    /** Lookup from substep key to parent step key (and step->step identity). */
    substepToStepMap?: Record<string, string>;
    /** Public ID of the parent submission — required for step-based auto-save. */
    submissionPublicId?: string | null;
  }

  const props = withDefaults(defineProps<Props>(), {
    autoSaveThrottle: 3000,
    initialSubmissionData: null,
    surveyStepKey: '',
    substepToStepMap: () => ({}),
    submissionPublicId: null,
  });

  function getSurveyStepKey(): string {
    return props.surveyStepKey || props.stepKeys[0] || '';
  }

  function resolveParentStepKey(key: string): string | null {
    if (!key) return null;
    if (props.stepKeys.includes(key)) return key;
    return props.substepToStepMap[key] ?? null;
  }

  function isSurveyStep(stepKey: string): boolean {
    return stepKey === getSurveyStepKey();
  }

  // ── Emits ─────────────────────────────────────────────────────────────────
  const emit = defineEmits<{
    (e: 'submitted', stepKey: string, submissionId: string): void;
    (e: 'form-error', error: unknown): void;
    (e: 'saved', stepKey: string, submissionId: string): void;
    /** Fired once the step0 pre-qualifying survey sets data.showStepFunction = true. */
    (e: 'survey-complete'): void;
    /** Fired on first auto-save when no submission exists yet. Parent must create the submission. */
    (e: 'needs-submission', stepKey: string): void;
    /** Fired after hydration when a saved active substep is found. */
    (e: 'resume-substep', substepKey: string): void;
  }>();

  // ── Services ──────────────────────────────────────────────────────────────
  const chefsService = inject<ChefsService>('chefsService')!;
  const courtLocationService = inject<CourtLocationService>(
    'courtLocationService'
  );
  const reportService = inject<ReportService>('reportService')!;

  // Track generated blob URLs so they can be revoked on component teardown.
  const activeBlobUrls = new Set<string>();

  // ── Per-step state ────────────────────────────────────────────────────────
  type ViewState = 'loading' | 'ready' | 'error';

  /** Steps whose container div has been added to the DOM and init started. */
  const initializedSteps = reactive(new Set<string>());

  /** Loading/ready/error state per step. */
  const stepStates = reactive<Record<string, ViewState>>({});

  /** Error message per step. */
  const stepErrors = reactive<Record<string, string>>({});

  /** DOM container refs — populated by the :ref callback once the div mounts. */
  const containers: Record<string, HTMLElement | null> = {};

  /** CHEFS web-component element per step. */
  const stepEls: Record<string, any> = {};

  /**
   * Snapshot of each step's own field keys, captured in formio:ready BEFORE
   * we inject accumulated data from other steps. Used to filter what we store
   * back into WizardDataStore — so each step saves only its own fields, not
   * the cross-step data we injected for CHEFS form logic to read.
   */
  const stepOwnedKeys: Record<string, Set<string>> = {};

  /**
   * Keys injected from accumulated cross-step data at formio:ready time.
   * Used to distinguish true step-owned dynamic keys from injected keys.
   */
  const stepInjectedKeys: Record<string, Set<string>> = {};

  interface StepCaptureShape {
    containerKeys: Set<string>;
    containerFieldKeys: Record<string, Set<string>>;
  }
  const stepCaptureShapes: Record<string, StepCaptureShape> = {};

  /** Keys owned by wizard runtime. */
  const CAPTURE_EXCLUDED_KEYS = new Set<string>([
    'currentStep',
    'currentSubstep',
    'wizardStatusState',
    'wizardNavState',
    'wizardDisabledSteps',
    'showStepFunction',
    'showSurvey2',
    'animationDebounceScript',
  ]);

  /** True once hydrateFromPersistedPayload() has finished. */
  const hydrationDone = ref(false);

  /**
   * Suppressed until all initially-loaded forms have settled after hydration.
   * Prevents preloaded forms from auto-saving empty/default data that would
   * overwrite real persisted data on resume.
   */
  const autoSaveEnabled = ref(false);

  /** Per-step async plumbing (timers, save locks). */
  interface StepRuntime {
    readyTimer: ReturnType<typeof setTimeout> | null;
    debounceTimer: ReturnType<typeof setTimeout> | null;
    isSaving: boolean;
    pendingSave: boolean;
    formReady: boolean;
    /** True once the user has made a real change (after autoSaveEnabled). */
    dirty: boolean;
    submissionId: string | undefined;
  }
  const stepRuntime: Record<string, StepRuntime> = {};

  interface PersistedWizardPayload {
    version: number;
    wizard: {
      activeSubstep: string;
      navState: {
        hiddenSteps: Record<string, boolean>;
        hiddenSubsteps: Record<string, boolean>;
      };
      statusMap: Record<string, string>;
      disabledMap: Record<string, boolean>;
      stepSubmissionIds: Record<string, string>;
    };
    steps: Record<string, Record<string, any>>;
  }

  function buildPersistedPayload(): PersistedWizardPayload {
    const stepSubmissionIds: Record<string, string> = {};
    for (const key of Object.keys(stepRuntime)) {
      const id = stepRuntime[key]?.submissionId;
      if (id) stepSubmissionIds[key] = id;
    }

    return {
      version: 1,
      wizard: {
        activeSubstep: wizardActiveSubstep.value,
        navState: {
          hiddenSteps: { ...navState.hiddenSteps },
          hiddenSubsteps: { ...navState.hiddenSubsteps },
        },
        statusMap: { ...statusMap },
        disabledMap: { ...disabledMap },
        stepSubmissionIds,
      },
      steps: { ...wizardDataStore.stepData },
    };
  }

  async function hydrateFromPersistedPayload() {
    console.log(
      '[Hydrate] START — submissionPublicId:',
      props.submissionPublicId
    );
    // If we have a submission ID, try loading step data from the API first.
    if (props.submissionPublicId) {
      try {
        const allSteps = await chefsService.getAllStepData(
          props.submissionPublicId
        );
        console.log(
          '[Hydrate] API returned',
          allSteps.length,
          'steps:',
          allSteps.map((s) => s.formId)
        );
        let resumeSubstep: string | null = null;
        for (const step of allSteps) {
          if (step.data) {
            const parsed = JSON.parse(step.data);
            if (parsed && typeof parsed === 'object') {
              if (step.formId === '__wizard_state__') {
                resumeSubstep = parsed.activeSubstep ?? null;
                console.log(
                  '[Hydrate] Found wizard state — resumeSubstep:',
                  resumeSubstep
                );
                continue; // Don't store wizard metadata in the data store.
              }
              wizardDataStore.setStepData(step.formId, parsed);
              console.log(
                '[Hydrate] Stored data for',
                step.formId,
                '— keys:',
                Object.keys(parsed)
              );
            }
          }
        }
        console.log(
          '[Hydrate] Store accumulatedData keys after hydration:',
          Object.keys(wizardDataStore.accumulatedData)
        );
        if (resumeSubstep) {
          emit('resume-substep', resumeSubstep);
        }
        return; // Hydrated from API — skip legacy payload parsing.
      } catch (err) {
        console.warn(
          '[StepFormViewer] Failed to hydrate from step data API, falling back to payload:',
          err
        );
      }
    } else {
      console.log('[Hydrate] No submissionPublicId — skipping API hydration');
    }

    // Legacy fallback: hydrate from initialSubmissionData JSON blob.
    if (!props.initialSubmissionData) return;
    try {
      const parsed = JSON.parse(
        props.initialSubmissionData
      ) as Partial<PersistedWizardPayload>;
      const savedSteps = parsed?.steps;
      if (savedSteps && typeof savedSteps === 'object') {
        for (const [stepKey, data] of Object.entries(savedSteps)) {
          if (data && typeof data === 'object') {
            wizardDataStore.setStepData(stepKey, data);
          }
        }
      }

      const savedIds = parsed?.wizard?.stepSubmissionIds;
      if (savedIds && typeof savedIds === 'object') {
        for (const [stepKey, submissionId] of Object.entries(savedIds)) {
          if (!submissionId) continue;
          const rt = getRuntime(stepKey);
          rt.submissionId = submissionId;
        }
      }
    } catch {
      // Ignore malformed payloads and continue as a new in-memory session.
    }
  }

  interface PendingFocusRequest {
    substepKey: string;
    fieldKey: string;
  }

  const pendingFocus = ref<PendingFocusRequest | null>(null);
  let suppressAutoScrollUntil = 0;

  function shouldAutoScrollToTop(): boolean {
    if (pendingFocus.value) return false;
    return Date.now() > suppressAutoScrollUntil;
  }

  function getRuntime(stepKey: string): StepRuntime {
    if (!stepRuntime[stepKey]) {
      stepRuntime[stepKey] = {
        readyTimer: null,
        debounceTimer: null,
        isSaving: false,
        pendingSave: false,
        formReady: false,
        dirty: false,
        submissionId: undefined,
      };
    }
    return stepRuntime[stepKey];
  }

  function tryFocusField(substepKey: string, fieldKey: string): boolean {
    const parentStep = resolveParentStepKey(substepKey);
    if (!parentStep || parentStep !== props.activeStep) return false;

    const el = stepEls[parentStep];
    const formio = el?.formioInstance;
    if (!formio) return false;
    const searchRoot =
      (formio?.element as ParentNode | null) ||
      ((el as any)?.shadowRoot as ParentNode | null) ||
      (el as HTMLElement);

    const comp = formio.getComponent?.(fieldKey);

    if (comp) {
      const compEl = comp.element instanceof HTMLElement ? comp.element : null;
      if (compEl) {
        compEl.scrollIntoView({ behavior: 'smooth', block: 'center' });
      }
      if (typeof comp.focus === 'function') {
        try {
          comp.focus();
          return true;
        } catch {
          // Continue to DOM fallback below.
        }
      }
    }

    if (!searchRoot || typeof searchRoot.querySelector !== 'function')
      return false;

    const directMatch = searchRoot.querySelector(
      `.formio-component-${fieldKey}`
    );
    const namedMatch = searchRoot.querySelector(`[name="${fieldKey}"]`);
    const wrapper =
      (directMatch instanceof HTMLElement ? directMatch : null) ||
      (namedMatch instanceof HTMLElement
        ? ((namedMatch.closest(
            '.formio-component, .form-group, .formio-field'
          ) as HTMLElement | null) ?? namedMatch)
        : null);

    if (!wrapper) return false;

    wrapper.scrollIntoView({ behavior: 'smooth', block: 'center' });
    const target = wrapper.matches(
      'input, select, textarea, button, [role="radio"], [tabindex]'
    )
      ? wrapper
      : wrapper.querySelector<HTMLElement>(
          'input:not([type="hidden"]), select, textarea, button, [role="radio"], [tabindex]'
        );

    if (target instanceof HTMLElement) {
      target.focus();
    } else if (comp && typeof comp.focus === 'function') {
      try {
        comp.focus();
      } catch {
        wrapper.setAttribute('tabindex', '-1');
        wrapper.focus();
      }
    } else {
      wrapper.setAttribute('tabindex', '-1');
      wrapper.focus();
    }

    return true;
  }

  function applyPendingFocus() {
    const req = pendingFocus.value;
    if (!req) return;

    if (tryFocusField(req.substepKey, req.fieldKey)) {
      pendingFocus.value = null;
      // Keep top-scroll suppressed briefly so smooth focus-scroll wins.
      suppressAutoScrollUntil = Date.now() + 600;
    }
  }

  function requestFocusField(substepKey: string, fieldKey: string) {
    // Suppress automatic top-scroll while cross-step navigation settles.
    suppressAutoScrollUntil = Date.now() + 2000;
    pendingFocus.value = { substepKey, fieldKey };
  }

  function goToField(substepKey: string, fieldKey: string) {
    requestFocusField(substepKey, fieldKey);
    window.wizardUpdateSidebar?.(substepKey);
    nextTick(() => applyPendingFocus());
  }

  // substep validation — exposed as window.wizardValidateStep
  function validateSubstep(substepKey: string): boolean {
    const parentStep = resolveParentStepKey(substepKey);
    console.log(
      '[Validate]',
      substepKey,
      '— parentStep:',
      parentStep,
      '— activeStep:',
      props.activeStep
    );
    if (!parentStep || parentStep !== props.activeStep) return true;

    const el = stepEls[parentStep];
    const formio = el?.formioInstance;
    console.log(
      '[Validate]',
      substepKey,
      '— formio exists:',
      !!formio,
      '— checkValidity exists:',
      typeof formio?.checkValidity === 'function'
    );
    if (!formio || typeof formio.checkValidity !== 'function') return true;

    try {
      const data = formio.submission?.data ?? formio.data ?? {};
      const isValid = !!formio.checkValidity(data, true);
      console.log('[Validate]', substepKey, '— isValid:', isValid);
      window.wizardSetStepStatus?.(substepKey, isValid ? 'completed' : 'error');
      if (!isValid) {
        formio.once?.('change', () => {
          if (wizardActiveSubstep.value !== substepKey) return;
          const stillValid = !!formio.checkValidity(
            formio.submission?.data ?? formio.data ?? {},
            true
          );
          window.wizardSetStepStatus?.(
            substepKey,
            stillValid ? 'completed' : 'error'
          );
        });
      }

      return isValid;
    } catch {
      return true;
    }
  }

  // ── Container ref callback ─────────────────────────────────────────────────
  function setContainer(stepKey: string, el: HTMLElement | null) {
    containers[stepKey] = el;
  }

  function collectDescendantKeys(component: any, out: Set<string>): void {
    if (!component || typeof component !== 'object') return;
    if (typeof component.key === 'string' && component.key.length > 0) {
      out.add(component.key);
    }
    const children = Array.isArray(component.components)
      ? component.components
      : [];
    for (const child of children) collectDescendantKeys(child, out);
  }

  function deriveCaptureShape(formioInstance: any): StepCaptureShape {
    const containerKeys = new Set<string>();
    const containerFieldKeys: Record<string, Set<string>> = {};
    const rootComponents = Array.isArray(formioInstance?.component?.components)
      ? formioInstance.component.components
      : [];

    for (const comp of rootComponents) {
      if (
        comp?.type !== 'container' ||
        typeof comp?.key !== 'string' ||
        !comp.key
      )
        continue;
      containerKeys.add(comp.key);
      const keys = new Set<string>();
      const children = Array.isArray(comp.components) ? comp.components : [];
      for (const child of children) collectDescendantKeys(child, keys);
      keys.delete(comp.key);
      containerFieldKeys[comp.key] = keys;
    }

    return { containerKeys, containerFieldKeys };
  }

  // avoid mirroring nested container fields to top-level aliases.
  function normalizeContainerCapture(
    stepKey: string,
    ownData: Record<string, any>
  ): void {
    const shape = stepCaptureShapes[stepKey];
    if (!shape || shape.containerKeys.size === 0) return;

    for (const containerKey of shape.containerKeys) {
      const fieldKeys =
        shape.containerFieldKeys[containerKey] ?? new Set<string>();
      const containerObj =
        ownData[containerKey] && typeof ownData[containerKey] === 'object'
          ? ownData[containerKey]
          : {};

      for (const fieldKey of fieldKeys) {
        if (!Object.prototype.hasOwnProperty.call(ownData, fieldKey)) continue;

        const topValue = ownData[fieldKey];
        const nestedValue = containerObj[fieldKey];
        const nestedMissing =
          nestedValue === undefined ||
          nestedValue === null ||
          nestedValue === '';

        if (nestedMissing) containerObj[fieldKey] = topValue;
        delete ownData[fieldKey];
      }

      ownData[containerKey] = containerObj;
    }
  }

  // ── Script loader ─────────────────────────────────────────────────────────
  function injectChefsStyles(el: HTMLElement | null) {
    if (!el) return;
    const shadowRoot = (el as any).shadowRoot as ShadowRoot | null;
    if (
      !shadowRoot ||
      shadowRoot.querySelector('link[data-chefs-app-styles]')
    ) {
      return;
    }

    const link = document.createElement('link');
    link.setAttribute('data-chefs-app-styles', 'true');
    link.rel = 'stylesheet';
    link.href = chefsFormStylesUrl;
    shadowRoot.appendChild(link);
  }

  function loadWebComponentScript(baseUrl: string): Promise<void> {
    const scriptSrc = `${baseUrl}/embed/chefs-form-viewer.min.js`;
    if (
      document.querySelector<HTMLScriptElement>(`script[src="${scriptSrc}"]`)
    ) {
      return Promise.resolve();
    }
    return new Promise((resolve, reject) => {
      const script = document.createElement('script');
      script.src = scriptSrc;
      script.onload = () => resolve();
      script.onerror = () =>
        reject(new Error(`Failed to load CHEFS script from ${scriptSrc}`));
      document.head.appendChild(script);
    });
  }

  // ── Initialize a single step's form ───────────────────────────────────────
  async function initStep(stepKey: string) {
    teardownStep(stepKey);
    stepStates[stepKey] = 'loading';
    stepErrors[stepKey] = '';

    const rt = getRuntime(stepKey);
    rt.formReady = false;

    try {
      // Fetch the short-lived gateway JWT + resolved form GUID from the backend.
      const { token, formId, baseUrl } =
        await chefsService.getAuthToken(stepKey);

      await loadWebComponentScript(baseUrl);

      // Step schemas call window.courtLocationService.
      if (courtLocationService) {
        window.courtLocationService = courtLocationService;
      }

      await nextTick();

      const container = containers[stepKey];
      if (!container) throw new Error('Mount point not found');

      container.innerHTML = '';

      window.staticBaseUrl = `${window.location.origin}${import.meta.env.BASE_URL}`;

      const el = document.createElement('chefs-form-viewer') as any;
      el.setAttribute('form-id', formId);
      el.setAttribute('auth-token', token);
      el.setAttribute('base-url', baseUrl);
      el.setAttribute('isolate-styles', 'false');

      if (rt.submissionId) {
        el.setAttribute('submission-id', rt.submissionId);
        el.setAttribute('read-only', props.readOnly ? 'true' : 'false');
      }

      container.appendChild(el);
      stepEls[stepKey] = el;

      el.addEventListener('formio:submitDone', (e: CustomEvent) =>
        handleSubmitDone(stepKey, e)
      );
      el.addEventListener('formio:error', (e: CustomEvent) => {
        const currentSubstep = wizardActiveSubstep.value;
        if (resolveParentStepKey(currentSubstep) === stepKey) {
          window.wizardSetStepStatus?.(currentSubstep, 'error');
        }
        emit('form-error', e.detail);
      });

      await customElements.whenDefined('chefs-form-viewer');

      rt.readyTimer = setTimeout(() => {
        stepStates[stepKey] = 'ready';
      }, 15000);

      el.addEventListener(
        'formio:ready',
        () => {
          injectChefsStyles(el);
          if (rt.readyTimer) {
            clearTimeout(rt.readyTimer);
            rt.readyTimer = null;
          }
          rt.readyTimer = setTimeout(() => {
            rt.readyTimer = null;
            stepStates[stepKey] = 'ready';
          }, 500);

          // Set data.currentStep and data.currentSubstep so CHEFS onChange guards work.
          try {
            if (el.formioInstance?.data !== undefined) {
              const accumulated = wizardDataStore.accumulatedData;

              // On cached form loads, formio:ready can fire before the
              // component tree has built its default data object.  In that
              // case we defer injection until the data object is populated.
              const injectData = () => {
                console.log(
                  '[InjectData]',
                  stepKey,
                  '— formio.data keys:',
                  Object.keys(el.formioInstance.data)
                );
                console.log(
                  '[InjectData]',
                  stepKey,
                  '— accumulated keys:',
                  Object.keys(accumulated)
                );

                stepCaptureShapes[stepKey] = deriveCaptureShape(
                  el.formioInstance
                );

                // Snapshot own keys BEFORE injecting cross-step data.
                stepOwnedKeys[stepKey] = new Set(
                  Object.keys(el.formioInstance.data)
                );

                // Build injectedKeys from OTHER steps only.  If we include
                // the current step's own stored keys, captureStepData will
                // treat conditional / late-appearing fields as "injected"
                // and skip them — causing data loss on auto-save.
                const otherStepKeys = new Set<string>();
                for (const [sKey, sData] of Object.entries(
                  wizardDataStore.stepData
                )) {
                  if (sKey === stepKey) continue;
                  for (const k of Object.keys(sData)) {
                    if (!k.startsWith('_')) otherStepKeys.add(k);
                  }
                }
                stepInjectedKeys[stepKey] = otherStepKeys;
                console.log(
                  '[InjectData]',
                  stepKey,
                  '— injectedKeys (other-step only):',
                  [...otherStepKeys]
                );

                if (Object.keys(accumulated).length > 0) {
                  Object.assign(el.formioInstance.data, accumulated);
                }
                el.formioInstance.data.currentStep = stepKey;
                el.formioInstance.data.currentSubstep =
                  wizardActiveSubstep.value;

                console.log(
                  '[InjectData]',
                  stepKey,
                  '— formio.data AFTER inject:',
                  JSON.stringify(el.formioInstance.data).substring(0, 500)
                );

                el.formioInstance.triggerChange?.();
                el.formioInstance.redraw?.();
              };

              if (Object.keys(el.formioInstance.data).length === 0) {
                // Form component tree not ready yet — wait for the first
                // change event which signals that default data is populated.
                console.log(
                  '[FormReady]',
                  stepKey,
                  '— formio.data is EMPTY, deferring injection'
                );
                const onceChange = () => {
                  el.formioInstance?.off?.('change', onceChange);
                  console.log(
                    '[FormReady]',
                    stepKey,
                    '— deferred injection firing (change event received)'
                  );
                  injectData();
                };
                el.formioInstance.on?.('change', onceChange);
              } else {
                injectData();
              }
            }
          } catch {
            /* ignore — non-critical */
          }

          // pre-qualifying survey schema flips data.showStepFunction = true on its final "Next" button to signal the host app should move
          if (isSurveyStep(stepKey)) {
            try {
              el.formioInstance?.on?.('change', () => {
                if (el.formioInstance?.data?.showStepFunction === true) {
                  emit('survey-complete');
                }
              });
            } catch {
              /* ignore — non-critical */
            }
          }

          applyPendingFocus();
        },
        { once: true }
      );

      el.load();

      // Capture this step's own data into the shared store on every change.
      el.addEventListener('formio:change', (e: CustomEvent) => {
        // Only capture and auto-save once the hydration grace period has
        // elapsed.  During form initialisation the component fires change
        // events with empty / default data — writing those back into
        // wizardDataStore would overwrite the correctly hydrated values
        // that were loaded from the API.
        if (rt.formReady && autoSaveEnabled.value) {
          console.log(
            '[Change]',
            stepKey,
            '— CAPTURING + scheduling auto-save (formReady + autoSaveEnabled)'
          );
          try {
            captureStepData(stepKey, e.detail);
          } catch {
            /* ignore */
          }
          rt.dirty = true;
          scheduleAutoSave(stepKey);
        } else {
          console.log(
            '[Change]',
            stepKey,
            '— SKIPPED capture (formReady:',
            rt.formReady,
            'autoSaveEnabled:',
            autoSaveEnabled.value,
            ')'
          );
        }
        if (
          isSurveyStep(stepKey) &&
          el.formioInstance?.data?.showStepFunction === true
        ) {
          emit('survey-complete');
        }
      });

      if (props.autoSaveThrottle > 0 && !props.readOnly) {
        setTimeout(() => {
          rt.formReady = true;
        }, 2000);
      }
    } catch (err: any) {
      stepErrors[stepKey] =
        err?.response?.data?.message ?? err?.message ?? 'Unknown error.';
      stepStates[stepKey] = 'error';
    }
  }

  // ── Capture a step's own data into the store ───────────────────────────────
  function captureStepData(stepKey: string, detail?: any) {
    const el = stepEls[stepKey];
    if (!el?.formioInstance?.data) return;

    // Prefer Form.io canonical change payload when present.
    const sourceData =
      detail?.data && typeof detail.data === 'object'
        ? (detail.data as Record<string, any>)
        : (el.formioInstance.data as Record<string, any>);

    const ownKeys = stepOwnedKeys[stepKey];
    const injectedKeys = stepInjectedKeys[stepKey] ?? new Set<string>();
    if (ownKeys) {
      const ownData: Record<string, any> = {};
      for (const [key, value] of Object.entries(sourceData)) {
        if (CAPTURE_EXCLUDED_KEYS.has(key) || key.startsWith('_')) continue;

        if (ownKeys.has(key)) {
          ownData[key] = value;
          continue;
        }

        // Dynamic field created after ready
        if (!injectedKeys.has(key)) {
          ownKeys.add(key);
          ownData[key] = value;
        }
      }

      normalizeContainerCapture(stepKey, ownData);

      wizardDataStore.setStepData(stepKey, ownData);
    } else {
      // Snapshot not yet available (change fired before ready — unlikely).
      const fallback = { ...sourceData };
      normalizeContainerCapture(stepKey, fallback);
      wizardDataStore.setStepData(stepKey, fallback);
    }
  }

  // ── Sync cross-step data into an already-loaded step ────────────────────────
  // Called whenever the user navigates back to a step they've already visited.
  // Updates only the injected (non-owned) fields so shared data like deceasedName
  // reflects any changes made in other steps since the form was first loaded.
  function syncCrossStepData(stepKey: string) {
    const el = stepEls[stepKey];
    const ownKeys = stepOwnedKeys[stepKey];
    if (!el?.formioInstance?.data || !ownKeys) return;
    try {
      const accumulated = wizardDataStore.accumulatedData;
      const injectedKeys =
        stepInjectedKeys[stepKey] ?? (stepInjectedKeys[stepKey] = new Set());
      let hasChanges = false;
      for (const [key, value] of Object.entries(accumulated)) {
        if (!ownKeys.has(key) && el.formioInstance.data[key] !== value) {
          el.formioInstance.data[key] = value;
          // Keep late-synced keys marked as injected so captureStepData does not
          // treat them as dynamic step-owned fields on future changes.
          injectedKeys.add(key);
          hasChanges = true;
        }
      }
      if (!hasChanges) return;
      // Re-run Form.io logic triggers so review-table scripts recompute using
      // freshly synced cross-step data.
      el.formioInstance.triggerChange?.();
      // redraw() re-renders all components including fieldset legends, panel titles,
      // and HTML content components that use {{ data.xxx }} template expressions.
      // triggerChange() alone only recalculates computed values, not static DOM.
      el.formioInstance.redraw?.();
    } catch {
      /* ignore — non-critical */
    }
  }

  // ── Activate a step (init on first visit, show on subsequent visits) ───────
  async function activateStep(stepKey: string) {
    console.log(
      '[ActivateStep]',
      stepKey,
      '— stepKeys includes:',
      props.stepKeys.includes(stepKey),
      '— hydrationDone:',
      hydrationDone.value,
      '— alreadyInitialized:',
      initializedSteps.has(stepKey)
    );
    if (!props.stepKeys.includes(stepKey)) return;

    // Wait for hydration to finish so forms are seeded with persisted data.
    if (!hydrationDone.value) {
      console.log('[ActivateStep]', stepKey, '— WAITING for hydrationDone');
      const stop = watch(hydrationDone, (done) => {
        if (done) {
          stop();
          activateStep(stepKey);
        }
      });
      return;
    }

    if (!initializedSteps.has(stepKey)) {
      // Add to set first so Vue renders the container div, then init
      initializedSteps.add(stepKey);
      await initStep(stepKey);
    } else {
      // Already initialized — sync any cross-step fields that changed since last visit.
      syncCrossStepData(stepKey);
    }
  }

  // ── Background preloading ────────────────────────────────────────────────
  // Warm up forms so first navigation to each step does not wait on bootstrap.
  // This is intentionally reactive: stepKeys can arrive after mount.
  let isBackgroundPreloading = false;

  async function preloadStepsInBackground() {
    if (isBackgroundPreloading) return;
    // Wait for hydration so forms are seeded with persisted data.
    if (!hydrationDone.value) return;
    isBackgroundPreloading = true;

    try {
      await nextTick();

      for (const stepKey of props.stepKeys) {
        if (isSurveyStep(stepKey)) continue;
        if (stepKey === props.activeStep) continue;
        if (initializedSteps.has(stepKey)) continue;

        initializedSteps.add(stepKey);
        await initStep(stepKey);
      }
    } finally {
      isBackgroundPreloading = false;

      // If keys changed while we were preloading, run one more pass.
      const hasPending = props.stepKeys.some(
        (k) =>
          !isSurveyStep(k) && k !== props.activeStep && !initializedSteps.has(k)
      );
      if (hasPending) {
        void preloadStepsInBackground();
      }
    }
  }

  // ── Submit handler ────────────────────────────────────────────────────────
  function handleSubmitDone(stepKey: string, e: CustomEvent) {
    const submission = e.detail?.submission;
    const newId: string | undefined = submission?.id ?? submission?._id;
    if (!newId) return;
    const rt = getRuntime(stepKey);
    rt.submissionId = newId;

    captureStepData(stepKey);

    emit('saved', stepKey, newId);
    emit('submitted', stepKey, newId);
  }

  // ── Auto-save ─────────────────────────────────────────────────────────────
  function scheduleAutoSave(stepKey: string) {
    const rt = getRuntime(stepKey);
    if (rt.isSaving) {
      rt.pendingSave = true;
      return;
    }
    if (rt.debounceTimer) clearTimeout(rt.debounceTimer);
    rt.debounceTimer = setTimeout(
      () => performAutoSave(stepKey),
      props.autoSaveThrottle
    );
  }

  async function performAutoSave(stepKey: string) {
    const rt = getRuntime(stepKey);
    // Only save steps the user has actually modified.
    if (!rt.dirty) {
      console.log('[AutoSave]', stepKey, '— SKIPPED (not dirty)');
      return;
    }
    console.log('[AutoSave]', stepKey, '— SAVING (dirty=true)');
    if (rt.isSaving) {
      rt.pendingSave = true;
      return;
    }

    rt.isSaving = true;
    rt.pendingSave = false;

    try {
      captureStepData(stepKey);

      if (!props.submissionPublicId) {
        // No submission yet — ask the parent to create one.
        // The parent will set submissionPublicId, triggering a re-save.
        emit('needs-submission', stepKey);
      } else {
        // Persist to DB via step data endpoint.
        const stepPayload = wizardDataStore.getStepData(stepKey);
        await chefsService.upsertStepData(props.submissionPublicId, stepKey, {
          formId: stepKey,
          data: JSON.stringify(stepPayload),
        });
        emit('saved', stepKey, props.submissionPublicId);
      }
    } catch (err) {
      console.warn('[StepFormViewer] autosave failed:', err);
    } finally {
      rt.isSaving = false;
      if (rt.pendingSave) {
        rt.pendingSave = false;
        scheduleAutoSave(stepKey);
      }
    }
  }

  // Immediately save a step's data, bypassing the auto-save debounce — used
  // when navigating away (Next/Previous/direct sidebar click)
  function flushSaveStep(substepKey: string) {
    const parentStep = resolveParentStepKey(substepKey);
    if (!parentStep) return;
    const rt = stepRuntime[parentStep];
    if (rt?.debounceTimer) {
      clearTimeout(rt.debounceTimer);
      rt.debounceTimer = null;
    }
    performAutoSave(parentStep);
  }

  // ── Per-step teardown ─────────────────────────────────────────────────────
  function teardownStep(stepKey: string) {
    const rt = stepRuntime[stepKey];
    if (!rt) return;
    if (rt.readyTimer) {
      clearTimeout(rt.readyTimer);
      rt.readyTimer = null;
    }
    if (rt.debounceTimer) {
      clearTimeout(rt.debounceTimer);
      rt.debounceTimer = null;
    }
    rt.isSaving = false;
    rt.pendingSave = false;
  }

  // ── Watch active step ─────────────────────────────────────────────────────
  // On navigation away, do a final capture of the departing step's data.
  // This catches calculated values (e.g. deceasedDateOfDeathPlus4) that Form.io
  // computes asynchronously via JS triggers — they may settle AFTER the last
  // formio:change event fires, so formio:change alone can miss them.
  watch(
    () => props.activeStep,
    (newStep, oldStep) => {
      if (oldStep && oldStep !== newStep && autoSaveEnabled.value) {
        captureStepData(oldStep);
      }
      activateStep(newStep);
      if (isSurveyStep(newStep) && oldStep && oldStep !== newStep) {
        const el = stepEls[getSurveyStepKey()];
        try {
          if (el?.formioInstance?.data?.showSurvey2 === 'hide') {
            el.formioInstance.data.showSurvey2 = '';
            el.formioInstance.data.showStepFunction = false;
            el.formioInstance.triggerChange?.();
            el.formioInstance.redraw?.();
          }
        } catch {
          /* ignore — non-critical */
        }
      }

      if (oldStep && oldStep !== newStep && shouldAutoScrollToTop()) {
        scrollToTop();
      }
      nextTick(() => applyPendingFocus());
    },
    { immediate: true }
  );

  // ── Sync data.currentSubstep when substep changes within the same step ────
  // Needed for multi-panel forms (e.g. step3 with spouse/children/creditors)
  watch(wizardActiveSubstep, (substep) => {
    const parentStep = resolveParentStepKey(substep);
    if (!parentStep || parentStep !== props.activeStep) return;
    const el = stepEls[parentStep];
    try {
      if (el?.formioInstance?.data !== undefined) {
        // Let Form.io own visibility/state transitions for substep changes.
        el.formioInstance.data.currentSubstep = substep;
        el.formioInstance.triggerChange?.();
        if (shouldAutoScrollToTop()) {
          scrollToTop();
        }
        nextTick(() => applyPendingFocus());
      }
    } catch {
      /* ignore */
    }
  });

  // Start/refresh background preloading once the survey step is ready (or if
  // the user is already beyond the survey), and whenever the backend step list changes.
  watch(
    [
      () => stepStates[getSurveyStepKey()],
      () => props.stepKeys.join('|'),
      () => props.activeStep,
      hydrationDone,
    ],
    ([surveyState, stepKeysSig, activeStep]) => {
      if (!stepKeysSig) return;
      const hasWizardSteps = props.stepKeys.some((k) => !isSurveyStep(k));
      if (!hasWizardSteps) return;

      const canPreload = !isSurveyStep(activeStep) || surveyState === 'ready';
      if (!canPreload) return;

      void preloadStepsInBackground();
    },
    { immediate: true }
  );

  // ── Validate all steps ─────────────────────────────────────────────────────
  function validateAllSteps(): { valid: boolean; failedSteps: string[] } {
    const failedSteps: string[] = [];

    for (const stepKey of props.stepKeys) {
      if (isSurveyStep(stepKey)) continue;

      const el = stepEls[stepKey];
      const formio = el?.formioInstance;
      if (!formio || typeof formio.checkValidity !== 'function') continue;

      try {
        const data = formio.submission?.data ?? formio.data ?? {};
        const isValid = !!formio.checkValidity(data, true);
        if (!isValid) {
          failedSteps.push(stepKey);
        }
      } catch {
        failedSteps.push(stepKey);
      }
    }

    return { valid: failedSteps.length === 0, failedSteps };
  }

  /** Immediately persist every step that has user-entered data. */
  async function flushAllSteps() {
    console.log('[FlushAll] START — runtime keys:', Object.keys(stepRuntime));
    const saves: Promise<void>[] = [];
    for (const stepKey of Object.keys(stepRuntime)) {
      const rt = stepRuntime[stepKey];
      if (!rt) continue;
      if (rt.debounceTimer) {
        clearTimeout(rt.debounceTimer);
        rt.debounceTimer = null;
      }
      // Only save steps that have meaningful data in the store.
      const data = wizardDataStore.getStepData(stepKey);
      console.log(
        '[FlushAll]',
        stepKey,
        '— dirty:',
        rt.dirty,
        '— storeDataKeys:',
        Object.keys(data).length
      );
      if (Object.keys(data).length === 0) continue;
      saves.push(performAutoSave(stepKey));
    }
    // Also persist the current active substep so resume can restore position.
    if (props.submissionPublicId && wizardActiveSubstep.value) {
      try {
        await chefsService.upsertStepData(
          props.submissionPublicId,
          '__wizard_state__',
          {
            formId: '__wizard_state__',
            data: JSON.stringify({
              activeSubstep: wizardActiveSubstep.value,
            }),
          }
        );
      } catch {
        // Best-effort — don't block navigation if this fails.
      }
    }

    await Promise.allSettled(saves);
  }

  defineExpose({ validateAllSteps, flushAllSteps });

  // ── Lifecycle ───────────────────────────────────────────────────────────────
  onMounted(async () => {
    console.log(
      '[Mount] StepFormViewer onMounted — submissionPublicId:',
      props.submissionPublicId,
      '— activeStep:',
      props.activeStep,
      '— stepKeys:',
      props.stepKeys
    );
    await hydrateFromPersistedPayload();
    hydrationDone.value = true;
    // Allow forms to settle after hydration before enabling auto-save.
    // This prevents preloaded forms from overwriting persisted data
    // with empty/default values during their initial formio:change events.
    setTimeout(() => {
      autoSaveEnabled.value = true;
    }, 5000);
    window.wizardGoToField = goToField;
    window.wizardValidateStep = validateSubstep;
    window.wizardSaveStep = flushSaveStep;
    window.wizardFlushAll = flushAllSteps;
    window.wizardGetPersistedPayload = () => buildPersistedPayload();

    // Bridge expected by CHEFS schema scripts in preview/print substeps.
    window.probate = {
      generatePdf: async (
        templateKey: string,
        submissionData: unknown
      ): Promise<string> => {
        const { url } = await reportService.generateReport({
          templateKey,
          submissionData,
        });
        activeBlobUrls.add(url);
        return url;
      },
      downloadPdf: async (
        instance: any,
        data: unknown,
        templateKey: string,
        iframeTitle: string,
        fileName: string
      ): Promise<void> => {
        const root = instance.root;
        const rootEl = root?.element ?? document;
        const frame: HTMLIFrameElement | null = rootEl.querySelector(
          `iframe[title='${iframeTitle}']`
        );

        let url = frame?.getAttribute('src') ?? '';
        let freshUrl = false;
        if (!url || url === 'about:blank' || !url.startsWith('blob:')) {
          const result = await reportService.generateReport({
            templateKey,
            submissionData: data,
          });
          url = result.url;
          freshUrl = true;
        }

        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        a.click();

        if (freshUrl) {
          URL.revokeObjectURL(url);
        }
      },
      previewPdf: async (
        instance: any,
        data: unknown,
        currentStep: string,
        targetStep: string,
        templateKey: string,
        iframeTitle: string
      ): Promise<void> => {
        if (currentStep !== targetStep) return;

        const root = instance.root;
        const rootEl = root?.element ?? document;
        const frame: HTMLIFrameElement | null = rootEl.querySelector(
          `iframe[title='${iframeTitle}']`
        );

        if (!frame) return;

        const oldSrc = frame.getAttribute('src') ?? '';
        if (oldSrc.startsWith('blob:')) {
          URL.revokeObjectURL(oldSrc);
          activeBlobUrls.delete(oldSrc);
        }

        const { url } = await reportService.generateReport({
          templateKey,
          submissionData: data,
        });
        activeBlobUrls.add(url);
        frame.setAttribute('src', url);
        frame.setAttribute('data-pdf-loaded', 'true');
      },
    };
  });

  onUnmounted(() => {
    pendingFocus.value = null;

    delete window.wizardGoToField;
    delete window.wizardValidateStep;
    delete window.wizardSaveStep;
    delete window.wizardFlushAll;
    delete window.wizardGetPersistedPayload;

    activeBlobUrls.forEach((url) => URL.revokeObjectURL(url));
    activeBlobUrls.clear();

    delete window.probate;

    for (const stepKey of Object.keys(stepRuntime)) {
      teardownStep(stepKey);
    }
  });
</script>

<style scoped>
  .step-form-viewer {
    width: 100%;
    max-width: 100%;
    /* Space for sticky app header so scrollIntoView lands below it */
    scroll-margin-top: 80px;
  }

  .step-form-loading {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 3rem;
  }

  .chefs-form-viewer {
    display: block;
    width: 100%;
    max-width: 100%;
  }

  .sr-only {
    position: absolute;
    width: 1px;
    height: 1px;
    padding: 0;
    margin: -1px;
    overflow: hidden;
    clip: rect(0, 0, 0, 0);
    white-space: nowrap;
    border: 0;
  }
</style>
