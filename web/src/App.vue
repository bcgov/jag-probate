<template>
  <div id="app" class="app-outer">
    <NavigationTopbar />

    <main
      id="main-content"
      class="app-main container-fluid position-relative"
      :class="layoutStore.backdropClass"
      tabindex="-1"
    >
      <router-view />
    </main>

    <NavigationFooter />
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
</style>
