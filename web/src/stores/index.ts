import { App } from 'vue';
import { pinia } from './pinia';
import { useRuntimeConfigStore } from './RuntimeConfigStore';

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
export { useWizardStateStore } from './WizardStateStore';
export { installWizardBridge } from './wizardBridge';
export { useApplicationStore } from './PreviousApplicationStore';
export { useRuntimeConfigStore } from './RuntimeConfigStore';
