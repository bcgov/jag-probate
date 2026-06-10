/// <reference types="vite/client" />

interface ImportMeta {
  readonly env: ImportMetaEnv & { readonly BASE_URL: string };
}

interface Window {
  staticBaseUrl: string;
  probate?: {
    generatePdf: (
      templateKey: string,
      submissionData: unknown
    ) => Promise<string>;
    downloadPdf: (
      instance: unknown,
      data: unknown,
      templateKey: string,
      iframeTitle: string,
      fileName: string
    ) => Promise<void>;
    previewPdf: (
      instance: unknown,
      data: unknown,
      currentStep: string,
      targetStep: string,
      templateKey: string,
      iframeTitle: string
    ) => Promise<void>;
  };
}

declare module '*.vue' {
  import type { DefineComponent } from 'vue';
  // eslint-disable-next-line @typescript-eslint/no-empty-object-type
  const component: DefineComponent<{}, {}, unknown>;
  export default component;
}
