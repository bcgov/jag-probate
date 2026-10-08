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
  // Absolute wall-clock deadlines (epoch ms) — chained setTimeout/setInterval
  // calls are heavily throttled in backgrounded tabs (Chrome clamps them to
  // as little as once/minute), so firing correctly depends on comparing
  // Date.now() to a deadline rather than trusting tick counts.
  // See: https://developer.chrome.com/blog/timer-throttling-in-chrome-88
  let warningDeadline = 0;
  let logoutDeadline = 0;
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

  // Recomputed from logoutDeadline (not decremented) so a throttled/delayed
  // tick self-corrects instead of leaving a stale countdown on screen.
  function tickCountdown() {
    remainingSeconds.value = Math.max(
      Math.ceil((logoutDeadline - Date.now()) / 1000),
      0
    );
    if (Date.now() >= logoutDeadline) {
      forceLogout();
    }
  }

  function showIdleWarning() {
    showWarning.value = true;
    logoutDeadline = Date.now() + WARNING_SECONDS * 1000;
    tickCountdown();
    countdownInterval = setInterval(tickCountdown, 1000);
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
    warningDeadline = Date.now() + warningDelaySeconds * 1000;
    warningTimer = setTimeout(showIdleWarning, warningDelaySeconds * 1000);
  }

  // A backgrounded tab can delay the scheduled timers above by minutes; when
  // the tab regains focus, reconcile immediately against the deadlines
  // instead of waiting for the (possibly very late) throttled timer to fire.
  function onVisibilityChange() {
    if (document.visibilityState !== 'visible' || !started) return;

    if (showWarning.value) {
      tickCountdown();
    } else if (warningDeadline && Date.now() >= warningDeadline) {
      showIdleWarning();
    }
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
    document.addEventListener('visibilitychange', onVisibilityChange);
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
    document.removeEventListener('visibilitychange', onVisibilityChange);
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
