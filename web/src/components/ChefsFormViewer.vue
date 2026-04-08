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
  import { computed, inject, onMounted, ref } from 'vue';

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
  }

  const props = withDefaults(defineProps<Props>(), {
    chefsBaseUrl: 'https://submit.digital.gov.bc.ca/app',
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
      if (props.submissionId) {
        el.setAttribute('submission-id', props.submissionId);
        el.setAttribute('read-only', 'false'); // allow editing resumed draft
      }

      container.appendChild(el);

      el.addEventListener('formio:submitDone', handleSubmitDone);

      el.addEventListener('formio:error', (e: CustomEvent) => {
        emit('form-error', e.detail);
      });
      el.load();

      state.value = 'ready';
    } catch (err: any) {
      errorMessage.value =
        err?.response?.data?.message ?? err?.message ?? 'Unknown error.';
      state.value = 'error';
    }
  }

  async function handleSubmitDone(e: CustomEvent) {
    const submission = e.detail?.submission;

    const chefsSubmissionId = props.submissionId ?? submission?.id;
    const createdBy = chefsToken.value?.preferred_username;
    const applicantName = chefsToken.value?.display_name;
    const status = submission?.submission?.state;
    const lastUpdatedAt = submission?.updatedAt;
    const lastFiledAt = status === 'submitted' ? submission?.updatedAt : null;

    await chefsService.upsertSubmission({
      chefsSubmissionId,
      createdBy,
      applicantName,
      status,
      lastUpdatedAt,
      lastFiledAt,
    });

    emit('submitted', chefsSubmissionId);
  }

  // ── Lifecycle ─────────────────────────────────────────────────────────────
  onMounted(() => {
    initForm();
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
