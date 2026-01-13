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

        <div class="navbar-brand ms-3">
          <h1 class="m-0 text-white">
            Probate
            <span class="badge ms-2 p-1 ml-2" :class="envBadgeClass">{{
              environment
            }}</span>
          </h1>
        </div>

        <div class="navbar-nav ms-auto">
          <div class="nav-item dropdown">
            <a
              class="nav-link dropdown-toggle text-white"
              href="#"
              role="button"
              data-bs-toggle="dropdown"
              aria-expanded="false"
            >
              <svg
                xmlns="http://www.w3.org/2000/svg"
                width="24"
                height="24"
                fill="currentColor"
                class="bi bi-person-circle"
                viewBox="0 0 16 16"
              >
                <path d="M11 6a3 3 0 1 1-6 0 3 3 0 0 1 6 0z" />
                <path
                  fill-rule="evenodd"
                  d="M0 8a8 8 0 1 1 16 0A8 8 0 0 1 0 8zm8-7a7 7 0 0 0-5.468 11.37C3.242 11.226 4.805 10 8 10s4.757 1.225 5.468 2.37A7 7 0 0 0 8 1z"
                />
              </svg>
            </a>
            <ul class="dropdown-menu dropdown-menu-end">
              <li><a class="dropdown-item" href="/dashboard">Dashboard</a></li>
              <li><hr class="dropdown-divider" /></li>
              <li><a class="dropdown-item" href="/logout">Sign out</a></li>
            </ul>
          </div>
        </div>
      </div>
    </nav>
  </header>
</template>

<script setup lang="ts">
import { computed, ref, onMounted } from "vue";

// Environment from runtime config
const runtimeEnv = ref<string>("dev");

// Fetch runtime config on mount
onMounted(async () => {
  try {
    const response = await fetch("/config.json");
    const config = await response.json();
    runtimeEnv.value = config.environment || "dev";
  } catch {
    console.warn("Could not load runtime config, defaulting to dev");
    runtimeEnv.value = "dev";
  }
});

// Display environment
const environment = computed(() => {
  const env = runtimeEnv.value.toLowerCase();
  if (env === "dev" || env === "development") return "DEV";
  if (env === "test") return "TEST";
  if (env === "prod" || env === "production") return "PROD";
  return env.toUpperCase();
});

// Badge color based on environment
const envBadgeClass = computed(() => {
  const env = environment.value;
  if (env === "PROD") return "badge-prod";
  if (env === "TEST") return "badge-test";
  return "badge-dev";
});
</script>

<style scoped>
.navbar-brand h1 {
  font-size: 1.25rem;
  font-weight: 600;
  display: flex;
  align-items: center;
}

.navbar-brand h1 .badge {
  font-size: 0.6rem;
  padding: 0.25em 0.5em;
  font-weight: 700;
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
