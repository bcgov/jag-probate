import AuthService from '@/services/AuthService';
import HttpService from '@/services/HttpService';
import { useAuthStore, useLayoutStore } from '@/stores';
import type { RouteRecordRaw } from 'vue-router';
import { createRouter, createWebHistory } from 'vue-router';

/**
 * Auth guard that checks authentication before navigating to protected routes.
 *  1. Calls the backend /api/auth/user endpoint
 *  2. If authenticated, stores user info and allows navigation
 *  3. If 401, redirects to /<base>/api/auth/login (Keycloak SSO)
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
      redirectToLogin(to);
    }
  } catch {
    authStore.setLoading(false);
    // getUserInfo returned 401 — redirect to backend login.
    // Pass the intended destination so the user returns here after SSO.
    redirectToLogin(to);
  }
}

/**
 * Redirects the browser to the backend login endpoint which triggers
 * the Keycloak OIDC challenge. After successful SSO, the backend
 * redirects to the returnUrl.
 */
function redirectToLogin(to: any) {
  const configuredBase = import.meta.env.BASE_URL.replace(/\/$/, '');

  // Detect the actual app sub-path prefix by comparing the live pathname with
  // the route's matched path. Vue Router always reports `to.path` without the
  // base, so when the browser is at /probate/previous-activity and the route
  // path is /previous-activity, the base is /probate.
  // This is robust even when BASE_URL is misconfigured (e.g. set to "/").
  const routePath = to.fullPath;
  const currentPath = window.location.pathname;

  const base =
    currentPath !== routePath && currentPath.endsWith(routePath)
      ? currentPath.slice(0, currentPath.length - routePath.length)
      : configuredBase;

  const apiBase = `${base}/api`;
  // Pass just the route path as returnUrl; AuthController prepends the base
  // path (e.g. /probate) from the X-Base-Href header set by nginx.
  window.location.assign(
    `${apiBase}/auth/login?returnUrl=${encodeURIComponent(routePath)}`
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
      navHeader: 'Probate Application',
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
    component: () => import('../views/ApplicationForm.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/terms',
    name: 'terms',
    component: () => import('@/views/TermsAndConditions.vue'),
    meta: { requiresAuth: false },
  },
  {
    path: '/resume/:id',
    name: 'ResumeApplication',
    component: () => import('@/views/ApplicationForm.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/app-preview',
    name: 'ApplicationManager',
    component: () => import('@/views/ApplicationManager.vue'),
    meta: { requiresAuth: false },
  },
  {
    path: '/error',
    name: 'Error',
    component: () => import('@/views/ErrorView.vue'),
    meta: { requiresAuth: false },
  },
  {
    path: '/404',
    redirect: { name: 'Error', query: { status: '404' } },
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: () => ({
      name: 'Error',
      query: {
        status: '404',
        details: `The requested path could not be found.`,
      },
    }),
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
