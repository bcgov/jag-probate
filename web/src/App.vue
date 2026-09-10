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
  import { useIdleTimeout } from './composables/useIdleTimeout';
  import { useAuthStore, useLayoutStore } from './stores';
  import { watch } from 'vue';

  const layoutStore = useLayoutStore();
  const authStore = useAuthStore();
  const idleTimeout = useIdleTimeout();

  // Start tracking idle time once the user is authenticated, stop when
  // they log out (e.g. via forced idle logout or manual sign-out).
  watch(
    () => authStore.isAuthenticated,
    (isAuthenticated) => {
      if (isAuthenticated) {
        idleTimeout.start();
      } else {
        idleTimeout.stop();
      }
    },
    { immediate: true }
  );
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
