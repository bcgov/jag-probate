<template>
  <div class="wizard-preview-layout">
    <!-- Desktop sidebar -->
    <div v-if="!isSurveyStep" class="wizard-col d-none d-lg-block">
      <ApplicationStepSidebar
        ref="sidebarRef"
        :steps="wizardSteps"
        :initial-step="firstWizardSubstepKey"
        :survey-step-key="surveyStepKey"
        :initial-nav-state="sidebarInitialNavState"
        :initial-status-map="sidebarInitialStatusMap"
        :initial-disabled-map="sidebarInitialDisabledMap"
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
        v-if="!isSurveyStep"
        :has-prev="sidebarRef?.hasPrev ?? false"
        :has-next="sidebarRef?.hasNext ?? false"
        :is-last-step="sidebarRef?.isLastStep ?? false"
        @prev="sidebarRef?.navigatePrevious()"
        @next="sidebarRef?.attemptNext()"
        @submit="onSubmit"
      />
    </div>
  </div>

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
        <div v-if="!isSurveyStep" class="mobile-drawer-steps">
          <ApplicationStepSidebar
            :steps="wizardSteps"
            :initial-step="activeStep"
            :survey-step-key="surveyStepKey"
            :initial-nav-state="sidebarInitialNavState"
            :initial-status-map="sidebarInitialStatusMap"
            :initial-disabled-map="sidebarInitialDisabledMap"
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
  import StepNavButtons from '@/components/wizard/StepNavButton.vue';
  import ApplicationStepSidebar from '@/components/wizard/ApplicationStepSidebar.vue';
  import StepFormViewer from '@/components/wizard/StepFormViewer.vue';
  import { useAuthStore, useLayoutStore } from '@/stores';
  import { useRoute, useRouter } from 'vue-router';
  import {
    mapSidebarStepsToWizardSteps,
    deriveInitialNavState,
  } from '@/config/wizardSteps';
  import type {
    StepStatus,
    WizardNavState,
    WizardStep,
  } from '@/types/applicationStep';
  import { useWizardState } from '@/composables/useWizardState';
  import { computed, inject, onMounted, onUnmounted, ref } from 'vue';
  import type ChefsService from '@/services/ChefsService';

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
  /** Snapshot of current wizard state so remounting (e.g. step0 round-trip) doesn't reset unlocks. */
  const persistedNavState = ref<WizardNavState | null>(null);
  const persistedStatusMap = ref<Record<string, StepStatus>>({});
  const persistedDisabledMap = ref<Record<string, boolean>>({});

  const { navState, statusMap, disabledMap } = useWizardState();

  const sidebarInitialNavState = computed<WizardNavState>(
    () => persistedNavState.value ?? initialNavState.value
  );
  const sidebarInitialStatusMap = computed<Record<string, StepStatus>>(() =>
    persistedNavState.value ? persistedStatusMap.value : {}
  );
  const sidebarInitialDisabledMap = computed<Record<string, boolean>>(() =>
    persistedNavState.value ? persistedDisabledMap.value : {}
  );

  const firstWizardSubstepKey = computed<string>(
    () => wizardSteps.value[0]?.defaultSubstep ?? firstWizardStepKey.value
  );

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

  function snapshotWizardState() {
    persistedNavState.value = {
      hiddenSteps: { ...navState.hiddenSteps },
      hiddenSubsteps: { ...navState.hiddenSubsteps },
    };
    persistedStatusMap.value = { ...statusMap };
    persistedDisabledMap.value = { ...disabledMap };
  }

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

      if (submissionPublicId.value) {
        // Resuming a saved submission — all steps should be visible and
        // navigable since the user has already progressed through them.
        initialNavState.value = { hiddenSteps: {}, hiddenSubsteps: {} };
      } else {
        // New application — progressive reveal (only first step visible).
        initialNavState.value = deriveInitialNavState(wizardStepDtos);
      }

      if (!activeStep.value) {
        if (submissionPublicId.value) {
          // Resuming — try to restore the last-active substep from localStorage.
          let resumed = false;
          try {
            const saved = localStorage.getItem(
              `wizard_state_${submissionPublicId.value}`
            );
            if (saved) {
              const { activeSubstep } = JSON.parse(saved);
              if (activeSubstep) {
                activeStep.value = activeSubstep;
                resumed = true;
              }
            }
          } catch {
            // Best-effort — fall back to first step.
          }
          if (!resumed) {
            activeStep.value = firstWizardStepKey.value;
          }
        } else {
          activeStep.value = surveyStepKey.value;
        }
      }
    } catch (error) {
      console.error('Failed to load sidebar structure from backend', error);
    }
  });

  const isSurveyStep = computed(() => activeStep.value === surveyStepKey.value);

  const authStore = useAuthStore();
  const layoutStore = useLayoutStore();
  const router = useRouter();

  let devToggleArmed = false;
  let devToggleArmedTimer: ReturnType<typeof setTimeout> | undefined;

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
    layoutStore.setHasMobileNav(false);
    layoutStore.closeMobileNav();
    globalThis.removeEventListener('keydown', handleDevToggleKeydown);
    clearTimeout(devToggleArmedTimer);
  });
  /** Resolve the top-level form key from either a step key or substep key. */
  const activeStepKey = computed(
    () => substepToStepMap.value[activeStep.value] ?? activeStep.value
  );

  function onNavigate(stepKey: string) {
    // Capture latest shared state before route-like step switches (especially step0).
    snapshotWizardState();
    activeStep.value = stepKey;
  }

  function onSurveyComplete() {
    activeStep.value = firstWizardStepKey.value;
  }

  function onMobileNavigate(stepKey: string) {
    snapshotWizardState();
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
    if (submissionPublicId.value) return; // Already created.

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

  async function onSubmit() {
    if (!submissionPublicId.value) {
      console.warn('[ApplicationManager] No submission to submit.');
      return;
    }

    // Validate all steps before submitting.
    const validation = stepFormViewerRef.value?.validateAllSteps();
    if (validation && !validation.valid) {
      console.warn(
        '[ApplicationManager] Validation failed for steps:',
        validation.failedSteps
      );
      // Navigate to the first failed step so the user can fix it.
      const firstFailed = validation.failedSteps[0];
      if (firstFailed) {
        activeStep.value = firstFailed;
        sidebarRef.value?.updateSidebar?.(firstFailed);
        sidebarRef.value?.setStepStatus(firstFailed, 'error');
      }
      return;
    }

    try {
      await chefsService.submitApplication(submissionPublicId.value);
      router.push({
        name: 'PreviousActivity',
        query: { submitted: submissionPublicId.value },
      });
    } catch (err) {
      console.error('[ApplicationManager] Submit failed:', err);
    }
  }
</script>

<style scoped>
  .wizard-preview-layout {
    display: flex;
    min-height: calc(100vh - 62px);
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
