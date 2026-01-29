<template>
  <div class="form-holder">
    <iframe
      v-if="formLink"
      :src="formLink"
      class="form-iframe"
      frameborder="0"
      allowfullscreen
      :title="title || 'CHEFS Form'"
    ></iframe>
    <div v-else class="no-form-message">
      <p>No form link provided</p>
    </div>
  </div>
</template>

<script setup lang="ts">
  interface Props {
    formLink: string;
    title?: string;
  }

  const props = defineProps<Props>();
</script>

<style scoped lang="scss">
  .form-holder {
    width: 100%;
    height: 100%;
    min-height: 100vh;
    position: relative;
  }

  .form-iframe {
    width: 100%;
    height: 100vh;
    min-height: 100vh;
    border: none;
  }

  .no-form-message {
    display: flex;
    align-items: center;
    justify-content: center;
    height: 100%;
    min-height: 100vh;
    background-color: #f8f9fa;
    
    p {
      color: #6c757d;
      font-size: 1.1rem;
    }
  }
</style>
