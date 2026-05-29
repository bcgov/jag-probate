<template>
  <div class="chefs-form-wrapper">
    <!-- Loading state -->
    <div v-if="state === 'loading'" class="chefs-form-loading">
      <div class="spinner-border text-primary" role="status">
        <span class="sr-only">Loading form…</span>
      </div>
      <p class="mt-2 text-muted">Loading form…</p>
    </div>

    <!-- Error state -->
    <div v-else-if="state === 'error'" class="alert alert-danger" role="alert">
      <strong>Failed to load the form.</strong>
      {{ errorMessage }}
      <div class="mt-2">
        <button class="btn btn-sm btn-outline-danger" @click="initForm">
          Retry
        </button>
      </div>
    </div>

    <!-- Mount point -->
    <div ref="chefsContainer" class="chefs-form-viewer"></div>
  </div>
</template>

<script setup lang="ts">
  import ChefsService from '@/services/ChefsService';
  import { useAuthStore } from '@/stores';
  import { extractTokenPayload } from '@/utils/claims';
  import { computed, inject, onMounted, onUnmounted, ref } from 'vue';

  const authStore = useAuthStore();
  const chefsToken = computed(() => {
    if (!authStore.userInfo) return {};
    return extractTokenPayload(authStore.userInfo);
  });

  // ── Props ─────────────────────────────────────────────────────────────────
  interface Props {
    /** Logical form key resolved server-side to a CHEFS form GUID. */
    formKey: string;
    /** Base URL for the CHEFS service. */
    chefsBaseUrl?: string;
    submissionId?: string;
    /** Auto-save interval in milliseconds. 0 disables auto-save. Default: 3000. */
    autoSaveThrottle?: number;
  }

  const props = withDefaults(defineProps<Props>(), {
    chefsBaseUrl: 'https://submit.digital.gov.bc.ca/app',
    autoSaveThrottle: 3000,
  });

  // ── Emits ─────────────────────────────────────────────────────────────────
  const emit = defineEmits<{
    (e: 'submitted', submissionId: string): void;
    (e: 'form-error', error: unknown): void;
  }>();

  // ── State ─────────────────────────────────────────────────────────────────
  type ViewState = 'loading' | 'ready' | 'error';

  const state = ref<ViewState>('loading');
  const errorMessage = ref('');
  const chefsContainer = ref<HTMLElement | null>(null);

  /**
   * The CHEFS submission ID currently loaded in the form.
   * CHEFS creates a new submission ID on every save (POST-only),
   * so this value changes after each save.
   */
  const currentSubmissionId = ref<string | undefined>(props.submissionId);

  /**
   * Our internal DB row ID – loaded from sessionStorage on resume,
   * or set after the first upsert on a new session.
   */
  const currentDbId = ref<number | undefined>(
    sessionStorage.getItem('resumeDbId')
      ? Number(sessionStorage.getItem('resumeDbId'))
      : undefined
  );

  /** Whether this submission is already submitted (read-only, no auto-save). */
  const isSubmitted = sessionStorage.getItem('resumeStatus') === 'submitted';

  const chefsService = inject<ChefsService>('chefsService')!;

  // ── Script loader ─────────────────────────────────────────────────────────
  function loadWebComponentScript(baseUrl: string): Promise<void> {
    const scriptSrc = `${baseUrl}/embed/chefs-form-viewer.min.js`;
    if (
      document.querySelector<HTMLScriptElement>(`script[src="${scriptSrc}"]`)
    ) {
      return Promise.resolve();
    }
    return new Promise((resolve, reject) => {
      const script = document.createElement('script');
      script.src = scriptSrc;
      script.onload = () => resolve();
      script.onerror = () =>
        reject(new Error(`Failed to load CHEFS script from ${scriptSrc}`));
      document.head.appendChild(script);
    });
  }

  // ── Core init ─────────────────────────────────────────────────────────────
  async function initForm() {
    state.value = 'loading';
    errorMessage.value = '';

    try {
      const { token, formId, baseUrl } = await chefsService.getAuthToken(
        props.formKey
      );
      const resolvedBaseUrl = baseUrl || props.chefsBaseUrl;

      await loadWebComponentScript(resolvedBaseUrl);

      const container = chefsContainer.value;
      if (!container) throw new Error('Mount point not found');

      container.innerHTML = '';

      window.staticBaseUrl = `${window.location.origin}${import.meta.env.BASE_URL}`;

      const el = document.createElement('chefs-form-viewer') as any;
      el.setAttribute('form-id', formId);
      el.setAttribute('auth-token', token);
      el.setAttribute('base-url', resolvedBaseUrl);
      el.setAttribute('isolate-styles', 'false');
      if (chefsToken.value && Object.keys(chefsToken.value).length > 0) {
        el.setAttribute('token', JSON.stringify(chefsToken.value));
      }
      if (currentSubmissionId.value) {
        el.setAttribute('submission-id', currentSubmissionId.value);
        el.setAttribute('read-only', isSubmitted ? 'true' : 'false');
      }

      container.appendChild(el);

      el.addEventListener('formio:submitDone', handleSubmitDone);
      el.addEventListener('formio:error', (e: CustomEvent) => {
        emit('form-error', e.detail);
      });
      el.load();

      // Listen for form changes and auto-save with debounce
      if (props.autoSaveThrottle > 0 && !isSubmitted) {
        setupAutoSave(el);
      }
      state.value = 'ready';
    } catch (err: any) {
      errorMessage.value =
        err?.response?.data?.message ?? err?.message ?? 'Unknown error.';
      state.value = 'error';
    }
  }

  // ── Shared save handler ───────────────────────────────────────────────────

  /**
   * Syncs a CHEFS submit event to our DB and navigates away.
   * Called when the user clicks the form's Submit button.
   */
  async function syncSave(newChefsId: string, submissionPayload: any) {
    // Always advance our tracked CHEFS ID to the latest one.
    currentSubmissionId.value = newChefsId;

    const createdBy = chefsToken.value?.preferred_username;
    const applicantName =
      submissionPayload?.submission?.data?.deceasedName ||
      submissionPayload?.data?.deceasedName ||
      '';
    const status = submissionPayload?.submission?.state;
    const now = new Date().toISOString();
    const lastUpdatedAt =
      submissionPayload?.updatedAt ?? submissionPayload?.modified ?? now;

    try {
      const response = await chefsService.upsertSubmission({
        id: currentDbId.value,
        chefsSubmissionId: newChefsId,
        createdBy,
        applicantName,
        status: status,
        lastUpdatedAt,
        lastFiledAt: status === 'submitted' ? now : null,
      });
      currentDbId.value = response?.id;
      if (response?.id) {
        sessionStorage.setItem('resumeDbId', String(response.id));
      }
    } catch (err) {
      console.error('[ChefsFormViewer] upsert failed:', err);
    }

    // Navigate back so the user can resume from Previous Activity.
    emit('submitted', newChefsId);
  }

  // ── Event handlers ────────────────────────────────────────────────────────

  async function handleSubmitDone(e: CustomEvent) {
    const submission = e.detail?.submission;
    const newId: string | undefined = submission?.id ?? submission?._id;
    if (!newId) return;

    await syncSave(newId, submission);
  }

  // ── Auto-save (change-driven with debounce + lock) ─────────────────────
  let debounceTimer: ReturnType<typeof setTimeout> | null = null;
  let isSaving = false;
  let pendingSave = false;
  let formReady = false;

  function setupAutoSave(el: any) {
    teardownAutoSave();
    // Skip initial load events — wait for form to settle, then start listening
    setTimeout(() => {
      formReady = true;
    }, 2000);
    el.addEventListener('formio:change', () => {
      if (formReady) scheduleAutoSave(el);
    });
  }

  function scheduleAutoSave(el: any) {
    // If currently saving, mark that another save is needed after lock expires
    if (isSaving) {
      pendingSave = true;
      return;
    }
    // Debounce: reset timer on every change, fire after 5s of quiet
    if (debounceTimer) clearTimeout(debounceTimer);
    debounceTimer = setTimeout(
      () => performAutoSave(el),
      props.autoSaveThrottle
    );
  }

  async function performAutoSave(el: any) {
    if (isSaving) {
      pendingSave = true;
      return;
    }
    isSaving = true;
    pendingSave = false;

    try {
      if (!el.formioInstance) return;

      const currentData =
        el.formioInstance?.submission?.data ||
        (typeof el.formioInstance.getValue === 'function'
          ? el.formioInstance.getValue()
          : el.formioInstance?.data) ||
        {};

      const submitUrl = el._resolveUrl?.('submit');
      if (!submitUrl) return;

      const authHeaders = el._buildAuthHeader?.(submitUrl) || {};
      const headers: Record<string, string> = {
        'Content-Type': 'application/json',
        ...authHeaders,
      };

      const res = await fetch(submitUrl, {
        method: 'POST',
        headers,
        body: JSON.stringify({
          submission: { data: currentData },
          draft: true,
        }),
      });

      if (res.ok) {
        const result = await res.json();
        const newId = result?.id;
        if (newId) {
          currentSubmissionId.value = newId;

          // Update sessionStorage so a browser refresh loads the latest draft
          sessionStorage.setItem('resumeSubmissionId', newId);
          if (currentDbId.value) {
            sessionStorage.setItem('resumeDbId', String(currentDbId.value));
          }

          try {
            const createdBy = chefsToken.value?.preferred_username;
            const applicantName = currentData?.deceasedName || '';
            const response = await chefsService.upsertSubmission({
              id: currentDbId.value,
              chefsSubmissionId: newId,
              createdBy,
              applicantName,
              status: 'draft',
              lastUpdatedAt: result?.updatedAt ?? new Date().toISOString(),
              lastFiledAt: null,
            });
            currentDbId.value = response?.id;
          } catch (err) {
            console.error('[ChefsFormViewer] auto-save upsert failed:', err);
          }
        }
      } else {
        console.warn('[ChefsFormViewer] auto-save draft response:', res.status);
      }
    } catch (err) {
      console.warn('[ChefsFormViewer] auto-save draft failed:', err);
    } finally {
      isSaving = false;
      // If changes occurred during save, schedule another save
      if (pendingSave) {
        pendingSave = false;
        scheduleAutoSave(el);
      }
    }
  }

  function teardownAutoSave() {
    if (debounceTimer) {
      clearTimeout(debounceTimer);
      debounceTimer = null;
    }
  }

  // ── Lifecycle ─────────────────────────────────────────────────────────────
  onMounted(() => {
    initForm();
  });

  onUnmounted(() => {
    teardownAutoSave();
  });
</script>

<style scoped>
  .chefs-form-wrapper {
    width: 100%;
  }

  .chefs-form-loading {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 3rem;
  }

  .chefs-form-viewer {
    display: block;
    width: 100%;
  }

  .sr-only {
    position: absolute;
    width: 1px;
    height: 1px;
    padding: 0;
    margin: -1px;
    overflow: hidden;
    clip: rect(0, 0, 0, 0);
    white-space: nowrap;
    border: 0;
  }

  .v-container {
    padding: 0;
    margin: 0 !important;
  }
</style>
