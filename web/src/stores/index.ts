import { createPinia } from 'pinia';
import { App } from 'vue';
import { useRuntimeConfigStore } from './RuntimeConfigStore';

const pinia = createPinia();

export function registerPinia(app: App) {
  app.use(pinia);
}

export async function initializePinia() {
  const runtimeConfigStore = useRuntimeConfigStore(pinia);
  await runtimeConfigStore.loadConfig();
}

export default pinia;

export { useAuthStore } from './AuthStore';
export { useLayoutStore } from './LayoutStore';
export { useLocationStore } from './LocationStore';
export { useWizardDataStore } from './WizardDataStore';
export { useApplicationStore } from './PreviousApplicationStore';
export { useRuntimeConfigStore } from './RuntimeConfigStore';
