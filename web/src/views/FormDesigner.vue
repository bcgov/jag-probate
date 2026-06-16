<template>
  <div class="form-designer-page">
    <div class="d-flex align-items-center gap-3 mb-3 flex-wrap">
      <div>
        <h1 class="mb-0">
          Form Designer
          <span v-if="schemaName" class="text-muted fs-5 fw-normal ms-2">— {{ schemaName }}</span>
        </h1>
      </div>
      <div class="d-flex gap-2 ms-auto flex-wrap">
        <span v-if="statusMsg" :class="['small', statusError ? 'text-danger' : 'text-success']">
        {{ statusMsg }}
      </span>
        <router-link class="fdt-btn fdt-btn--secondary" :to="{ name: 'FormSchemaList' }">
          <i class="bi bi-arrow-left me-1"></i> All Schemas
        </router-link>
        <label class="fdt-btn fdt-btn--secondary" style="margin-bottom:0 !important;cursor:pointer">
          <span><i class="bi bi-upload me-1"></i> Import JSON</span>
          <input type="file" accept=".json" class="d-none" @change="loadSchemaFile" />
        </label>
        <div class="fdt-btn-group">
          <button class="fdt-btn fdt-btn--ghost" title="Undo (Ctrl+Z)" :disabled="!canUndo" @click="undoAction">
            <i class="bi bi-arrow-counterclockwise me-1"></i> Undo
          </button>
          <button class="fdt-btn fdt-btn--ghost" title="Redo (Ctrl+Y)" :disabled="!canRedo" @click="redoAction">
            <i class="bi bi-arrow-clockwise me-1"></i> Redo
          </button>
        </div>
        <button class="fdt-btn fdt-btn--save" :disabled="saving" @click="saveSchema">
          <i :class="saving ? 'bi bi-hourglass-split me-1' : 'bi bi-floppy me-1'"></i>
          {{ saving ? 'Saving…' : 'Save' }}
        </button>
        <button class="fdt-btn fdt-btn--primary" @click="openPreview">
          <i class="bi bi-eye me-1"></i> Preview
        </button>
        <button class="fdt-btn fdt-btn--export" @click="exportSchema">
          <i class="bi bi-download me-1"></i> Export JSON
        </button>
      </div>
      
    </div>
    <div ref="builderContainer" class="form-builder-container fdt-builder-scope"></div>
  </div>
</template>

<script setup lang="ts">
  import { Formio, Components } from '@formio/js';
  import { onMounted, onBeforeUnmount, ref } from 'vue';
  import { useRouter } from 'vue-router';
  import axios from 'axios';

  const props = defineProps<{ id?: string }>();
  const router = useRouter();
  const schemaName = ref('');
  const saving = ref(false);
  const statusMsg = ref('');
  const statusError = ref(false);

  // Map CHEFS custom component types to their closest standard formio equivalents.
  // This prevents "Unknown component" warnings when loading CHEFS schemas.
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
      if (Components.components[chefsType]) continue; // already registered
      const Base = Components.components[baseType];
      if (!Base) continue;
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const Stub = class extends (Base as any) {
        static schema(...extend: object[]) {
          return Base.schema({ type: chefsType }, ...extend);
        }
        static get builderInfo() {
          return { ...Base.builderInfo, title: chefsType, type: chefsType, group: 'basic' };
        }
      };
      Object.defineProperty(Stub, 'name', { value: chefsType });
      Components.addComponent(chefsType, Stub);
    }
  }

  registerChefsStubs();

  const builderContainer = ref<HTMLElement | null>(null);
  const loadError = ref('');
  const canUndo = ref(false);
  const canRedo = ref(false);

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let builder: any = null;

  // Manual undo/redo history — formio has no built-in support
  const history: string[] = [];  // JSON snapshots
  let historyIndex = -1;
  let applyingHistory = false;  // prevent change event from recording during restore
  const MAX_HISTORY = 50;

  function recordSnapshot(schema: object) {
    if (applyingHistory) return;
    const snap = JSON.stringify(schema);
    // Drop anything ahead of current index (new branch)
    history.splice(historyIndex + 1);
    history.push(snap);
    if (history.length > MAX_HISTORY) history.shift();
    historyIndex = history.length - 1;
    canUndo.value = historyIndex > 0;
    canRedo.value = false;
  }

  async function restoreSnapshot(snap: string) {
    applyingHistory = true;
    await initBuilder(JSON.parse(snap), false);
    applyingHistory = false;
    canUndo.value = historyIndex > 0;
    canRedo.value = historyIndex < history.length - 1;
  }

  async function initBuilder(initialSchema: object = {}, record = true) {
    if (!builderContainer.value) return;
    builder?.destroy?.();
    builderContainer.value.innerHTML = '';

    // Inject formio CSS once into <head>
    if (!document.getElementById('fdt-formio-style')) {
      const link = document.createElement('link');
      link.id = 'fdt-formio-style';
      link.rel = 'stylesheet';
      link.href = new URL('@formio/js/dist/formio.full.min.css', import.meta.url).href;
      document.head.appendChild(link);
    }

    builder = await Formio.builder(builderContainer.value, initialSchema, {});

    if (record) recordSnapshot(builder.schema);

    // Capture every change into the history
    builder.on('change', (schema: object) => {
      recordSnapshot(schema);
    });
  }

  function loadSchemaFile(event: Event) {
    loadError.value = '';
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = async (e) => {
      try {
        const parsed = JSON.parse(e.target?.result as string);
        history.splice(0);
        historyIndex = -1;
        await initBuilder(parsed);
      } catch {
        loadError.value = 'Invalid JSON file.';
      }
    };
    reader.readAsText(file);
    (event.target as HTMLInputElement).value = '';
  }

  async function saveSchema() {
    if (!builder || !props.id) return;
    saving.value = true;
    statusMsg.value = '';
    statusError.value = false;
    try {
      await axios.put(`/api/form-schemas/${props.id}`, {
        schema: JSON.stringify(builder.schema),
      });
      statusMsg.value = 'Saved!';
      setTimeout(() => (statusMsg.value = ''), 3000);
    } catch {
      statusMsg.value = 'Save failed.';
      statusError.value = true;
    } finally {
      saving.value = false;
    }
  }

  async function undoAction() {
    if (historyIndex <= 0) return;
    historyIndex--;
    await restoreSnapshot(history[historyIndex]);
  }

  async function redoAction() {
    if (historyIndex >= history.length - 1) return;
    historyIndex++;
    await restoreSnapshot(history[historyIndex]);
  }

  function onKeyDown(e: KeyboardEvent) {
    const ctrl = e.ctrlKey || e.metaKey;
    // Don't intercept when focus is inside an input/textarea
    const tag = (e.target as HTMLElement)?.tagName?.toLowerCase();
    if (tag === 'input' || tag === 'textarea' || tag === 'select') return;
    if (ctrl && !e.shiftKey && e.key === 'z') {
      e.preventDefault();
      undoAction();
    } else if (ctrl && (e.key === 'y' || (e.shiftKey && e.key === 'z'))) {
      e.preventDefault();
      redoAction();
    }
  }

  function openPreview() {
    if (!builder) return;
    sessionStorage.setItem('formio-preview-schema', JSON.stringify(builder.schema));
    const route = router.resolve({ name: 'FormPreview' });
    window.open(route.href, '_blank');
  }

  function exportSchema() {
    if (!builder) return;
    const json = JSON.stringify(builder.schema, null, 2);
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'form-schema.json';
    a.click();
    URL.revokeObjectURL(url);
  }

  onMounted(async () => {
    registerChefsStubs();
    window.addEventListener('keydown', onKeyDown);
    if (props.id) {
      try {
        const { data } = await axios.get(`/api/form-schemas/${props.id}`);
        schemaName.value = `${data.name} v${data.version}`;
        await initBuilder(JSON.parse(data.schema));
      } catch {
        await initBuilder();
      }
    } else {
      await initBuilder();
    }
  });

  onBeforeUnmount(() => {
    builder?.destroy?.();
    window.removeEventListener('keydown', onKeyDown);
  });
</script>

<style>
  /* Scope formio CSS inside the builder container only */
  @import url('https://cdn.jsdelivr.net/npm/bootstrap-icons@1/font/bootstrap-icons.min.css');
</style>

<style>
  .fdt-builder-scope {
    /* All formio styles injected here via JS are naturally scoped to this div.
       We load formio CSS manually via JS to keep it out of the global scope. */
  }
</style>

<style scoped>
  .form-designer-page {
    padding: 1.5rem;
  }

  .form-builder-container {
    min-height: 500px;
  }

  .fdt-btn--ghost {
    color: #495057 !important;
    border-color: #ced4da !important;
    background: #fff !important;
  }
  .fdt-btn--ghost:hover {
    background: #f1f3f5 !important;
    border-color: #adb5bd !important;
  }
  .fdt-btn--ghost:disabled {
    opacity: 0.45 !important;
    cursor: not-allowed !important;
  }

  .fdt-btn-group {
    display: inline-flex !important;
    gap: 2px !important;
  }

  .fdt-btn i {
    font-size: 0.9rem !important;
    line-height: 1 !important;
    vertical-align: middle !important;
  }

  /* Toolbar buttons — isolated from formio/Bootstrap .btn classes */
  .fdt-btn {
    display: inline-flex !important;
    align-items: center !important;
    padding: 0.3rem 0.85rem !important;
    font-size: 0.875rem !important;
    font-weight: 500 !important;
    line-height: 1.5 !important;
    border-radius: 4px !important;
    border: 1px solid transparent !important;
    cursor: pointer !important;
    text-decoration: none !important;
    white-space: nowrap !important;
    transition: background-color 0.15s, border-color 0.15s, color 0.15s !important;
    font-family: inherit !important;
  }

  .fdt-btn--secondary {
    color: #6c757d !important;
    border-color: #6c757d !important;
    background: transparent !important;
  }
  .fdt-btn--secondary:hover {
    background: #6c757d !important;
    color: #fff !important;
  }

  .fdt-btn--primary {
    color: #fff !important;
    background: #0d4e9c !important;
    border-color: #0d4e9c !important;
  }
  .fdt-btn--primary:hover {
    background: #0a3d7a !important;
    border-color: #0a3d7a !important;
  }

  .fdt-btn--save {
    color: #fff !important;
    background: #198754 !important;
    border-color: #198754 !important;
  }
  .fdt-btn--save:hover:not(:disabled) {
    background: #146c43 !important;
    border-color: #146c43 !important;
  }
  .fdt-btn--save:disabled {
    opacity: 0.65 !important;
    cursor: not-allowed !important;
  }

  .fdt-btn--export {
    color: #198754 !important;
    border-color: #198754 !important;
    background: transparent !important;
  }
  .fdt-btn--export:hover {
    background: #198754 !important;
    color: #fff !important;
  }
</style>
