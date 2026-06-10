<template>
  <div v-if="visible" class="modal d-block" tabindex="-1" role="dialog">
    <div class="modal-dialog modal-xl" role="document">
      <div class="modal-content">
        <!-- Header -->
        <div class="modal-header">
          <h5 class="modal-title">{{ title }}</h5>
          <button type="button" class="btn-close" aria-label="Close" @click="close" />
        </div>

        <!-- Body -->
        <div class="modal-body p-0" style="height: 80vh">
          <!-- Loading -->
          <div
            v-if="isLoading"
            class="d-flex flex-column justify-content-center align-items-center h-100 gap-3"
          >
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">Generating PDF…</span>
            </div>
            <p class="text-muted mb-0">Generating PDF…</p>
          </div>

          <!-- Error -->
          <div v-else-if="errorMessage" class="alert alert-danger m-3" role="alert">
            <strong>Failed to generate PDF.</strong> {{ errorMessage }}
          </div>

          <!-- PDF via URL -->
          <iframe
            v-else-if="pdfUrl"
            :src="pdfUrl"
            class="w-100 h-100 border-0"
            title="PDF Preview"
          />
        </div>

        <!-- Footer -->
        <div class="modal-footer">
          <a
            v-if="pdfUrl"
            :href="pdfUrl"
            :download="pdfFileName"
            class="btn btn-primary"
          >
            <span aria-hidden="true">&#8659;</span>
            Download PDF
          </a>
          <button type="button" class="btn btn-secondary" @click="close">Close</button>
        </div>
      </div>
    </div>
  </div>

  <!-- Backdrop -->
  <div v-if="visible" class="modal-backdrop fade show" />
</template>

<script setup lang="ts">
  import ReportService from '@/services/ReportService';
  import { inject, ref, watch } from 'vue';

  interface Props {
    visible: boolean;
    /** The raw CHEFS form submission data to generate the PDF from. */
    submissionData: unknown;
    /** Logical template key, e.g. "P1". */
    templateKey: string;
    title?: string;
  }

  const props = withDefaults(defineProps<Props>(), {
    title: 'PDF Preview',
  });

  const emit = defineEmits<{
    (e: 'close'): void;
  }>();

  const reportService = inject<ReportService>('reportService')!;

  const isLoading = ref(false);
  const errorMessage = ref('');
  const pdfUrl = ref<string | null>(null);
  const pdfFileName = ref('document.pdf');

  async function generatePdf() {
    pdfUrl.value = null;
    isLoading.value = true;
    errorMessage.value = '';

    try {
      const result = await reportService.generateReport({
        templateKey: props.templateKey,
        submissionData: props.submissionData,
      });
      pdfUrl.value = result.url;
      pdfFileName.value = result.fileName;
    } catch (err: any) {
      errorMessage.value =
        err?.response?.data?.detail ??
        err?.response?.data?.message ??
        err?.message ??
        'An unknown error occurred.';
    } finally {
      isLoading.value = false;
    }
  }

  function close() {
    if (pdfUrl.value) {
      URL.revokeObjectURL(pdfUrl.value);
    }
    pdfUrl.value = null;
    emit('close');
  }

  watch(
    () => props.visible,
    (isVisible) => {
      if (isVisible) {
        generatePdf();
      } else {
        if (pdfUrl.value) {
          URL.revokeObjectURL(pdfUrl.value);
        }
        pdfUrl.value = null;
      }
    }
  );
</script>
