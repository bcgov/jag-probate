<template>
  <div class="probate-form-page">
    <FormHolder
      :form-link="CHEFS_FORM_URL"
      title="Probate Application Form"
    />
  </div>
</template>

<script setup lang="ts">
  import FormHolder from '@/components/shared/FormHolder.vue';
  import { useLayoutStore } from '@/stores';
  import { onMounted, onUnmounted } from 'vue';

  const layoutStore = useLayoutStore();

  const CHEFS_FORM_URL =
    'https://chefs-dev.apps.silver.devops.gov.bc.ca/app/form/submit?f=9aa7a0f0-b2f3-4d64-98f2-c13f596d1714';

  onMounted(() => {
    layoutStore.setFullscreen(true);
  });

  onUnmounted(() => {
    layoutStore.resetFullscreen();
  });
</script>

<style scoped lang="scss">
  .probate-form-page {
    width: 100%;
    height: 100vh;
    overflow: hidden;
  }
</style>
