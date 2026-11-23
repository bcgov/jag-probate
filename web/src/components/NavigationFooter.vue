<template>
  <footer class="mt-auto">
    <nav class="navbar navbar-expand-lg navbar-dark bg-primary">
      <div class="container-fluid">
        <ul class="navbar-nav">
          <li class="nav-item">
            <a
              class="nav-link"
              href="https://www2.gov.bc.ca/gov/content/home/disclaimer"
              target="_blank"
              rel="noopener noreferrer"
            >
              Disclaimer
            </a>
          </li>
          <li class="nav-item">
            <RouterLink class="nav-link" to="/terms"> Terms of Use </RouterLink>
          </li>
          <li class="nav-item">
            <a
              class="nav-link"
              href="https://www2.gov.bc.ca/gov/content/home/privacy"
              target="_blank"
              rel="noopener noreferrer"
            >
              Privacy
            </a>
          </li>
          <li class="nav-item">
            <a
              class="nav-link"
              href="https://www2.gov.bc.ca/gov/content/home/accessibility"
              target="_blank"
              rel="noopener noreferrer"
            >
              Accessibility
            </a>
          </li>
          <li class="nav-item">
            <a
              class="nav-link"
              href="https://www2.gov.bc.ca/gov/content/home/copyright"
              target="_blank"
              rel="noopener noreferrer"
            >
              Copyright
            </a>
          </li>
          <li class="nav-item">
            <a
              class="nav-link"
              href="#"
              role="button"
              @click.prevent="contactUs = true"
            >
              Contact Us
            </a>
          </li>
        </ul>
      </div>
    </nav>
    <BModal
      v-model="contactUs"
      size="lg"
      header-class="bg-primary text-white"
      header-close-variant="white"
      ok-only
      ok-title="Close"
    >
      <template #title>
        <h3 class="mb-0">General and Technical Enquiries</h3>
      </template>
      <div class="m-3">
        <p>Can't find what you are looking for? Have a technical issue?</p>
        <p>
          Contact us 8:00 am to 4:30 pm Pacific Time - Monday to Friday except
          Statutory Holidays
        </p>
        <p>
          CSO Support -
          <a href="mailto:Courts.CSO@gov.bc.ca">Courts.CSO@gov.bc.ca</a>
        </p>
      </div>
    </BModal>
  </footer>
</template>

<script setup lang="ts">
  import { ref } from 'vue';
  import { BModal } from 'bootstrap-vue-next';

  const contactUs = ref(false);
</script>

<style scoped>
  footer {
    margin-top: auto;
  }

  .navbar {
    padding: 0.5rem 0;
  }

  .navbar-nav {
    flex-direction: row;
    flex-wrap: wrap;
  }

  .nav-item {
    margin-right: 1rem;
  }

  @media (max-width: 768px) {
    .navbar-nav {
      flex-direction: column;
    }

    .nav-item {
      margin-right: 0;
    }
  }
</style>
