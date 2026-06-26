<template>
  <div id="new-application" class="card bg-white border-white">
    <div class="card home-content border-white">
      <!-- Loading state for resume -->
      <div
        v-if="loadingResume"
        class="form-area d-flex align-items-center justify-content-center"
      >
        <span class="text-muted">Loading application…</span>
      </div>

      <!-- CHEFS Form -->
      <div v-else class="form-area">
        <ChefsFormViewer
          form-key="legal"
          :submission-id="chefsSubmissionId"
          :db-id="dbId"
          :read-only="isReadOnly"
          @submitted="onSubmitted"
          @saved="onSaved"
          @form-error="onFormError"
        />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
  import ChefsFormViewer from '@/components/ChefsFormViewer.vue';
  import ChefsService from '@/services/ChefsService';
  import { inject, onMounted, ref } from 'vue';
  import { useRoute, useRouter } from 'vue-router';

  const router = useRouter();
  const route = useRoute();
  const chefsService = inject<ChefsService>('chefsService')!;

  const chefsSubmissionId = ref<string | undefined>(undefined);
  const dbId = ref<string | undefined>(undefined);
  const isReadOnly = ref(false);
  const loadingResume = ref(false);

  onMounted(async () => {
    const rawId = route.params.id;
    if (!rawId) return; // new application — no pre-load needed

    const id = Array.isArray(rawId) ? rawId[0] : rawId;
    loadingResume.value = true;
    try {
      const submission = await chefsService.getSubmission(id);
      chefsSubmissionId.value = submission.chefsSubmissionId;
      dbId.value = submission.publicId;
      isReadOnly.value = submission.status === 'submitted';
    } catch (err) {
      console.error('[ApplicationForm] failed to load submission:', err);
    } finally {
      loadingResume.value = false;
    }
  });

  function onSubmitted(submissionId: string) {
    router.push({
      name: 'PreviousActivity',
      query: submissionId ? { submitted: submissionId } : undefined,
    });
  }

  function onSaved(publicId: string) {
    // After the first save on a new application, replace the URL with the
    // resume route so a page refresh reloads the saved submission instead
    // of starting a new one.
    if (route.name === 'NewApplication') {
      router.replace({ name: 'ResumeApplication', params: { id: publicId } });
    }
  }

  function onFormError(error: unknown) {
    console.error('[ApplicationForm] CHEFS form error:', error);
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
    padding: 2rem 1.5rem 0;
    width: 100%;
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

  .app-main.home-content {
    padding-bottom: 0;
  }

  .app-main.container-fluid {
    padding-bottom: 0;
  }
</style>
