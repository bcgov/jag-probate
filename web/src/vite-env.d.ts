/// <reference types="vite/client" />

import type CourtLocationService from './services/CourtLocationService';

interface ImportMeta {
  readonly env: ImportMetaEnv & { readonly BASE_URL: string };
}

declare global {
  interface Window {
    staticBaseUrl: string;
    courtLocationService?: CourtLocationService;
  }
}

declare module '*.vue' {
  import type { DefineComponent } from 'vue';
  // eslint-disable-next-line @typescript-eslint/no-empty-object-type
  const component: DefineComponent<{}, {}, unknown>;
  export default component;
}
