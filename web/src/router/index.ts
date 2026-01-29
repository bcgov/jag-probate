import { useLayoutStore } from '@/stores';
import type { RouteRecordRaw } from 'vue-router';
import { createRouter, createWebHistory } from 'vue-router';

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    name: 'LandingPage',
    component: () => import('../views/landing/LandingPage.vue'),
  },
  {
    path: '/represent-someone-who-died',
    name: 'RepresentSomeoneWhoDied',
    component: () => import('../views/landing/LandingPage.vue'),
    meta: {
      navHeader: 'Represent Someone Who Died',
    },
  },
  {
    path: '/about',
    name: 'About',
    component: () => import('../views/about/AboutView.vue'),
  },
  {
    path: '/probate-form',
    name: 'ProbateForm',
    component: () => import('../views/forms/ProbateFormPage.vue'),
    meta: {
      navHeader: 'Probate Application',
    },
  },
];

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
});

router.beforeEach((to, _from, next) => {
  const layoutStore = useLayoutStore();
  if (to.meta.navHeader && typeof to.meta.navHeader === 'string') {
    layoutStore.setNavHeader(to.meta.navHeader);
  } else {
    layoutStore.resetNavHeader();
  }
  next();
});

export default router;
