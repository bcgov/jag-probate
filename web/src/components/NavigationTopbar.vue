<template>
  <header class="app-header">
    <nav class="navbar navbar-expand-lg navbar-dark bg-primary">
      <div class="container-fluid">
        <a class="navbar-brand" href="/">
          <img
            class="img-fluid d-none d-lg-block"
            src="/images/bcid-logo-rev-en.svg"
            width="177"
            height="44"
            alt="B.C. Government Logo"
          />
          <img
            class="img-fluid d-lg-none"
            src="/images/bcid-symbol-rev.svg"
            width="63"
            height="44"
            alt="B.C. Government Logo"
          />
        </a>

        <div class="navbar-brand me-auto">
          <h1 class="m-0 text-white d-none d-sm-inline">
            {{ layoutStore.navHeader }}
            <small
              class="small d-none d-lg-inline fs-6"
              v-if="layoutStore.navSubtitle"
              >{{ layoutStore.navSubtitle }}</small
            >
          </h1>
          <h1 class="m-0 text-white d-sm-none">Probate</h1>
          <span class="badge ms-2 p-1 ml-2" :class="envBadgeClass">{{
            environment
          }}</span>
        </div>

        <div v-if="authStore.isAuthenticated" class="navbar-nav ms-auto">
          <a class="nav-link text-white" href="#" @click.prevent="handleLogout">
            Log out
          </a>
        </div>
      </div>
    </nav>
  </header>
</template>

<script setup lang="ts">
  import { useAuthStore, useLayoutStore } from '@/stores';
  import { computed, onMounted, ref } from 'vue';

  const layoutStore = useLayoutStore();
  const authStore = useAuthStore();

  /**
   * Logs the user out by clearing the auth store and redirecting
   * to the backend logout endpoint, which signs out of Keycloak.
   */
  const handleLogout = () => {
    authStore.clearUserInfo();
    window.location.href = `${import.meta.env.BASE_URL}api/auth/logout`;
  };

  // Environment from runtime config
  const runtimeEnv = ref<string>('dev');

  // Fetch runtime config on mount
  onMounted(async () => {
    try {
      const response = await fetch(`${import.meta.env.BASE_URL}config.json`);
      const config = await response.json();
      runtimeEnv.value = config.environment || 'dev';
    } catch {
      console.warn('Could not load runtime config, defaulting to dev');
      runtimeEnv.value = 'dev';
    }
  });

  // Display environment
  const environment = computed(() => {
    const env = runtimeEnv.value.toLowerCase();
    if (env === 'dev' || env === 'development') return 'DEV';
    if (env === 'test') return 'TEST';
    if (env === 'prod' || env === 'production') return 'PROD';
    return env.toUpperCase();
  });

  // Badge color based on environment
  const envBadgeClass = computed(() => {
    const env = environment.value;
    if (env === 'PROD') return 'badge-prod';
    if (env === 'TEST') return 'badge-test';
    return 'badge-dev';
  });
</script>

<style scoped>
  .navbar-brand h1 {
    font-size: 1.1rem;
    font-weight: 600;
    display: flex;
    align-items: baseline;
  }

  .navbar-brand .badge {
    font-size: 0.6rem;
    padding: 0.25em 0.5em;
    font-weight: 700;
    align-items: center;
    vertical-align: middle;
  }

  .badge-dev {
    background-color: #fcba19;
    color: #003366;
  }

  .badge-test {
    background-color: #f9ca54;
    color: #003366;
  }

  .badge-prod {
    background-color: #dc3545;
    color: white;
  }

  .navbar .dropdown-menu {
    width: 250px !important;
  }
</style>
