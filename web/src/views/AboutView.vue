<template>
  <div class="about">
    <h2>About Probate Management System</h2>
    
    <div class="card mt-4">
      <div class="card-body">
        <h5 class="card-title">System Information</h5>
        <p class="card-text">
          The Probate Management System is a comprehensive solution for managing probate cases
          in British Columbia. Built with modern technologies to ensure efficiency and security.
        </p>
      </div>
    </div>

    <div class="card mt-3">
      <div class="card-body">
        <h5 class="card-title">Technology Stack</h5>
        <ul>
          <li>Frontend: Vue 3 with TypeScript</li>
          <li>Backend: .NET 10 Web API</li>
          <li>Database: PostgreSQL</li>
          <li>Deployment: OpenShift (BC Gov Emerald)</li>
        </ul>
      </div>
    </div>

    <div class="card mt-3">
      <div class="card-body">
        <h5 class="card-title">API Health Status</h5>
        <div v-if="healthLoading">Checking API...</div>
        <div v-else-if="healthStatus">
          <span class="badge bg-success me-2">{{ healthStatus.status }}</span>
          <small class="text-muted">Last checked: {{ healthStatus.timestamp }}</small>
        </div>
        <div v-else class="text-danger">API unavailable</div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import axios from 'axios';

interface HealthStatus {
  status: string;
  timestamp: string;
  application: string;
}

const healthStatus = ref<HealthStatus | null>(null);
const healthLoading = ref(true);

const checkHealth = async () => {
  try {
    const response = await axios.get('/api/health');
    healthStatus.value = response.data;
  } catch (err) {
    console.error('Health check failed:', err);
  } finally {
    healthLoading.value = false;
  }
};

onMounted(() => {
  checkHealth();
});
</script>

<style scoped>
.about {
  padding: 1rem 0;
}
</style>
