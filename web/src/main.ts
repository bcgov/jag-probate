import 'bootstrap-vue-next/dist/bootstrap-vue-next.css';
import { createBootstrap } from 'bootstrap-vue-next/plugins/createBootstrap';
import 'bootstrap/dist/css/bootstrap.css';
import { createApp } from 'vue';
import App from './App.vue';
import './assets/main.css';
import registerFontAwesomeAndLibrary from './plugins/FontAwesome';
import router from './router';
import { registerServices } from './services';
import { initializePinia, installWizardBridge, registerPinia } from './stores';
import './styles/backdrop.scss';
import './styles/index.scss';
import { registerRelatedPeopleGlobal } from './utils/relatedPeople';

async function bootstrap() {
  // Must run before any CHEFS <chefs-form-viewer> can initialize and call it.
  registerRelatedPeopleGlobal();

  const app = createApp(App);

  // Add Pinia store with extensible registration function
  registerPinia(app);
  // Wizard state + window.wizard* bridge must exist before any CHEFS form
  // script runs, independent of whether the wizard sidebar is mounted.
  installWizardBridge();
  await initializePinia();
  registerFontAwesomeAndLibrary(app);

  // Register services (HttpService, AuthService) as provide/inject singletons
  registerServices(app);

  // Add Vue Router
  app.use(router);

  // Register Boostrap-Vue-Next
  app.use(createBootstrap());

  app.mount('#app');
}

bootstrap();
