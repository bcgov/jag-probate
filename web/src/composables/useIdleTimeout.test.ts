import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const getAuthStatus = vi.fn();
const clearUserInfo = vi.fn();

vi.mock('@/services/AuthService', () => ({
  default: class {
    getAuthStatus = getAuthStatus;
  },
}));
vi.mock('@/services/HttpService', () => ({ default: class {} }));
vi.mock('@/stores', () => ({ useAuthStore: () => ({ clearUserInfo }) }));
vi.mock('vue', async (importOriginal) => ({
  ...(await importOriginal<typeof import('vue')>()),
  onBeforeUnmount: vi.fn(),
}));

import { useIdleTimeout } from './useIdleTimeout';

const IDLE_SECONDS = 180;
const BASE = import.meta.env.BASE_URL;

describe('useIdleTimeout', () => {
  let location: { href: string };

  beforeEach(() => {
    vi.useFakeTimers();
    getAuthStatus.mockReset();
    clearUserInfo.mockReset();
    location = { href: '' };
    vi.stubGlobal('location', location);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  async function startIdle() {
    getAuthStatus.mockResolvedValue({
      isAuthenticated: true,
      idleTimeoutSeconds: IDLE_SECONDS,
    });
    const idle = useIdleTimeout();
    await idle.start();
    return idle;
  }

  it('redirects to the landing page (no warning) when restored after the timeout elapsed', async () => {
    const idle = await startIdle();
    getAuthStatus.mockResolvedValue({ isAuthenticated: false });

    // Simulate a minimized tab: wall clock advances but timers don't fire.
    vi.setSystemTime(Date.now() + (IDLE_SECONDS + 600) * 1000);
    document.dispatchEvent(new Event('visibilitychange'));
    await vi.waitFor(() => expect(location.href).toBe(BASE));

    expect(idle.showWarning.value).toBe(false);
    expect(clearUserInfo).toHaveBeenCalled();
  });

  it('does not let activity after the deadline revive the session', async () => {
    await startIdle();
    getAuthStatus.mockResolvedValue({ isAuthenticated: false });

    vi.setSystemTime(Date.now() + (IDLE_SECONDS + 1) * 1000);
    window.dispatchEvent(new Event('mousemove'));
    await vi.waitFor(() => expect(location.href).toBe(BASE));
  });

  it('shows the warning with the remaining time anchored to last activity', async () => {
    const idle = await startIdle();

    vi.setSystemTime(Date.now() + (IDLE_SECONDS - 20) * 1000);
    window.dispatchEvent(new Event('focus'));

    expect(idle.showWarning.value).toBe(true);
    expect(idle.remainingSeconds.value).toBe(20);
  });

  it('sends the user to the landing page if Continue Working finds the server session gone', async () => {
    const idle = await startIdle();
    vi.advanceTimersByTime((IDLE_SECONDS - 30) * 1000);
    expect(idle.showWarning.value).toBe(true);

    getAuthStatus.mockResolvedValue({ isAuthenticated: false });
    await idle.continueWorking();

    expect(location.href).toBe(BASE);
  });

  it('keeps working after Continue Working when the server session is valid', async () => {
    const idle = await startIdle();
    vi.advanceTimersByTime((IDLE_SECONDS - 30) * 1000);

    await idle.continueWorking();

    expect(idle.showWarning.value).toBe(false);
    expect(location.href).toBe('');
  });

  it('logs out through the backend when the countdown completes', async () => {
    const idle = await startIdle();
    vi.advanceTimersByTime(IDLE_SECONDS * 1000);

    expect(idle.showWarning.value).toBe(false);
    expect(location.href).toBe(`${BASE}api/auth/logout`);
  });
});
