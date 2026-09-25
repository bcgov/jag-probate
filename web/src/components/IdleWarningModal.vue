<template>
  <div v-if="visible" class="modal d-block" tabindex="-1" role="dialog">
    <div class="modal-dialog" role="document">
      <div class="modal-content">
        <div class="modal-header bg-warning text-light">
          <h2 class="modal-title mb-0 text-light">Are you still there?</h2>
        </div>
        <div class="modal-body">
          <p>
            Your session has been flagged as idle. To protect your privacy we
            will automatically sign you out in
            <b>{{ remainingSeconds }}</b> second<span
              v-if="remainingSeconds !== 1"
              >s</span
            >.
          </p>
        </div>
        <div class="modal-footer">
          <button
            type="button"
            class="btn btn-primary"
            @click="$emit('continue-working')"
          >
            Continue Working
          </button>
          <button
            type="button"
            class="btn btn-danger"
            @click="$emit('sign-out')"
          >
            Sign Out
          </button>
        </div>
      </div>
    </div>
  </div>
  <div v-if="visible" class="modal-backdrop fade show"></div>
</template>

<script setup lang="ts">
  defineProps<{
    visible: boolean;
    remainingSeconds: number;
  }>();

  defineEmits<{
    (e: 'continue-working'): void;
    (e: 'sign-out'): void;
  }>();
</script>
