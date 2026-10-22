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

// The server's sliding auth cookie is only renewed by requests, so genuine
// user activity pings the server at most this often to keep both clocks in sync.
const KEEP_ALIVE_THROTTLE_MS = 60 * 1000;

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
 *
 * All deadlines are derived from the time of the last genuine user activity,
 * never from "now", so a tab that was minimized/backgrounded past the timeout
 * is signed out as soon as it becomes visible again instead of being offered
 * a fresh countdown for a session the server has already expired.
 */
export function useIdleTimeout() {
  const authStore = useAuthStore();
  const idleTimeoutSeconds = ref(DEFAULT_IDLE_TIMEOUT_SECONDS);
  const showWarning = ref(false);
  const remainingSeconds = ref(WARNING_SECONDS);

  const authService = new AuthService(
    new HttpService(import.meta.env.BASE_URL)
  );

  let warningTimer: ReturnType<typeof setTimeout> | null = null;
  let countdownInterval: ReturnType<typeof setInterval> | null = null;
  // Epoch ms of the last genuine user activity. Timers in backgrounded tabs
  // are heavily throttled (or frozen when minimized), so every check compares
  // Date.now() to deadlines derived from this rather than trusting timers.
  // See: https://developer.chrome.com/blog/timer-throttling-in-chrome-88
  let lastActivityAt = 0;
  let lastKeepAliveAt = 0;
  let started = false;
  let loggingOut = false;

  const idleDeadline = () => lastActivityAt + idleTimeoutSeconds.value * 1000;
  const warningDeadline = () => idleDeadline() - WARNING_SECONDS * 1000;

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

  // User-initiated or countdown-completed logout while the server session is
  // still alive: go through the backend so the Keycloak session is ended too.
  function forceLogout() {
    if (loggingOut) return;
    loggingOut = true;
    stop();
    authStore.clearUserInfo();
    globalThis.location.href = `${import.meta.env.BASE_URL}api/auth/logout`;
  }

  // The idle timeout fully elapsed while the tab wasn't able to react (e.g.
  // minimized). The server cookie is already gone, so send the user straight
  // to the landing page; if the server session somehow survived, fall back
  // to the full logout so it gets terminated.
  async function expireSession() {
    if (loggingOut) return;
    loggingOut = true;
    stop();
    let serverStillAuthenticated = false;
    try {
      serverStillAuthenticated = (await authService.getAuthStatus())
        .isAuthenticated;
    } catch {
      // Treat failures as already expired.
    }
    authStore.clearUserInfo();
    globalThis.location.href = serverStillAuthenticated
      ? `${import.meta.env.BASE_URL}api/auth/logout`
      : import.meta.env.BASE_URL;
  }

  // Recomputed from the deadline (not decremented) so a throttled/delayed
  // tick self-corrects instead of leaving a stale countdown on screen.
  function tickCountdown() {
    const msLeft = idleDeadline() - Date.now();
    remainingSeconds.value = Math.max(Math.ceil(msLeft / 1000), 0);
    if (msLeft <= 0) {
      forceLogout();
    }
  }

  function showIdleWarning() {
    if (!started || showWarning.value) return;
    showWarning.value = true;
    tickCountdown();
    if (started) {
      countdownInterval = setInterval(tickCountdown, 1000);
    }
  }

  // Reconciles UI state against the wall clock. Called whenever timers fire
  // and whenever the page becomes visible/focused again.
  function checkIdleState() {
    if (!started || loggingOut) return;
    const now = Date.now();
    if (now >= idleDeadline()) {
      // Whole timeout passed without the warning being acted on (or even
      // seen) — the server session is gone, don't offer "Continue Working".
      void expireSession();
    } else if (showWarning.value) {
      tickCountdown();
    } else if (now >= warningDeadline()) {
      showIdleWarning();
    }
  }

  // Schedules the warning dialog to appear once the user has been idle for
  // (idleTimeoutSeconds - WARNING_SECONDS), and clears any warning in progress.
  function scheduleIdleTimer() {
    clearWarningTimer();
    clearCountdownInterval();
    showWarning.value = false;
    const delay = Math.max(warningDeadline() - Date.now(), 0);
    warningTimer = setTimeout(checkIdleState, delay);
  }

  function keepServerSessionAlive(force = false) {
    const now = Date.now();
    if (!force && now - lastKeepAliveAt < KEEP_ALIVE_THROTTLE_MS) return;
    lastKeepAliveAt = now;
    authService
      .getAuthStatus()
      .then((status) => {
        if (!status.isAuthenticated) void expireSession();
      })
      .catch(() => {
        // Transient network errors are ignored; the next check will retry.
      });
  }

  function onPageVisible() {
    if (document.visibilityState === 'visible') checkIdleState();
  }

  function onActivity() {
    // Once the warning is showing, only an explicit "Continue Working" click
    // (continueWorking) should restart idle detection — silent background
    // activity must not dismiss a warning the user hasn't acknowledged.
    if (!started || showWarning.value || loggingOut) return;

    const now = Date.now();
    // Activity arriving after the deadline (e.g. the first mousemove after
    // restoring a minimized window) must not revive an expired session.
    if (now >= idleDeadline()) {
      void expireSession();
      return;
    }
    if (now - lastActivityAt < ACTIVITY_THROTTLE_MS) return;
    lastActivityAt = now;
    scheduleIdleTimer();
    keepServerSessionAlive();
  }

  async function continueWorking() {
    if (!started || loggingOut) return;
    if (Date.now() >= idleDeadline()) {
      await expireSession();
      return;
    }
    // Confirm the server session is still valid (this request also renews
    // the sliding cookie) before letting the user carry on.
    try {
      const status = await authService.getAuthStatus();
      if (!status.isAuthenticated) {
        await expireSession();
        return;
      }
    } catch {
      // If the check fails, let the user continue; the next API call or
      // idle check will catch a genuinely expired session.
    }
    lastActivityAt = Date.now();
    lastKeepAliveAt = lastActivityAt;
    scheduleIdleTimer();
  }

  function signOut() {
    forceLogout();
  }

  async function start() {
    if (started) return;
    started = true;
    loggingOut = false;
    lastActivityAt = Date.now();
    lastKeepAliveAt = lastActivityAt;

    try {
      const status = await authService.getAuthStatus();
      if (status.idleTimeoutSeconds && status.idleTimeoutSeconds > 0) {
        idleTimeoutSeconds.value = status.idleTimeoutSeconds;
      }
    } catch {
      // Fall back to the default if the status check fails.
    }
    if (!started) return;

    ACTIVITY_EVENTS.forEach((eventName) =>
      window.addEventListener(eventName, onActivity, { passive: true })
    );
    // Minimizing/restoring or switching tabs/windows doesn't always fire every
    // one of these, so listen to all of them.
    document.addEventListener('visibilitychange', onPageVisible);
    window.addEventListener('focus', checkIdleState);
    window.addEventListener('pageshow', checkIdleState);
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
    document.removeEventListener('visibilitychange', onPageVisible);
    window.removeEventListener('focus', checkIdleState);
    window.removeEventListener('pageshow', checkIdleState);
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
