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

        <!-- Portal mount point: pages inject mobile-only controls here via Teleport -->
        <div id="topbar-mobile-extras"></div>

        <!-- Save and exit: mobile, beside the hamburger -->
        <button
          v-if="authStore.isAuthenticated && isApplicationInProgress"
          class="btn btn-link text-white text-decoration-none btn-save-exit d-lg-none me-3"
          aria-label="Save and exit"
          :disabled="isSavingAndExiting"
          @click="handleSaveAndExit"
        >
          <font-awesome-icon icon="floppy-disk" class="me-1" />
          Save and Exit
        </button>

        <!-- Hamburger: always visible on mobile when authenticated -->
        <button
          v-if="authStore.isAuthenticated"
          class="btn btn-link text-white p-0 d-lg-none"
          aria-label="Open navigation menu"
          @click="layoutStore.openMobileNav()"
        >
          <font-awesome-icon icon="bars" size="lg" />
        </button>

        <!-- Desktop user dropdown -->
        <div
          v-if="authStore.isAuthenticated"
          class="navbar-nav ms-auto d-none d-lg-flex align-items-center"
        >
          <button
            v-if="isApplicationInProgress"
            class="btn btn-link text-white text-decoration-none btn-save-exit me-3"
            :disabled="isSavingAndExiting"
            @click="handleSaveAndExit"
          >
            <font-awesome-icon icon="floppy-disk" class="me-1" />
            Save and Exit
          </button>

          <BDropdown
            variant="link"
            no-caret
            menu-class="dropdown-menu-end"
            toggle-class="text-white text-decoration-none"
          >
            <template #button-content>
              <font-awesome-icon icon="fas-solid fa-user" class="me-1" />
              {{ authStore.displayName || 'User' }}
              <font-awesome-icon
                icon="fas-solid fa-chevron-down"
                class="ms-2"
              />
            </template>
            <BDropdownItem
              v-if="!isOnPreviousActivity"
              @click="handlePreviousApplications"
            >
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

    <div
      v-if="saveAndExitError"
      class="alert alert-danger m-0 rounded-0 text-center"
      role="alert"
    >
      We could not save your application. Please try again.
    </div>

    <!-- Mobile drawer for non-wizard pages (wizard pages use their own drawer) -->
    <Teleport to="body">
      <div
        v-if="authStore.isAuthenticated && !layoutStore.hasMobileNav"
        v-show="layoutStore.mobileNavOpen"
        class="topbar-mobile-overlay"
        @click.self="layoutStore.closeMobileNav()"
      >
        <div class="topbar-mobile-drawer">
          <div class="topbar-mobile-drawer-header">
            <div class="topbar-mobile-drawer-title">
              <font-awesome-icon icon="user" class="me-2" />
              {{ authStore.displayName || 'User' }}
            </div>
            <button
              class="btn btn-link text-white p-0"
              aria-label="Close menu"
              @click="layoutStore.closeMobileNav()"
            >
              <font-awesome-icon icon="xmark" size="lg" />
            </button>
          </div>
          <div class="topbar-mobile-drawer-actions">
            <button
              v-if="isApplicationInProgress"
              class="topbar-mobile-action"
              :disabled="isSavingAndExiting"
              @click="handleSaveAndExit"
            >
              <font-awesome-icon icon="floppy-disk" class="me-2" />
              Save and Exit
            </button>
            <button
              v-if="!isOnPreviousActivity"
              class="topbar-mobile-action"
              @click="handlePreviousApplications"
            >
              <font-awesome-icon icon="list" class="me-2" />
              Previous Applications
            </button>
            <button class="topbar-mobile-action" @click="handleLogout">
              <font-awesome-icon icon="right-from-bracket" class="me-2" />
              Log out
            </button>
          </div>
        </div>
      </div>
    </Teleport>
  </header>
</template>

<script setup lang="ts">
  import {
    useAuthStore,
    useLayoutStore,
    useRuntimeConfigStore,
  } from '@/stores';
  import { BDropdown, BDropdownItem } from 'bootstrap-vue-next';
  import { computed, ref } from 'vue';
  import { useRoute, useRouter } from 'vue-router';

  const layoutStore = useLayoutStore();
  const authStore = useAuthStore();
  const runtimeConfigStore = useRuntimeConfigStore();
  const router = useRouter();
  const route = useRoute();
  const isSavingAndExiting = ref(false);
  const saveAndExitError = ref(false);

  const isOnPreviousActivity = computed(
    () => route.path === '/previous-activity'
  );

  /** True while the user is actively working through an application form. */
  const isApplicationInProgress = computed(() =>
    ['NewApplication', 'ResumeApplication', 'ApplicationManager'].includes(
      route.name as string
    )
  );

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
    layoutStore.closeMobileNav();
    router.push('/previous-activity');
  };

  /**
   * Saves progress on the current step(s) and returns to the previous
   * activity list.
   *
   * Placeholder only — actual save-progress persistence is not yet
   * implemented and will be wired up once the backend supports it.
   */
  const handleSaveAndExit = async () => {
    if (isSavingAndExiting.value) return;
    isSavingAndExiting.value = true;
    saveAndExitError.value = false;
    layoutStore.closeMobileNav();
    try {
      if (typeof window.wizardFlushAll === 'function') {
        await window.wizardFlushAll();
      }
      await router.push('/previous-activity');
    } catch {
      saveAndExitError.value = true;
    } finally {
      isSavingAndExiting.value = false;
    }
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

  /* Same box model as the dropdown toggle button (border, radius, padding),
     just with a green fill so it reads as the primary action. */
  .btn-save-exit {
    border: 1px solid white;
    border-radius: 4px;
    padding: 0.375rem 0.75rem;
    background-color: #2e8540;
  }

  .btn-save-exit:hover {
    background-color: #23652f;
    color: #fff;
  }
</style>

<style>
  .topbar-mobile-overlay {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.45);
    z-index: 1050;
    display: flex;
    align-items: stretch;
  }

  .topbar-mobile-drawer {
    width: min(300px, 85vw);
    background: #fff;
    display: flex;
    flex-direction: column;
  }

  .topbar-mobile-drawer-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0.75rem 1rem;
    background: #234075;
    color: #fff;
    flex-shrink: 0;
  }

  .topbar-mobile-drawer-title {
    font-weight: 600;
    font-size: 1rem;
  }

  .topbar-mobile-drawer-actions {
    padding: 0.5rem 0;
  }

  .topbar-mobile-action {
    display: flex;
    align-items: center;
    width: 100%;
    padding: 0.75rem 1.25rem;
    background: none;
    border: none;
    font-size: 0.9375rem;
    color: #212529;
    cursor: pointer;
    text-align: left;
  }

  .topbar-mobile-action:hover {
    background: #f8f9fa;
  }
</style>
