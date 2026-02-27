import { useAuthStore, useLayoutStore } from '@/stores';
import AuthService from '@/services/AuthService';
import HttpService from '@/services/HttpService';
import type { RouteRecordRaw } from 'vue-router';
import { createRouter, createWebHistory } from 'vue-router';

/**
 * Auth guard that checks authentication before navigating to protected routes.
 *  1. Calls the backend /api/auth/user endpoint
 *  2. If authenticated, stores user info and allows navigation
 *  3. If 401, HttpService interceptor redirects to /api/auth/login (Keycloak SSO)
 */
async function authGuard(to: any, _from: any, next: any) {
  const authStore = useAuthStore();

  // If we already have user info, allow navigation
  if (authStore.isAuthenticated) {
    next();
    return;
  }

  try {
    authStore.setLoading(true);

    const httpService = new HttpService(import.meta.env.BASE_URL);
    const authService = new AuthService(httpService);
    const userInfo = await authService.getUserInfo();

    authStore.setUserInfo(userInfo);
    authStore.setLoading(false);

    if (userInfo.isAuthenticated) {
      next();
    } else {
      // Not authenticated — redirect to backend login endpoint.
      // Use the intended destination as returnUrl so the user lands
      // on the correct page after Keycloak SSO completes.
      redirectToLogin(to.fullPath);
    }
  } catch {
    authStore.setLoading(false);
    // getUserInfo returned 401 — redirect to backend login.
    // Pass the intended destination so the user returns here after SSO.
    redirectToLogin(to.fullPath);
  }
}

/**
 * Redirects the browser to the backend login endpoint which triggers
 * the Keycloak OIDC challenge. After successful SSO, the backend
 * redirects to the returnUrl.
 */
function redirectToLogin(destinationPath: string) {
  const base = import.meta.env.BASE_URL.replace(/\/$/, '');
  const apiBase = `${base}/api`;
  console.log(
    'base',
    base,
    'apiBase',
    apiBase,
    'destinationPath',
    destinationPath
  );
  window.location.replace(
    `${apiBase}/auth/login?returnUrl=${base}${destinationPath}`
  );
}

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    name: 'LandingPage',
    component: () => import('../views/landing/LandingPage.vue'),
    meta: { requiresAuth: false },
  },
  {
    path: '/represent-someone-who-died',
    name: 'RepresentSomeoneWhoDied',
    component: () => import('../views/landing/LandingPage.vue'),
    meta: {
      navHeader: 'Represent Someone Who Died',
      requiresAuth: false,
    },
  },
  {
    path: '/about',
    name: 'About',
    component: () => import('../views/about/AboutView.vue'),
    meta: { requiresAuth: false },
  },
  {
    path: '/previous-activity',
    name: 'PreviousActivity',
    component: () => import('../views/PreviousActivity.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/get-started',
    name: 'NewApplication',
    component: () => import('../views/NewApplication.vue'),
    meta: { requiresAuth: true },
  },
];

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
});

router.beforeEach(async (to, _from, next) => {
  const layoutStore = useLayoutStore();

  // Update nav header from route meta
  if (to.meta.navHeader && typeof to.meta.navHeader === 'string') {
    layoutStore.setNavHeader(to.meta.navHeader);
  } else {
    layoutStore.resetNavHeader();
  }

  // Apply auth guard only to protected routes
  if (to.meta.requiresAuth) {
    await authGuard(to, _from, next);
  } else {
    next();
  }
});

export default router;
