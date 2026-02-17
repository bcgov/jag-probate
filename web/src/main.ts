import 'bootstrap-vue-next/dist/bootstrap-vue-next.css';
import { createBootstrap } from 'bootstrap-vue-next/plugins/createBootstrap';
import 'bootstrap/dist/css/bootstrap.css';
import { createApp } from 'vue';
import App from './App.vue';
import './assets/main.css';
import registerFontAwesomeAndLibrary from './plugins/FontAwesome';
import router from './router';
import { registerServices } from './services';
import { registerPinia } from './stores';
import './styles/backdrop.scss';
import './styles/index.scss';

const app = createApp(App);

// Add Pinia store with extensible registration function
registerPinia(app);
registerFontAwesomeAndLibrary(app);

// Register services (HttpService, AuthService) as provide/inject singletons
registerServices(app);

// Add Vue Router
app.use(router);

// Register Boostrap-Vue-Next
app.use(createBootstrap());

app.mount('#app');
