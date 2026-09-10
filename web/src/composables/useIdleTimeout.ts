import AuthService from '@/services/AuthService';
import HttpService from '@/services/HttpService';
import { useAuthStore } from '@/stores';
import { onBeforeUnmount, ref } from 'vue';

const DEFAULT_IDLE_TIMEOUT_SECONDS = 30 * 60;

// Only these represent genuine user interaction — background/automatic API
// calls (autosave, polling, etc.) must never reset the idle clock.
const ACTIVITY_EVENTS = [
  'mousemove',
  'mousedown',
  'keydown',
  'wheel',
  'touchstart',
  'scroll',
] as const;

// Ignore activity bursts more frequent than this to avoid excessive timer churn.
const ACTIVITY_THROTTLE_MS = 1000;

/**
 * Forces re-authentication after a period of user inactivity.
 *
 * The backend enforces the real security boundary via the sliding-expiration
 * auth cookie (Keycloak:IdleTimeout). This composable mirrors that timeout on
 * the client so the user is proactively redirected to the login page as soon
 * as they go idle, instead of only discovering the expired session on their
 * next API call.
 */
export function useIdleTimeout() {
  const authStore = useAuthStore();
  const idleTimeoutSeconds = ref(DEFAULT_IDLE_TIMEOUT_SECONDS);

  let idleTimer: ReturnType<typeof setTimeout> | null = null;
  let lastActivityAt = 0;
  let started = false;

  function clearIdleTimer() {
    if (idleTimer) {
      clearTimeout(idleTimer);
      idleTimer = null;
    }
  }

  function forceLogout() {
    stop();
    authStore.clearUserInfo();
    globalThis.location.href = `${import.meta.env.BASE_URL}api/auth/logout`;
  }

  function scheduleIdleTimer() {
    clearIdleTimer();
    idleTimer = setTimeout(forceLogout, idleTimeoutSeconds.value * 1000);
  }

  function onActivity() {
    const now = Date.now();
    if (now - lastActivityAt < ACTIVITY_THROTTLE_MS) return;
    lastActivityAt = now;
    scheduleIdleTimer();
  }

  async function start() {
    if (started) return;
    started = true;

    try {
      const httpService = new HttpService(import.meta.env.BASE_URL);
      const authService = new AuthService(httpService);
      const status = await authService.getAuthStatus();
      if (status.idleTimeoutSeconds && status.idleTimeoutSeconds > 0) {
        idleTimeoutSeconds.value = status.idleTimeoutSeconds;
      }
    } catch {
      // Fall back to the default if the status check fails.
    }

    ACTIVITY_EVENTS.forEach((eventName) =>
      window.addEventListener(eventName, onActivity, { passive: true })
    );
    scheduleIdleTimer();
  }

  function stop() {
    if (!started) return;
    started = false;
    clearIdleTimer();
    ACTIVITY_EVENTS.forEach((eventName) =>
      window.removeEventListener(eventName, onActivity)
    );
  }

  onBeforeUnmount(stop);

  return { start, stop, idleTimeoutSeconds };
}
