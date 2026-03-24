<template>
  <div class="container terms-page">
    <div class="row mt-3">
      <div class="col">
        <h1 class="text-primary ms-4">Terms and Conditions</h1>

        <div class="terms-content ms-4">
          <p>
            Represent Someone Who Died is a service provided by the Government
            of British Columbia to help you apply for a
            <ToolTip term="Representation Grant" />.
          </p>

          <ul>
            <li>
              The Supreme Court of British Columbia will review the documents
              that you create through this service.
            </li>
            <li>
              The Court may ask you for additional information even if you have
              used this service.
            </li>
            <li>
              The Court will decide whether or not to give you a Representation
              Grant.
            </li>
          </ul>

          <p>
            The Government of British Columbia has the right to change this
            service at any time.
          </p>

          <ul>
            <li>
              Learn more about the disclaimer and liability
              <a
                target="_blank"
                rel="noreferrer"
                href="https://www2.gov.bc.ca/gov/content/home/disclaimer"
                >here</a
              >.
            </li>
            <li>
              Learn more about privacy and security
              <a
                target="_blank"
                rel="noreferrer"
                href="https://www2.gov.bc.ca/gov/content/home/privacy"
                >here</a
              >.
            </li>
          </ul>

          <p>
            You previously agreed to these terms when you clicked "Continue" on
            the first page of this service entitled
            <RouterLink :to="{ name: 'pre-qualification' }" target="_blank">
              Is this service right for your situation?</RouterLink
            >
          </p>
        </div>

        <button
          type="button"
          class="btn btn-primary btn-lg ms-4 fw-bold accept-btn"
          @click="navigate"
        >
          Accept
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
  import ToolTip from '@/components/shared/glossary/ToolTip.vue';
  import { useRouter } from 'vue-router';

  const router = useRouter();

  function navigate() {
    router.push({ name: 'PreviousActivity' });
  }
</script>

<style scoped>
  .terms-page {
    padding-top: 2rem;
    padding-bottom: 2rem;
    max-width: 950px;
    color: black;
  }

  .terms-content {
    margin-top: 1rem;
    margin-bottom: 1.5rem;
  }

  .terms-content ul {
    margin-bottom: 1rem;
  }

  .terms-content li {
    margin-bottom: 0.4rem;
  }

  .accept-btn {
    font-size: 1.375rem;
  }
</style>
