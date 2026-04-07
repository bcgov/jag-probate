<template>
  <div class="container pre-qualification-page">
    <div class="row mt-3">
      <div class="col">
        <h1 class="text-primary">Is this service right for your situation?</h1>
        <p class="text-muted mb-4">
          Please answer the following questions to confirm this service is
          appropriate for your situation.
        </p>

        <ChefsFormViewer
          form-key="pre-qualification"
          @submitted="onSubmitted"
          @form-error="onFormError"
        />

        <div v-if="formError" class="alert alert-danger mt-3" role="alert">
          Something went wrong with the form. Please try again.
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
  import ChefsFormViewer from '@/components/ChefsFormViewer.vue';
  import { useApplicationStore } from '@/stores/PreviousApplicationStore';
  import { ref } from 'vue';
  import { useRouter } from 'vue-router';

  const router = useRouter();
  const applicationStore = useApplicationStore();
  const formError = ref(false);

  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  function onSubmitted(submissionId: string) {
    // Submission means the user qualified — initialise a new application
    // and proceed to the survey steps
    applicationStore.setExistingApplication(false);
    router.push({ name: 'surveys' });
  }

  function onFormError(error: unknown) {
    console.error('CHEFS form error:', error);
    formError.value = true;
  }
</script>

<style scoped>
  .pre-qualification-page {
    padding-top: 2rem;
    padding-bottom: 2rem;
    max-width: 950px;
    color: black;
  }
</style>
