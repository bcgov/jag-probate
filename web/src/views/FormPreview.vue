<template>
  <div class="preview-page">
    <div class="preview-toolbar">
      <strong>Form Preview</strong>
      <button class="btn btn-sm btn-outline-secondary" @click="window.close()">Close</button>
    </div>
    <div class="preview-body">
      <div v-if="error" class="alert alert-danger">{{ error }}</div>
      <div ref="previewContainer"></div>
    </div>
  </div>
</template>

<script setup lang="ts">
  import { Formio, Components } from '@formio/js';
  import { onMounted, onBeforeUnmount, ref } from 'vue';

  const previewContainer = ref<HTMLElement | null>(null);
  const error = ref('');
  const window = globalThis.window;

  // Register the same CHEFS stubs so unknown-component warnings don't appear in preview
  const chefsTypeMap: Record<string, string> = {
    simpletextfield: 'textfield',
    simpletextareaadvanced: 'textarea',
    simplebuttonadvanced: 'button',
    simplecols2: 'columns',
    simplecols3: 'columns',
    simpledatetimeadvanced: 'datetime',
    simpleradioadvanced: 'radio',
    simpleselectadvanced: 'select',
    calendar: 'datetime',
    phoneNumber: 'phoneNumber',
    well: 'panel',
  };

  function registerChefsStubs() {
    for (const [chefsType, baseType] of Object.entries(chefsTypeMap)) {
      if (Components.components[chefsType]) continue;
      const Base = Components.components[baseType];
      if (!Base) continue;
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const Stub = class extends (Base as any) {
        static schema(...extend: object[]) {
          return Base.schema({ type: chefsType }, ...extend);
        }
        static get builderInfo() {
          return { ...Base.builderInfo, title: chefsType, type: chefsType };
        }
      };
      Object.defineProperty(Stub, 'name', { value: chefsType });
      Components.addComponent(chefsType, Stub);
    }
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let form: any = null;

  onMounted(async () => {
    registerChefsStubs();
    const raw = sessionStorage.getItem('formio-preview-schema');
    if (!raw) {
      error.value = 'No schema found. Open this page from the Form Designer.';
      return;
    }
    try {
      const schema = JSON.parse(raw);
      if (previewContainer.value) {
        form = await Formio.createForm(previewContainer.value, schema, {});
      }
    } catch {
      error.value = 'Failed to parse form schema.';
    }
  });

  onBeforeUnmount(() => {
    form?.destroy?.();
  });
</script>

<style>
  @import url('https://cdn.jsdelivr.net/npm/bootstrap-icons@1/font/bootstrap-icons.min.css');
  @import '@formio/js/dist/formio.full.min.css';
</style>

<style scoped>
  .preview-page {
    min-height: 100vh;
    display: flex;
    flex-direction: column;
  }

  .preview-toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0.6rem 1.25rem;
    background: #f8f9fa;
    border-bottom: 1px solid #dee2e6;
    position: sticky;
    top: 0;
    z-index: 10;
  }

  .preview-body {
    padding: 1.5rem;
    margin: 0;
    width: 100%;
  }
</style>
