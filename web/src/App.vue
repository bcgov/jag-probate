<template>
  <div
    id="app"
    class="app-outer"
    :class="{ 'fullscreen-mode': layoutStore.isFullscreen }"
  >
    <NavigationTopbar v-if="!layoutStore.isFullscreen" />

    <main
      class="app-main"
      :class="[
        layoutStore.backdropClass,
        layoutStore.isFullscreen ? '' : 'container-fluid position-relative',
      ]"
    >
      <router-view />
    </main>

    <NavigationFooter v-if="!layoutStore.isFullscreen" />
  </div>
</template>

<script setup lang="ts">
  import NavigationFooter from './components/NavigationFooter.vue';
  import NavigationTopbar from './components/NavigationTopbar.vue';
  import { useLayoutStore } from './stores/LayoutStore';

  const layoutStore = useLayoutStore();
</script>

<style scoped>
  #app {
    min-height: 100vh;
    display: flex;
    flex-direction: column;
  }

  main {
    flex: 1;
  }

  .fullscreen-mode {
    height: 100vh;
    overflow: hidden;
  }

  .fullscreen-mode main {
    height: 100vh;
    padding: 0;
    margin: 0;
  }
</style>
