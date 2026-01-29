<template>
  <div class="probate-form-page">
    <FormHolder :form-link="CHEFS_FORM_URL" title="Probate Application Form" />
  </div>
</template>

<script setup lang="ts">
  import FormHolder from '@/components/shared/FormHolder.vue';
  import { useLayoutStore } from '@/stores';
  import { onMounted, onUnmounted } from 'vue';

  const layoutStore = useLayoutStore();

  const CHEFS_FORM_URL =
    'https://chefs-dev.apps.silver.devops.gov.bc.ca/app/form/submit?f=449a6ceb-9cf8-45cc-a697-e5bd46cfb522';

  onMounted(() => {
    layoutStore.setFullscreen(true);
  });

  onUnmounted(() => {
    layoutStore.resetFullscreen();
  });
</script>

<style lang="scss">
  .probate-form-page {
    width: 100%;
    height: 100vh;
    overflow: hidden;
  }
  .circle-radio .form-radio {
    display: flex !important;
    gap: 20px !important;
  }

  .circle-radio .form-check {
    margin: 0 !important;
  }

  .circle-radio input[type='radio'] {
    position: absolute !important;
    opacity: 0 !important;
  }

  .circle-radio .form-check-label {
    width: 60px !important;
    height: 60px !important;
    border-radius: 50% !important;
    background-color: white !important;
    color: #666 !important;
    display: flex !important;
    align-items: center !important;
    justify-content: center !important;
    font-size: 14px !important;
    font-weight: 600 !important;
    border: 2px solid #ccc !important;
    cursor: pointer !important;
    transition: all 0.3s ease !important;
    margin: 0 !important;
  }

  .circle-radio input[type='radio']:checked + .form-check-label {
    background-color: #003366 !important;
    border-color: #003366 !important;
    color: white !important;
  }

  .circle-radio .form-check-label:hover {
    border-color: #003366 !important;
  }
</style>
