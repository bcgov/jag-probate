import { createRouter, createWebHistory } from "vue-router";
import type { RouteRecordRaw } from "vue-router";
import HomeView from "../views/home/HomeView.vue";

const routes: RouteRecordRaw[] = [
  {
    path: "/",
    name: "Home",
    component: HomeView,
  },
  {
    path: "/cases",
    name: "Cases",
    component: () => import("../views/cases/ListView.vue"),
  },
  {
    path: "/cases/new",
    name: "CreateCase",
    component: () => import("../views/cases/CreateView.vue"),
  },
  {
    path: "/cases/:id",
    name: "CaseDetail",
    component: () => import("../views/cases/DetailView.vue"),
    props: true,
  },
  {
    path: "/about",
    name: "About",
    component: () => import("../views/about/AboutView.vue"),
  },
];

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
});

export default router;
