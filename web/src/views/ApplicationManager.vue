<template>
  <div class="wizard-preview-layout">
    <!-- Desktop sidebar -->
    <div
      v-if="activeStep && !isSurveyStep"
      v-show="isActiveStepLoaded"
      class="wizard-col d-none d-lg-block"
    >
      <ApplicationStepSidebar
        ref="sidebarRef"
        :steps="wizardSteps"
        :initial-step="wizardStateStore.activeSubstep"
        :survey-step-key="surveyStepKey"
        @navigate="onNavigate"
      />
    </div>

    <div class="content-col">
      <!-- CHEFS form viewer-->
      <StepFormViewer
        ref="stepFormViewerRef"
        :active-step="activeStepKey"
        :step-keys="allStepKeys"
        :survey-step-key="surveyStepKey"
        :substep-to-step-map="substepToStepMap"
        :submission-public-id="submissionPublicId"
        @survey-complete="onSurveyComplete"
        @saved="onStepSaved"
        @needs-submission="onNeedsSubmission"
        @resume-wizard-state="onResumeWizardState"
        @hydration-complete="onHydrationComplete"
        @step-ready="onStepReady"
      />

      <!-- Dev controls -->
      <div v-show="showDevControls" class="dev-panel mt-3">
        <!-- Simulate unlocking steps to test nav -->
        <div class="controls mt-4">
          <p class="fw-bold mb-2">Dev controls (preview only)</p>
          <div class="d-flex flex-wrap gap-2">
            <button class="btn btn-sm btn-outline-secondary" @click="unlockAll">
              Unlock all steps
            </button>
            <button class="btn btn-sm btn-outline-secondary" @click="lockAll">
              Reset to initial state
            </button>
            <button
              class="btn btn-sm btn-outline-success"
              @click="markCurrentComplete"
            >
              Mark current complete
            </button>
            <button
              class="btn btn-sm btn-outline-danger"
              @click="markCurrentError"
            >
              Mark current error
            </button>
          </div>

          <!-- Step 1 specific controls -->
          <div
            v-if="activeStep === firstWizardStepKey"
            class="mt-3 d-flex flex-wrap gap-2"
          >
            <span class="text-muted small align-self-center"
              >Step 1 actions:</span
            >
            <button
              class="btn btn-sm btn-outline-warning"
              @click="sidebarRef?.setSubstepVisibility('step4_citor', false)"
            >
              Hide Step 4 — Citor
            </button>
            <button
              class="btn btn-sm btn-outline-secondary"
              @click="sidebarRef?.setSubstepVisibility('step4_citor', true)"
            >
              Show Step 4 — Citor
            </button>
          </div>
        </div>
      </div>

      <!-- Step Nav buttons -->
      <StepNavButtons
        v-if="!isSurveyStep && isActiveStepLoaded"
        :has-prev="sidebarRef?.hasPrev ?? false"
        :has-next="sidebarRef?.hasNext ?? false"
        :is-last-step="sidebarRef?.isLastStep ?? false"
        @prev="sidebarRef?.navigatePrevious()"
        @next="sidebarRef?.attemptNext()"
        @submit="onSubmit"
      />
    </div>
  </div>

  <SubmitValidationModal
    :visible="submitDialog.visible"
    :phase="submitDialog.phase"
    :steps="submitDialog.steps"
    :issues="submitDialog.issues"
    :error-message="submitDialog.errorMessage"
    @close="submitDialog.visible = false"
    @retry="saveCompletedApplication"
    @go-to="goToSubstep"
  />

  <!-- ── Mobile drawer overlay ───────────────────────────────────────────── -->
  <Teleport to="body">
    <div
      v-show="layoutStore.mobileNavOpen"
      class="mobile-drawer-overlay"
      @click.self="layoutStore.closeMobileNav()"
    >
      <div class="mobile-drawer">
        <div class="mobile-drawer-header">
          <div class="mobile-drawer-title">
            <font-awesome-icon icon="user" class="me-2" />
            {{ authStore.displayName || 'Menu' }}
          </div>
          <button
            class="btn btn-link text-white p-0"
            aria-label="Close navigation menu"
            @click="layoutStore.closeMobileNav()"
          >
            <font-awesome-icon icon="xmark" size="lg" />
          </button>
        </div>

        <!-- Step navigation — hidden during the step0 survey -->
        <div
          v-if="!isSurveyStep && isActiveStepLoaded"
          class="mobile-drawer-steps"
        >
          <ApplicationStepSidebar
            :steps="wizardSteps"
            :survey-step-key="surveyStepKey"
            :register-bridge="false"
            @navigate="onMobileNavigate"
          />
        </div>

        <!-- User actions -->
        <div class="mobile-drawer-user">
          <button
            class="mobile-drawer-action"
            @click="handlePreviousApplications"
          >
            <font-awesome-icon icon="list" class="me-2" />
            Previous Applications
          </button>
          <button class="mobile-drawer-action" @click="handleLogout">
            <font-awesome-icon icon="right-from-bracket" class="me-2" />
            Log out
          </button>
        </div>
      </div>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
  import ApplicationStepSidebar from '@/components/wizard/ApplicationStepSidebar.vue';
  import StepFormViewer from '@/components/wizard/StepFormViewer.vue';
  import StepNavButtons from '@/components/wizard/StepNavButton.vue';
  import SubmitValidationModal from '@/components/wizard/SubmitValidationModal.vue';
  import {
    getValidationTargets,
    type SubmitIssue,
    type SubmitPhase,
    type SubmitStepProgress,
  } from '@/components/wizard/submitValidation';
  import {
    repairResumeVisibility,
    type ResumeWizardState,
  } from '@/components/wizard/resumeWizardState';
  import {
    deriveInitialNavState,
    mapSidebarStepsToWizardSteps,
  } from '@/config/wizardSteps';
  import type ChefsService from '@/services/ChefsService';
  import { useAuthStore, useLayoutStore, useWizardStateStore } from '@/stores';
  import { pinia } from '@/stores/pinia';
  import type {
    StepStatus,
    WizardNavState,
    WizardStep,
  } from '@/types/applicationStep';
  import { usePathwayController } from '@/wizard/pathway/usePathwayController';
  import { computed, inject, onMounted, onUnmounted, reactive, ref } from 'vue';
  import { useRoute, useRouter } from 'vue-router';

  const chefsService = inject<ChefsService>('chefsService')!;
  const route = useRoute();

  const stepFormViewerRef = ref<InstanceType<typeof StepFormViewer> | null>(
    null
  );
  const sidebarRef = ref<InstanceType<typeof ApplicationStepSidebar> | null>(
    null
  );
  const activeStep = ref('');
  const surveyStepKey = ref('');
  const firstWizardStepKey = ref('');
  const showDevControls = ref(false);
  const submissionPublicId = ref<string | null>(null);

  /** All logical form step keys in display order, including step0 (survey), sourced from the backend. */
  const allStepKeys = ref<string[]>([]);
  /** Sidebar-visible steps (excludes step0, which is the pre-qualifying survey, not a wizard step). */
  const wizardSteps = ref<WizardStep[]>([]);
  /** Initial hidden step/substep state, derived from the backend sidebar structure. */
  const initialNavState = ref<WizardNavState>({
    hiddenSteps: {},
    hiddenSubsteps: {},
  });
  const wizardStateStore = useWizardStateStore(pinia);
  // Step/substep availability is derived from the form data (Repgrant-7xx).
  usePathwayController(pinia);
  let resumedState: ResumeWizardState | null = null;
  let hydrationComplete = false;
  let disposed = false;

  const substepToStepMap = computed<Record<string, string>>(() => {
    const map: Record<string, string> = {};
    for (const step of wizardSteps.value) {
      map[step.key] = step.key;
      for (const substep of step.substeps) {
        map[substep.key] = step.key;
      }
    }
    map[surveyStepKey.value] = surveyStepKey.value;
    return map;
  });

  // Set submissionPublicId synchronously so child components (StepFormViewer)
  // see it in their own onMounted, which runs before the parent's onMounted.
  const rawId = route.params.id;
  if (rawId) {
    submissionPublicId.value = Array.isArray(rawId) ? rawId[0] : rawId;
  }

  onMounted(async () => {
    try {
      const sortedDtos = (await chefsService.getSidebarStructure())
        .slice()
        .sort((a, b) => a.order - b.order);
      if (disposed) return;

      allStepKeys.value = sortedDtos.map((d) => d.key);

      // By contract, the first ordered item from the API is always the survey.
      const surveyDto = sortedDtos[0];
      if (surveyDto) {
        surveyStepKey.value = surveyDto.key;
      }

      const wizardStepDtos = sortedDtos.filter(
        (d) => d.key !== surveyStepKey.value
      );
      firstWizardStepKey.value = wizardStepDtos[0]?.key ?? surveyStepKey.value;

      wizardSteps.value = mapSidebarStepsToWizardSteps(wizardStepDtos);
      // Make the step config available to the bridge/store before the sidebar
      // mounts (replays deferred calls such as wizardBanPathway).
      wizardStateStore.setSteps(wizardSteps.value);
      // Progressive reveal by default; onResumeWizardState overrides this
      // with the real saved state if the carrier step has one.
      initialNavState.value = deriveInitialNavState(wizardStepDtos);
      initializeWizard();
    } catch (error) {
      console.error('Failed to load sidebar structure from backend', error);
    }
  });

  const isSurveyStep = computed(() => activeStep.value === surveyStepKey.value);

  /** Top-level step keys whose form has finished loading (or timed out). */
  const readyStepKeys = ref(new Set<string>());
  function onStepReady(stepKey: string) {
    readyStepKeys.value.add(stepKey);
    // Trigger reactivity - Set mutation alone isn't tracked by refs.
    readyStepKeys.value = new Set(readyStepKeys.value);
  }
  /** Don't show the sidebar/nav until the currently active step's own form is ready. */
  const isActiveStepLoaded = computed(() =>
    readyStepKeys.value.has(activeStepKey.value)
  );

  const authStore = useAuthStore();
  const layoutStore = useLayoutStore();
  const router = useRouter();

  let devToggleArmed = false;
  let devToggleArmedTimer: ReturnType<typeof setTimeout> | undefined;
  /** Synchronous guard against concurrent auto-save calls racing to create the draft submission. */
  let isCreatingDraft = false;

  function handleDevToggleKeydown(event: KeyboardEvent) {
    const key = event.key.toLowerCase();
    if ((event.ctrlKey || event.metaKey) && key === 'd') {
      event.preventDefault();
      devToggleArmed = true;
      clearTimeout(devToggleArmedTimer);
      devToggleArmedTimer = setTimeout(() => {
        devToggleArmed = false;
      }, 1500);
      return;
    }
    if (devToggleArmed && key === 't') {
      event.preventDefault();
      showDevControls.value = !showDevControls.value;
      devToggleArmed = false;
      clearTimeout(devToggleArmedTimer);
    }
  }

  onMounted(() => {
    layoutStore.setHasMobileNav(true);
    globalThis.addEventListener('keydown', handleDevToggleKeydown);
  });
  onUnmounted(() => {
    disposed = true;
    layoutStore.setHasMobileNav(false);
    layoutStore.closeMobileNav();
    globalThis.removeEventListener('keydown', handleDevToggleKeydown);
    clearTimeout(devToggleArmedTimer);
    wizardStateStore.reset();
  });
  /** Resolve the top-level form key from either a step key or substep key. */
  const activeStepKey = computed(
    () => substepToStepMap.value[activeStep.value] ?? activeStep.value
  );

  function onNavigate(stepKey: string) {
    activeStep.value = stepKey;
  }

  function onSurveyComplete() {
    if (activeStep.value !== surveyStepKey.value) return;
    activeStep.value = firstWizardStepKey.value;
  }

  function onHydrationComplete() {
    if (hydrationComplete) return;
    hydrationComplete = true;
    initializeWizard();
  }

  function initializeWizard() {
    if (disposed || !allStepKeys.value.length || !hydrationComplete) return;
    const state = resumedState
      ? repairResumeVisibility(resumedState, wizardSteps.value)
      : null;
    wizardStateStore.init(
      state?.activeSubstep ||
        state?.activeStep ||
        wizardSteps.value[0]?.defaultSubstep ||
        firstWizardStepKey.value,
      state ?? initialNavState.value,
      (state?.statusMap ?? {}) as Record<string, StepStatus>,
      state?.disabledMap ?? {}
    );
    activeStep.value =
      state?.activeStep ||
      (submissionPublicId.value
        ? firstWizardStepKey.value
        : surveyStepKey.value);
  }

  function onResumeWizardState(state: ResumeWizardState) {
    if (disposed) return;
    resumedState = state;
    initializeWizard();
  }

  function onMobileNavigate(stepKey: string) {
    activeStep.value = stepKey;
    sidebarRef.value?.updateSidebar?.(stepKey);
    layoutStore.closeMobileNav();
  }

  function handlePreviousApplications() {
    layoutStore.closeMobileNav();
    router.push('/previous-activity');
  }

  function handleLogout() {
    authStore.clearUserInfo();
    globalThis.location.href = `${import.meta.env.BASE_URL}api/auth/logout`;
  }

  function unlockAll() {
    sidebarRef.value?.setAllVisibility({
      hiddenSteps: {},
      hiddenSubsteps: {},
    } as WizardNavState);
    wizardSteps.value.forEach((s) =>
      sidebarRef.value?.setStepClickable(s.key, true)
    );
  }

  function lockAll() {
    sidebarRef.value?.setAllVisibility(initialNavState.value);
  }

  function markCurrentComplete() {
    sidebarRef.value?.setStepStatus(activeStep.value, 'completed');
  }

  function markCurrentError() {
    sidebarRef.value?.setStepStatus(activeStep.value, 'error');
  }

  async function onNeedsSubmission() {
    if (submissionPublicId.value || isCreatingDraft) return; // Already created or in flight.
    isCreatingDraft = true;

    try {
      const submission = await chefsService.createDraftSubmission();
      submissionPublicId.value = submission.publicId;

      // Replace URL so refresh reloads the saved submission.
      if (route.name === 'ApplicationManager') {
        router.replace({
          name: 'ResumeApplication',
          params: { id: submission.publicId },
        });
      }
    } catch (err) {
      console.error(
        '[ApplicationManager] Failed to create draft submission:',
        err
      );
    } finally {
      isCreatingDraft = false;
    }
  }

  function onStepSaved(_stepKey: string, publicId: string) {
    if (!submissionPublicId.value) {
      submissionPublicId.value = publicId;
      // Replace URL so refresh reloads the saved submission.
      if (route.name === 'ApplicationManager') {
        router.replace({
          name: 'ResumeApplication',
          params: { id: publicId },
        });
      }
    }
  }

  const submitDialog = reactive({
    visible: false,
    phase: 'validating' as SubmitPhase,
    steps: [] as SubmitStepProgress[],
    issues: [] as SubmitIssue[],
    errorMessage: '',
  });
  let isSubmitting = false;

  function describeSubstep(substepKey: string): string {
    for (const step of wizardSteps.value) {
      const substep = step.substeps.find((s) => s.key === substepKey);
      if (!substep) continue;
      return step.substeps.length > 1 && substep.label !== step.title
        ? `${step.title} — ${substep.label}`
        : step.title;
    }
    return substepKey;
  }

  /**
   * "Save Completed Application": validates every visible step/substep's
   * CHEFS form (showing progress), then saves and submits only if all pass.
   */
  async function onSubmit() {
    if (!submissionPublicId.value) {
      console.warn('[ApplicationManager] No submission to submit.');
      return;
    }
    const viewer = stepFormViewerRef.value;
    if (!viewer || isSubmitting) return;
    isSubmitting = true;

    try {
      const targets = getValidationTargets(
        wizardSteps.value,
        wizardStateStore.isStepHidden,
        wizardStateStore.isSubstepHidden
      );
      const titles = new Map(wizardSteps.value.map((s) => [s.key, s.title]));
      submitDialog.steps = targets.map((t) => ({
        key: t.stepKey,
        title: titles.get(t.stepKey) ?? t.stepKey,
        status: 'pending' as const,
      }));
      submitDialog.issues = [];
      submitDialog.errorMessage = '';
      submitDialog.phase = 'validating';
      submitDialog.visible = true;

      const result = await viewer.validateSteps(targets, (stepKey, status) => {
        const item = submitDialog.steps.find((s) => s.key === stepKey);
        if (item) item.status = status;
      });
      if (disposed) return;

      const failed = new Set(result.failedSubsteps);
      const loadFailed = new Set(result.loadFailedSteps);
      for (const target of targets) {
        if (loadFailed.has(target.stepKey)) continue;
        for (const substepKey of target.substepKeys) {
          wizardStateStore.setStepStatus(
            substepKey,
            failed.has(substepKey) ? 'error' : 'completed'
          );
        }
      }

      if (!result.valid) {
        console.warn(
          '[ApplicationManager] Validation failed for substeps:',
          result.failedSubsteps,
          'unloaded steps:',
          result.loadFailedSteps
        );
        submitDialog.issues = targets.flatMap((target) =>
          loadFailed.has(target.stepKey)
            ? [
                {
                  substepKey: target.substepKeys[0],
                  label: `${titles.get(target.stepKey) ?? target.stepKey} — could not be loaded, please open it and try again`,
                },
              ]
            : target.substepKeys
                .filter((key) => failed.has(key))
                .map((key) => ({
                  substepKey: key,
                  label: describeSubstep(key),
                }))
        );
        submitDialog.phase = 'invalid';
        return;
      }

      await saveCompletedApplication();
    } finally {
      isSubmitting = false;
    }
  }

  async function saveCompletedApplication() {
    if (!submissionPublicId.value || submitDialog.phase === 'saving') return;
    submitDialog.phase = 'saving';
    submitDialog.errorMessage = '';
    try {
      await stepFormViewerRef.value?.flushAllSteps();
      await chefsService.submitApplication(submissionPublicId.value);
      submitDialog.visible = false;
      router.push({
        name: 'PreviousActivity',
        query: { submitted: submissionPublicId.value },
      });
    } catch (err) {
      console.error('[ApplicationManager] Submit failed:', err);
      submitDialog.phase = 'error';
      submitDialog.errorMessage = 'Please check your connection and try again.';
    }
  }

  /** Closes the dialog and opens the substep so its errors are shown. */
  function goToSubstep(substepKey: string) {
    submitDialog.visible = false;
    sidebarRef.value?.updateSidebar?.(substepKey);
    // Wait for the step/substep switch to settle before highlighting errors.
    setTimeout(() => {
      if (!disposed) window.wizardValidateStep?.(substepKey);
    }, 300);
  }
</script>

<style scoped>
  .wizard-preview-layout {
    display: flex;
    min-height: calc(100vh - var(--app-header-offset, 62px));
    font-family: BCSans, 'Noto Sans', Verdana, Arial, sans-serif;
    /* Pull up to cancel the app-main padding-top (2rem) and container gutter */
    margin-top: -2rem;
    margin-left: calc(var(--bs-gutter-x, 1.5rem) * -0.5);
    margin-right: calc(var(--bs-gutter-x, 1.5rem) * -0.5);
  }

  .wizard-col {
    flex: 0 0 320px;
    width: 320px;
    min-width: 320px;
  }

  .wizard-col:has(.wiz-sidebar.is-collapsed) {
    flex-basis: 62px;
    width: 62px;
    min-width: 62px;
  }

  .content-col {
    flex: 1;
    min-width: 0;
    padding: 1.5rem;
    background: #fff;
  }

  .step-indicator {
    margin-bottom: 1rem;
  }

  .badge {
    background: #234075;
    color: #fff;
    padding: 6px 12px;
    border-radius: 4px;
    font-size: 13px;
  }

  .fake-panel {
    background: #f8f9fa;
    border: 1px dashed #ced4da;
    border-radius: 8px;
    padding: 2rem;
    min-height: 300px;
  }

  @media (max-width: 991px) {
    .wizard-preview-layout {
      flex-direction: column;
    }

    .wizard-col {
      flex-basis: auto;
      width: auto;
      min-width: 0;
    }

    .content-col {
      padding: 1rem 0.75rem 2rem;
    }
  }

  /* ── Mobile trigger button ─────────────────────────────────────── */
  .mobile-nav-trigger {
    display: flex;
    align-items: center;
    margin-right: 0.5rem;
  }
</style>

<style>
  /* ── Mobile drawer overlay ─────────────────────────────────────── */
  .mobile-drawer-overlay {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.45);
    z-index: 1050;
    display: flex;
    align-items: stretch;
  }

  .mobile-drawer {
    width: min(340px, 90vw);
    background: #fff;
    display: flex;
    flex-direction: column;
    overflow-y: auto;
  }

  .mobile-drawer-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0.75rem 1rem;
    background: #234075;
    color: #fff;
    flex-shrink: 0;
  }

  .mobile-drawer-title {
    font-weight: 600;
    font-size: 1rem;
  }

  .mobile-drawer-steps {
    flex: 1;
    overflow-y: auto;
  }

  /* Reset sidebar positioning inside the drawer */
  .mobile-drawer-steps .wiz-sidebar {
    position: static !important;
    width: 100% !important;
    height: auto !important;
    min-height: unset !important;
  }

  /* Hide the desktop collapse toggle inside the drawer */
  .mobile-drawer-steps .wiz-toggle-btn {
    display: none !important;
  }

  .mobile-drawer-user {
    border-top: 1px solid #dee2e6;
    padding: 0.5rem 0;
    flex-shrink: 0;
  }

  .mobile-drawer-action {
    display: flex;
    align-items: center;
    width: 100%;
    padding: 0.65rem 1.25rem;
    background: none;
    border: none;
    font-size: 0.9375rem;
    color: #212529;
    cursor: pointer;
    text-align: left;
  }

  .mobile-drawer-action:hover {
    background: #f8f9fa;
  }
</style>
