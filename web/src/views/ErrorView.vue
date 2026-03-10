<template>
  <ErrorComponent
    :status="resolvedStatus"
    :description="resolvedDescription"
    :details="resolvedDetails"
  />
</template>

<script setup lang="ts">
  import ErrorComponent from '@/components/ErrorComponent.vue';
import { computed } from 'vue';
import { useRoute } from 'vue-router';

  const route = useRoute();

  function getQueryValue(value: string | string[] | null | undefined): string {
    if (Array.isArray(value)) {
      return value[0] ?? '';
    }

    return value ?? '';
  }

  const resolvedStatus = computed(() => {
    const statusText = getQueryValue(route.query.status as string | string[]);
    const parsedStatus = Number.parseInt(statusText, 10);

    if (!Number.isNaN(parsedStatus) && parsedStatus >= 100 && parsedStatus <= 599) {
      return parsedStatus;
    }

    return 500;
  });

  const resolvedDescription = computed(() => {
    const description = getQueryValue(route.query.description as string | string[]).trim();
    return description || undefined;
  });

  const resolvedDetails = computed(() => {
    const details = getQueryValue(route.query.details as string | string[]).trim();
    return details || undefined;
  });
</script>
