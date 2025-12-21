<template>
  <div class="case-detail">
    <div v-if="loading" class="text-center my-5">
      <div class="spinner-border" role="status">
        <span class="visually-hidden">Loading...</span>
      </div>
    </div>

    <div v-else-if="error" class="alert alert-danger" role="alert">
      {{ error }}
    </div>

    <div v-else-if="caseItem">
      <div class="d-flex justify-content-between align-items-center mb-4">
        <h2>Case Details</h2>
        <button class="btn btn-secondary" @click="goBack">← Back to Cases</button>
      </div>

      <div class="card">
        <div class="card-body">
          <h3 class="card-title">{{ caseItem.title }}</h3>
          
          <dl class="row mt-4">
            <dt class="col-sm-3">Case Number:</dt>
            <dd class="col-sm-9">{{ caseItem.caseNumber }}</dd>

            <dt class="col-sm-3">Status:</dt>
            <dd class="col-sm-9">
              <span class="badge bg-primary">{{ caseItem.status }}</span>
            </dd>

            <dt class="col-sm-3">Filed Date:</dt>
            <dd class="col-sm-9">{{ formatDate(caseItem.filedDate) }}</dd>

            <dt class="col-sm-3">Description:</dt>
            <dd class="col-sm-9">{{ caseItem.description || 'No description provided' }}</dd>

            <dt class="col-sm-3">Created:</dt>
            <dd class="col-sm-9">{{ formatDate(caseItem.createdAt) }}</dd>

            <dt class="col-sm-3">Last Updated:</dt>
            <dd class="col-sm-9">{{ formatDate(caseItem.updatedAt) }}</dd>
          </dl>

          <div class="mt-4">
            <button class="btn btn-primary me-2">Edit</button>
            <button class="btn btn-danger" @click="deleteCase">Delete</button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import axios from 'axios';

interface Case {
  id: number;
  caseNumber: string;
  title: string;
  status: string;
  filedDate: string;
  description: string;
  createdAt: string;
  updatedAt: string;
}

const router = useRouter();
const route = useRoute();
const caseItem = ref<Case | null>(null);
const loading = ref(true);
const error = ref('');

const fetchCase = async () => {
  try {
    loading.value = true;
    const response = await axios.get(`/api/cases/${route.params.id}`);
    caseItem.value = response.data;
  } catch (err: any) {
    error.value = err.response?.data?.message || 'Failed to load case';
  } finally {
    loading.value = false;
  }
};

const goBack = () => {
  router.push('/cases');
};

const deleteCase = async () => {
  if (!confirm('Are you sure you want to delete this case?')) return;
  
  try {
    await axios.delete(`/api/cases/${route.params.id}`);
    alert('Case deleted successfully');
    goBack();
  } catch (err: any) {
    alert(err.response?.data?.message || 'Failed to delete case');
  }
};

const formatDate = (date: string) => {
  if (!date) return 'N/A';
  return new Date(date).toLocaleDateString() + ' ' + new Date(date).toLocaleTimeString();
};

onMounted(() => {
  fetchCase();
});
</script>

<style scoped>
.case-detail {
  padding: 1rem 0;
}
</style>
