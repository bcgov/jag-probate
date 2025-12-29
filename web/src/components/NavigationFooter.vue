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
              href="https://www2.gov.bc.ca/gov/content/home/contact-us"
              target="_blank"
              rel="noopener noreferrer"
            >
              Contact Us
            </a>
          </li>
        </ul>
      </div>
    </nav>
  </footer>
</template>

<script setup lang="ts">
// Navigation Footer Component
// Uses BC Government Visual Identity
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
