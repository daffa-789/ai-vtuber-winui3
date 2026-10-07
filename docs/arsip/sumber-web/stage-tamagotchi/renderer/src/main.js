import { createApp } from "vue";
import { createPinia } from "pinia";
import App from "./App.jsx";
import "./style.css";
createApp(App).use(createPinia()).mount("#app");
