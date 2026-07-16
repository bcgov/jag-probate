<template>
  <div class="wizard-nav-buttons row mt-4">
    <!-- Previous -->
    <div class="col-md-3">
      <button
        v-if="hasPrev"
        type="button"
        class="btn btn-warning"
        @click="$emit('prev')"
      >
        &lt; Previous
      </button>
    </div>

    <!-- Spacer -->
    <div class="col-md-3"></div>

    <!-- Next / Submit -->
    <div class="col-md-6 d-flex justify-content-end gap-2">
      <slot name="extra-actions" />

      <button
        v-if="!isLastStep"
        type="button"
        class="btn btn-primary"
        @click="$emit('next')"
      >
        Next &gt;
      </button>

      <slot
        v-if="isLastStep"
        name="submit"
      >
        <!-- Default submit button rendered when no override slot provided -->
        <button
          type="button"
          class="btn btn-success"
          @click="$emit('submit')"
        >
          Submit Application
        </button>
      </slot>
    </div>
  </div>
</template>

<script setup lang="ts">
  interface Props {
    /** Whether there is a previous substep to navigate to. */
    hasPrev: boolean;
    /** Whether there is a next substep to navigate to. */
    hasNext: boolean;
    /**
     * True when the current substep is the last visible one.
     * Replaces the Next button with the submit slot.
     */
    isLastStep: boolean;
  }

  defineProps<Props>();

  defineEmits<{
    (e: 'prev'): void;
    (e: 'next'): void;
    (e: 'submit'): void;
  }>();
</script>
