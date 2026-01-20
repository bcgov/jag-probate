import { createApp } from "vue";
import App from "./App.vue";
import "./assets/main.css";
import router from "./router";
import { registerPinia } from "./stores";
import "./styles/index.scss";

const app = createApp(App);

// Add Pinia store with extensible registration function
registerPinia(app);

// Add Vue Router
app.use(router);

app.mount("#app");
