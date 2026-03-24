<template>
  <div id="new-application" class="card bg-white border-white">
    <div class="card home-content border-white">
      <!-- Navigation -->
      <div class="button-row">
        <button
          type="button"
          class="btn btn-outline-secondary"
          @click="router.back()"
        >
          <font-awesome-icon :icon="['fas', 'arrow-left']" class="me-2" /> Back
        </button>
      </div>

      <!-- CHEFS Form -->
      <div class="form-area">
        <ChefsFormViewer
          form-key="legal"
          :token="chefsToken"
          @submitted="onSubmitted"
          @form-error="onFormError"
        />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
  import { computed } from 'vue';
  import { useRouter } from 'vue-router';
  import ChefsFormViewer from '@/components/ChefsFormViewer.vue';
  import { useAuthStore } from '@/stores';
  import { extractTokenPayload } from '@/utils/claims';

  const router = useRouter();
  const authStore = useAuthStore();

  // Build token payload from Keycloak claims for CHEFS
  const chefsToken = computed(() => {
    if (!authStore.userInfo) return {};
    return extractTokenPayload(authStore.userInfo);
  });

  function onSubmitted(submissionId: string) {
    // Navigate back to previous activity after successful submission
    router.push({
      name: 'PreviousActivity',
      query: submissionId ? { submitted: submissionId } : undefined,
    });
  }

  function onFormError(error: unknown) {
    console.error('[NewApplication] CHEFS form error:', error);
  }
</script>

<style scoped>
  #new-application {
    margin: 0;
    padding: 0;
  }

  .card {
    border-radius: 0;
    border: none;
  }

  .home-content {
    padding: 2rem 1.5rem;
    max-width: 100%;
  }

  .button-row {
    display: flex;
    align-items: center;
    gap: 1rem;
    padding: 1rem 0;
  }

  .form-area {
    min-height: 600px;
  }
</style>
