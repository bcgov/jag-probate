<template>
  <div id="app" class="app-outer">
    <NavigationTopbar />

    <main class="app-main container">
      <router-view />
    </main>

    <NavigationFooter />
  </div>
</template>

<script setup lang="ts">
import NavigationTopbar from "./components/NavigationTopbar.vue";
import NavigationFooter from "./components/NavigationFooter.vue";
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
