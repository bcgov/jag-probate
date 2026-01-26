import { createPinia } from 'pinia';
import { App } from 'vue';

const pinia = createPinia();

export function registerPinia(app: App) {
  app.use(pinia);
}

export default pinia;

export { useLayoutStore } from './LayoutStore';
