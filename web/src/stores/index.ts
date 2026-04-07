import { createPinia } from 'pinia';
import { App } from 'vue';

const pinia = createPinia();

export function registerPinia(app: App) {
  app.use(pinia);
}

export default pinia;

export { useAuthStore } from './AuthStore';
export { useLayoutStore } from './LayoutStore';
export { useLocationStore } from './LocationStore';
export { useApplicationStore } from './PreviousApplicationStore';
