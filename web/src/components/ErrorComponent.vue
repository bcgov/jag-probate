<template>
  <section class="error-page container py-5" role="alert" aria-live="polite">
    <div class="error-card card shadow-sm border-0 mx-auto">
      <div class="card-body p-4 p-md-5 text-center">
        <p class="text-muted mb-2">Error {{ status }}</p>
        <h1 class="h3 mb-3">{{ resolvedDescription }}</h1>
        <p v-if="details" class="mb-0 text-secondary">{{ details }}</p>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
  import { computed } from 'vue';

  interface ErrorComponentProps {
    status: number;
    description?: string;
    details?: string;
  }

  const props = defineProps<ErrorComponentProps>();

  const knownStatusDescriptions: Record<number, string> = {
    400: 'Bad Request',
    401: 'Unauthorized',
    403: 'Forbidden',
    404: 'Not Found',
    408: 'Request Timeout',
    409: 'Conflict',
    422: 'Unprocessable Entity',
    429: 'Too Many Requests',
    500: 'Internal Server Error',
    502: 'Bad Gateway',
    503: 'Service Unavailable',
    504: 'Gateway Timeout',
  };

  const resolvedDescription = computed(() => {
    const explicitDescription = props.description?.trim();
    if (explicitDescription) {
      return explicitDescription;
    }

    return knownStatusDescriptions[props.status] ?? 'Unexpected Error';
  });
</script>

<style scoped>
  .error-page {
    min-height: 50vh;
    display: flex;
    align-items: center;
  }

  .error-card {
    max-width: 48rem;
    width: 100%;
  }
</style>
