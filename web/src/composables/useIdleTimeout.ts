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

// How long the "you're about to be signed out" warning is shown before the
// forced logout fires, counted down inside the warning dialog.
const WARNING_SECONDS = 60;

/**
 * Forces re-authentication after a period of user inactivity.
 *
 * The backend enforces the real security boundary via the sliding-expiration
 * auth cookie (Keycloak:IdleTimeout). This composable mirrors that timeout on
 * the client, warning the user shortly before the timeout so they can choose
 * to keep working or sign out, instead of only discovering the expired
 * session on their next API call.
 */
export function useIdleTimeout() {
  const authStore = useAuthStore();
  const idleTimeoutSeconds = ref(DEFAULT_IDLE_TIMEOUT_SECONDS);
  const showWarning = ref(false);
  const remainingSeconds = ref(WARNING_SECONDS);

  let warningTimer: ReturnType<typeof setTimeout> | null = null;
  let countdownInterval: ReturnType<typeof setInterval> | null = null;
  let lastActivityAt = 0;
  let started = false;

  function clearWarningTimer() {
    if (warningTimer) {
      clearTimeout(warningTimer);
      warningTimer = null;
    }
  }

  function clearCountdownInterval() {
    if (countdownInterval) {
      clearInterval(countdownInterval);
      countdownInterval = null;
    }
  }

  function forceLogout() {
    stop();
    authStore.clearUserInfo();
    globalThis.location.href = `${import.meta.env.BASE_URL}api/auth/logout`;
  }

  function showIdleWarning() {
    showWarning.value = true;
    remainingSeconds.value = WARNING_SECONDS;
    countdownInterval = setInterval(() => {
      remainingSeconds.value -= 1;
      if (remainingSeconds.value <= 0) {
        forceLogout();
      }
    }, 1000);
  }

  // Schedules the warning dialog to appear once the user has been idle for
  // (idleTimeoutSeconds - WARNING_SECONDS), and clears any warning in progress.
  function scheduleIdleTimer() {
    clearWarningTimer();
    clearCountdownInterval();
    showWarning.value = false;

    const warningDelaySeconds = Math.max(
      idleTimeoutSeconds.value - WARNING_SECONDS,
      0
    );
    warningTimer = setTimeout(showIdleWarning, warningDelaySeconds * 1000);
  }

  function onActivity() {
    // Once the warning is showing, only an explicit "Continue Working" click
    // (continueWorking) should restart idle detection — silent background
    // activity must not dismiss a warning the user hasn't acknowledged.
    if (showWarning.value) return;

    const now = Date.now();
    if (now - lastActivityAt < ACTIVITY_THROTTLE_MS) return;
    lastActivityAt = now;
    scheduleIdleTimer();
  }

  function continueWorking() {
    lastActivityAt = Date.now();
    scheduleIdleTimer();
  }

  function signOut() {
    forceLogout();
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
    clearWarningTimer();
    clearCountdownInterval();
    showWarning.value = false;
    ACTIVITY_EVENTS.forEach((eventName) =>
      window.removeEventListener(eventName, onActivity)
    );
  }

  onBeforeUnmount(stop);

  return {
    start,
    stop,
    idleTimeoutSeconds,
    showWarning,
    remainingSeconds,
    continueWorking,
    signOut,
  };
}
