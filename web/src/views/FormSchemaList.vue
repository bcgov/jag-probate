<template>
  <div class="schemas-page container-fluid py-4">
    <div class="d-flex align-items-center gap-3 mb-4">
      <h1 class="mb-0">Form Schemas</h1>
      <button class="btn btn-primary btn-sm" @click="openCreate">+ New Schema</button>
    </div>

    <div v-if="loading" class="text-muted">Loading…</div>
    <div v-else-if="error" class="alert alert-danger">{{ error }}</div>
    <table v-else class="table table-hover align-middle">
      <thead class="table-light">
        <tr>
          <th>Name</th>
          <th>Version</th>
          <th>Description</th>
          <th>Based on</th>
          <th>Created</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-if="schemas.length === 0">
          <td colspan="6" class="text-muted text-center py-4">No schemas yet.</td>
        </tr>
        <tr v-for="s in schemas" :key="s.id">
          <td class="fw-semibold">{{ s.name }}</td>
          <td><span class="badge bg-secondary">v{{ s.version }}</span></td>
          <td class="text-muted small">{{ s.description ?? '—' }}</td>
          <td class="text-muted small">{{ basedOnName(s.basedOnId) }}</td>
          <td class="text-muted small">{{ formatDate(s.createdAt) }}</td>
          <td class="text-end">
            <button class="btn btn-sm btn-outline-primary me-1" @click="editSchema(s)">Edit</button>
            <button class="btn btn-sm btn-outline-secondary me-1" @click="branchFrom(s)">Branch</button>
            <button class="btn btn-sm btn-outline-danger" @click="confirmDelete(s)">Delete</button>
          </td>
        </tr>
      </tbody>
    </table>

    <!-- Create / Branch Modal -->
    <div v-if="showCreateModal" class="modal-backdrop-custom" @click.self="showCreateModal = false">
      <div class="modal-dialog-custom">
        <div class="modal-header-custom">
          <strong>{{ createForm.basedOnId ? 'Branch from ' + basedOnName(createForm.basedOnId) : 'New Schema' }}</strong>
          <button type="button" class="btn-close" @click="showCreateModal = false"></button>
        </div>
        <div class="modal-body-custom">
          <div class="mb-3">
            <label class="form-label">Name <span class="text-danger">*</span></label>
            <input v-model="createForm.name" class="form-control" placeholder="e.g. probate-legal" />
          </div>
          <div class="mb-3">
            <label class="form-label">Description</label>
            <input v-model="createForm.description" class="form-control" placeholder="Optional" />
          </div>
          <div v-if="createError" class="alert alert-danger py-2 small">{{ createError }}</div>
        </div>
        <div class="modal-footer-custom">
          <button class="btn btn-secondary btn-sm" @click="showCreateModal = false">Cancel</button>
          <button class="btn btn-primary btn-sm" :disabled="creating" @click="submitCreate">
            {{ creating ? 'Creating…' : 'Create & Open Designer' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
  import { ref, onMounted } from 'vue';
  import { useRouter } from 'vue-router';
  import axios from 'axios';

  interface FormSchema {
    id: string;
    name: string;
    description?: string;
    basedOnId?: string;
    version: number;
    createdAt: string;
    schema: string;
  }

  const router = useRouter();
  const schemas = ref<FormSchema[]>([]);
  const loading = ref(true);
  const error = ref('');

  const showCreateModal = ref(false);
  const creating = ref(false);
  const createError = ref('');
  const createForm = ref({ name: '', description: '', basedOnId: '' as string | undefined, schema: '{}' });

  async function fetchSchemas() {
    loading.value = true;
    error.value = '';
    try {
      const { data } = await axios.get<FormSchema[]>('/api/form-schemas');
      schemas.value = data;
    } catch {
      error.value = 'Failed to load schemas.';
    } finally {
      loading.value = false;
    }
  }

  function basedOnName(id?: string) {
    if (!id) return '—';
    const found = schemas.value.find((s) => s.id === id);
    return found ? `${found.name} v${found.version}` : id;
  }

  function formatDate(iso: string) {
    return new Date(iso).toLocaleString();
  }

  function openCreate() {
    createForm.value = { name: '', description: '', basedOnId: undefined, schema: '{}' };
    createError.value = '';
    showCreateModal.value = true;
  }

  function branchFrom(s: FormSchema) {
    createForm.value = { name: s.name, description: '', basedOnId: s.id, schema: s.schema };
    createError.value = '';
    showCreateModal.value = true;
  }

  async function submitCreate() {
    if (!createForm.value.name.trim()) {
      createError.value = 'Name is required.';
      return;
    }
    creating.value = true;
    createError.value = '';
    try {
      const { data } = await axios.post<FormSchema>('/api/form-schemas', {
        name: createForm.value.name.trim(),
        description: createForm.value.description || undefined,
        basedOnId: createForm.value.basedOnId || undefined,
        schema: createForm.value.schema,
      });
      showCreateModal.value = false;
      router.push({ name: 'FormDesigner', params: { id: data.id } });
    } catch {
      createError.value = 'Failed to create schema.';
    } finally {
      creating.value = false;
    }
  }

  function editSchema(s: FormSchema) {
    router.push({ name: 'FormDesigner', params: { id: s.id } });
  }

  async function confirmDelete(s: FormSchema) {
    if (!confirm(`Delete "${s.name}" v${s.version}? This cannot be undone.`)) return;
    try {
      await axios.delete(`/api/form-schemas/${s.id}`);
      await fetchSchemas();
    } catch {
      alert('Failed to delete schema.');
    }
  }

  onMounted(fetchSchemas);
</script>

<style scoped>
  .schemas-page {
    max-width: 1100px;
    margin: 0 auto;
  }

  .modal-backdrop-custom {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.45);
    z-index: 1050;
    display: flex;
    align-items: center;
    justify-content: center;
  }

  .modal-dialog-custom {
    background: #fff;
    border-radius: 6px;
    width: 440px;
    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.2);
    overflow: hidden;
  }

  .modal-header-custom {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0.75rem 1rem;
    background: #f8f9fa;
    border-bottom: 1px solid #dee2e6;
  }

  .modal-body-custom {
    padding: 1.25rem 1rem;
  }

  .modal-footer-custom {
    display: flex;
    justify-content: flex-end;
    gap: 0.5rem;
    padding: 0.75rem 1rem;
    border-top: 1px solid #dee2e6;
    background: #f8f9fa;
  }
</style>
