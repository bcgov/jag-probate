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
          <BDropdown
            variant="link"
            no-caret
            menu-class="dropdown-menu-end"
            toggle-class="text-white text-decoration-none"
          >
            <template #button-content>
              <font-awesome-icon icon="fas-solid fa-user" class="me-1" />
              {{ authStore.displayName || 'User' }}
              <font-awesome-icon icon="fas-solid fa-chevron-down" class="ms-2" />
            </template>
            <BDropdownItem @click="handlePreviousApplications">
              <font-awesome-icon icon="fas-solid fa-list" class="me-2" />
              Previous Applications
            </BDropdownItem>
            <BDropdownItem @click="handleLogout">
              <font-awesome-icon
                icon="fas-solid fa-right-from-bracket"
                class="me-2"
              />
              Log out
            </BDropdownItem>
          </BDropdown>
        </div>
      </div>
    </nav>
  </header>
</template>

<script setup lang="ts">
  import { BDropdown, BDropdownItem } from 'bootstrap-vue-next';
  import {
    useAuthStore,
    useLayoutStore,
    useRuntimeConfigStore,
  } from '@/stores';
  import { computed } from 'vue';
  import { useRouter } from 'vue-router';

  const layoutStore = useLayoutStore();
  const authStore = useAuthStore();
  const runtimeConfigStore = useRuntimeConfigStore();
  const router = useRouter();

  /**
   * Logs the user out by clearing the auth store and redirecting
   * to the backend logout endpoint, which signs out of Keycloak.
   */
  const handleLogout = () => {
    authStore.clearUserInfo();
    globalThis.location.href = `${import.meta.env.BASE_URL}api/auth/logout`;
  };

  /**
   * Navigate to previous applications page
   */
  const handlePreviousApplications = () => {
    router.push('/previous-activity');
  };

  const environment = computed(() => runtimeConfigStore.environmentLabel);

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

  .dropdown-toggle::after {
    display: none;
  }

  /* White border for dropdown button */
  .navbar-nav :deep(.btn-link) {
    border: 1px solid white;
    border-radius: 4px;
    padding: 0.375rem 0.75rem;
  }

  .navbar-nav :deep(.btn-link:hover) {
    background-color: rgba(255, 255, 255, 0.1);
  }
</style>
