<template>
  <div class="wizard-preview-layout">
    <div class="wizard-col">
      <ApplicationStepSidebar
        ref="sidebarRef"
        :steps="PROBATE_WIZARD_STEPS"
        :initial-step="'step1'"
        :initial-nav-state="PROBATE_WIZARD_INITIAL_NAV_STATE"
        :initial-disabled-map="PROBATE_WIZARD_INITIAL_DISABLED_STEPS"
        @navigate="onNavigate"
      />
    </div>

    <div class="content-col">
      <!-- Active step indicator -->
      <div class="step-indicator">
        <span class="badge">Active substep: {{ activeStep }}</span>
      </div>

      <!-- Fake form content placeholder -->
      <div class="fake-panel">
        <h4>{{ activeStep }}</h4>
        <p class="text-muted">Form content for this step will render here.</p>

        <!-- Simulate unlocking steps to test nav -->
        <div class="controls mt-4">
          <p class="fw-bold mb-2">Dev controls (preview only)</p>
          <div class="d-flex flex-wrap gap-2">
            <button
              class="btn btn-sm btn-outline-secondary"
              @click="unlockAll"
            >
              Unlock all steps
            </button>
            <button
              class="btn btn-sm btn-outline-secondary"
              @click="lockAll"
            >
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
            v-if="activeStep === 'step1'"
            class="mt-3 d-flex flex-wrap gap-2"
          >
            <span class="text-muted small align-self-center">Step 1 actions:</span>
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

      <!-- Nav buttons -->
      <StepNavButtons
        :has-prev="sidebarRef?.hasPrev ?? false"
        :has-next="sidebarRef?.hasNext ?? false"
        :is-last-step="sidebarRef?.isLastStep ?? false"
        @prev="sidebarRef?.navigatePrevious()"
        @next="sidebarRef?.navigateNext()"
        @submit="onSubmit"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
  import StepNavButtons from '@/components/wizard/StepNavButton.vue';
  import ApplicationStepSidebar from '@/components/wizard/ApplicationStepSidebar.vue';
  import {
    PROBATE_WIZARD_INITIAL_DISABLED_STEPS,
    PROBATE_WIZARD_INITIAL_NAV_STATE,
    PROBATE_WIZARD_STEPS,
  } from '@/config/wizardSteps';
  import type { WizardNavState } from '@/types/applicationStep';
  import { ref } from 'vue';

  const sidebarRef = ref<InstanceType<typeof ApplicationStepSidebar> | null>(null);
  const activeStep = ref('step1');

  function onNavigate(stepKey: string) {
    activeStep.value = stepKey;
  }

  function unlockAll() {
    sidebarRef.value?.setAllVisibility({ hiddenSteps: {}, hiddenSubsteps: {} } as WizardNavState);
    PROBATE_WIZARD_STEPS.forEach((s) => sidebarRef.value?.setStepClickable(s.key, true));
  }

  function lockAll() {
    sidebarRef.value?.setAllVisibility(PROBATE_WIZARD_INITIAL_NAV_STATE);
  }

  function markCurrentComplete() {
    sidebarRef.value?.setStepStatus(activeStep.value, 'completed');
  }

  function markCurrentError() {
    sidebarRef.value?.setStepStatus(activeStep.value, 'error');
  }

  function onSubmit() {
    alert('Submit triggered (preview only)');
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
    flex-shrink: 0;
  }

  .content-col {
    flex: 1;
    padding: 2rem;
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

  @media (max-width: 768px) {
    .wizard-preview-layout {
      flex-direction: column;
    }

    .wizard-col {
      width: 100%;
    }
  }
</style>
