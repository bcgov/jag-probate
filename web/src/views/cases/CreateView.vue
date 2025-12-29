<template>
  <div class="create-case">
    <h2>Create New Probate Case</h2>

    <form @submit.prevent="handleSubmit">
      <div class="mb-3">
        <label for="caseNumber" class="form-label">Case Number</label>
        <input
          id="caseNumber"
          v-model="form.caseNumber"
          type="text"
          class="form-control"
          placeholder="e.g., PRB-2025-001"
          required
        />
      </div>

      <div class="mb-3">
        <label for="title" class="form-label">Case Title</label>
        <input
          id="title"
          v-model="form.title"
          type="text"
          class="form-control"
          required
        />
      </div>

      <div class="mb-3">
        <label for="description" class="form-label">Description</label>
        <textarea
          id="description"
          v-model="form.description"
          class="form-control"
          rows="4"
          required
        ></textarea>
      </div>

      <div class="mb-3">
        <label for="status" class="form-label">Status</label>
        <select id="status" v-model="form.status" class="form-select" required>
          <option value="Draft">Draft</option>
          <option value="Filed">Filed</option>
          <option value="In Review">In Review</option>
          <option value="Approved">Approved</option>
        </select>
      </div>

      <div v-if="error" class="alert alert-danger" role="alert">
        {{ error }}
      </div>

      <div class="d-flex gap-2">
        <button type="submit" class="btn btn-success" :disabled="submitting">
          {{ submitting ? "Creating..." : "Create Case" }}
        </button>
        <button type="button" class="btn btn-secondary" @click="cancel">
          Cancel
        </button>
      </div>
    </form>
  </div>
</template>

<script setup lang="ts">
import { ref } from "vue";
import { useRouter } from "vue-router";
import axios from "axios";

const router = useRouter();
const submitting = ref(false);
const error = ref("");

const form = ref({
  caseNumber: "",
  title: "",
  description: "",
  status: "Draft",
});

const handleSubmit = async () => {
  try {
    submitting.value = true;
    error.value = "";

    const response = await axios.post("/api/cases", form.value);

    // Navigate to the newly created case
    router.push(`/cases/${response.data.id}`);
  } catch (err: any) {
    error.value = err.response?.data?.message || "Failed to create case";
  } finally {
    submitting.value = false;
  }
};

const cancel = () => {
  router.push("/cases");
};
</script>

<style scoped>
.create-case {
  padding: 1rem 0;
  max-width: 800px;
}
</style>
