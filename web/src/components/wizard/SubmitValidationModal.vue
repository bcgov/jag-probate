<template>
  <div
    v-if="visible"
    class="modal d-block"
    tabindex="-1"
    role="dialog"
    aria-modal="true"
    aria-labelledby="submit-validation-title"
  >
    <div class="modal-dialog modal-dialog-scrollable" role="document">
      <div class="modal-content">
        <div class="modal-header">
          <h2 id="submit-validation-title" class="modal-title h5 mb-0">
            {{ heading }}
          </h2>
        </div>

        <div class="modal-body">
          <p class="mb-3" aria-live="polite">{{ message }}</p>

          <ul class="list-unstyled mb-0 submit-validation-steps">
            <li
              v-for="step in steps"
              :key="step.key"
              class="d-flex align-items-center gap-2 py-1"
              :data-status="step.status"
            >
              <span class="status-icon" aria-hidden="true">
                <span
                  v-if="step.status === 'loading' || step.status === 'checking'"
                  class="spinner-border spinner-border-sm text-primary"
                ></span>
                <font-awesome-icon
                  v-else-if="step.status === 'valid'"
                  icon="circle-check"
                  class="text-success"
                />
                <font-awesome-icon
                  v-else-if="
                    step.status === 'invalid' || step.status === 'error'
                  "
                  icon="circle-xmark"
                  class="text-danger"
                />
                <font-awesome-icon v-else icon="circle" class="text-muted" />
              </span>
              <span class="flex-grow-1">{{ step.title }}</span>
              <span class="small text-muted">{{
                STATUS_TEXT[step.status]
              }}</span>
            </li>
          </ul>

          <div
            v-if="phase === 'saving'"
            class="d-flex align-items-center gap-2 mt-3"
            role="status"
          >
            <span class="spinner-border spinner-border-sm text-primary"></span>
            <span>Saving your application…</span>
          </div>

          <div v-if="phase === 'invalid' && issues.length" class="mt-3">
            <p class="fw-bold mb-2">Please fix the following:</p>
            <ul class="mb-0">
              <li v-for="issue in issues" :key="issue.substepKey">
                <button
                  type="button"
                  class="btn btn-link p-0 text-start"
                  @click="$emit('go-to', issue.substepKey)"
                >
                  {{ issue.label }}
                </button>
              </li>
            </ul>
          </div>

          <div
            v-if="phase === 'error' && errorMessage"
            class="alert alert-danger mt-3 mb-0"
            role="alert"
          >
            {{ errorMessage }}
          </div>
        </div>

        <div
          v-if="phase === 'invalid' || phase === 'error'"
          class="modal-footer"
        >
          <button
            type="button"
            class="btn btn-outline-secondary"
            @click="$emit('close')"
          >
            Close
          </button>
          <button
            v-if="phase === 'invalid' && issues.length"
            type="button"
            class="btn btn-primary"
            @click="$emit('go-to', issues[0].substepKey)"
          >
            Go to first issue
          </button>
          <button
            v-if="phase === 'error'"
            type="button"
            class="btn btn-primary"
            @click="$emit('retry')"
          >
            Try again
          </button>
        </div>
      </div>
    </div>
  </div>
  <div v-if="visible" class="modal-backdrop fade show"></div>
</template>

<script setup lang="ts">
  import type {
    StepValidationStatus,
    SubmitIssue,
    SubmitPhase,
    SubmitStepProgress,
  } from '@/components/wizard/submitValidation';
  import { computed } from 'vue';

  const props = defineProps<{
    visible: boolean;
    phase: SubmitPhase;
    steps: SubmitStepProgress[];
    issues: SubmitIssue[];
    errorMessage?: string;
  }>();

  defineEmits<{
    (e: 'close'): void;
    (e: 'retry'): void;
    (e: 'go-to', substepKey: string): void;
  }>();

  const STATUS_TEXT: Record<StepValidationStatus, string> = {
    pending: 'Waiting',
    loading: 'Loading…',
    checking: 'Checking…',
    valid: 'Complete',
    invalid: 'Needs attention',
    error: 'Could not be checked',
  };

  const heading = computed(() => {
    switch (props.phase) {
      case 'invalid':
        return 'Your application is not complete';
      case 'saving':
        return 'Saving your application';
      case 'error':
        return 'Your application was not saved';
      default:
        return 'Checking your application';
    }
  });

  const message = computed(() => {
    const done = props.steps.filter(
      (s) =>
        s.status !== 'pending' &&
        s.status !== 'loading' &&
        s.status !== 'checking'
    ).length;
    switch (props.phase) {
      case 'invalid':
        return 'Some steps are missing information or have errors. Your application has not been saved as completed.';
      case 'saving':
        return 'All steps are complete.';
      case 'error':
        return 'All steps are complete, but saving failed.';
      default:
        return `Checking each step before saving (${done} of ${props.steps.length})…`;
    }
  });
</script>

<style scoped>
  .status-icon {
    display: inline-flex;
    justify-content: center;
    width: 1.25rem;
  }
</style>
