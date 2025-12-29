<template>
  <div class="cases">
    <div class="d-flex justify-content-between align-items-center mb-3">
      <h2>Probate Cases</h2>
      <button class="btn btn-secondary" @click="goBack">← Back to Home</button>
    </div>

    <div v-if="loading" class="text-center my-5">
      <div class="spinner-border" role="status">
        <span class="visually-hidden">Loading...</span>
      </div>
    </div>

    <div v-else-if="error" class="alert alert-danger" role="alert">
      {{ error }}
    </div>

    <div v-else>
      <div class="mb-3">
        <button class="btn btn-success" @click="createCase">
          Create New Case
        </button>
      </div>

      <div v-if="cases.length === 0" class="alert alert-info">
        No cases found. Create your first case to get started.
      </div>

      <div v-else class="table-responsive">
        <table class="table table-striped table-hover">
          <thead class="table-dark">
            <tr>
              <th>Case Number</th>
              <th>Title</th>
              <th>Status</th>
              <th>Filed Date</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="caseItem in cases" :key="caseItem.id">
              <td>{{ caseItem.caseNumber }}</td>
              <td>{{ caseItem.title }}</td>
              <td>
                <span class="badge bg-primary">{{ caseItem.status }}</span>
              </td>
              <td>{{ formatDate(caseItem.filedDate) }}</td>
              <td>
                <router-link
                  :to="`/cases/${caseItem.id}`"
                  class="btn btn-sm btn-outline-primary"
                >
                  View
                </router-link>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useRouter } from "vue-router";
import axios from "axios";

interface Case {
  id: number;
  caseNumber: string;
  title: string;
  status: string;
  filedDate: string;
  description: string;
}

const router = useRouter();
const cases = ref<Case[]>([]);
const loading = ref(true);
const error = ref("");

const fetchCases = async () => {
  try {
    loading.value = true;
    const response = await axios.get("/api/cases");
    cases.value = response.data;
  } catch (err: any) {
    error.value = err.response?.data?.message || "Failed to load cases";
  } finally {
    loading.value = false;
  }
};

const createCase = () => {
  router.push("/cases/new");
};

const goBack = () => {
  router.push("/");
};

const formatDate = (date: string) => {
  if (!date) return "N/A";
  return new Date(date).toLocaleDateString();
};

onMounted(() => {
  fetchCases();
});
</script>

<style scoped>
.cases {
  padding: 1rem 0;
}
</style>
