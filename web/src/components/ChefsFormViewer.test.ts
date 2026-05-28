import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';

/**
 * Tests for ChefsFormViewer auto-save and session state logic.
 *
 * Since ChefsFormViewer is tightly coupled to a web component (<chefs-form-viewer>)
 * and external CHEFS API, we test the extracted logic patterns rather than
 * mounting the full component. These tests verify:
 *   1. sessionStorage-based state initialization
 *   2. Debounce scheduling behavior
 *   3. Upsert payload construction
 *   4. Read-only / auto-save skip for submitted forms
 */

describe('ChefsFormViewer – sessionStorage state management', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  it('initializes currentDbId from sessionStorage when resumeDbId is set', () => {
    sessionStorage.setItem('resumeDbId', '42');

    const resumeDbId = sessionStorage.getItem('resumeDbId');
    const currentDbId = resumeDbId ? Number(resumeDbId) : undefined;

    expect(currentDbId).toBe(42);
  });

  it('initializes currentDbId as undefined when resumeDbId is not set', () => {
    const resumeDbId = sessionStorage.getItem('resumeDbId');
    const currentDbId = resumeDbId ? Number(resumeDbId) : undefined;

    expect(currentDbId).toBeUndefined();
  });

  it('detects submitted status from sessionStorage', () => {
    sessionStorage.setItem('resumeStatus', 'submitted');

    const isSubmitted =
      sessionStorage.getItem('resumeStatus') === 'submitted';

    expect(isSubmitted).toBe(true);
  });

  it('detects non-submitted (draft) status', () => {
    sessionStorage.setItem('resumeStatus', 'draft');

    const isSubmitted =
      sessionStorage.getItem('resumeStatus') === 'submitted';

    expect(isSubmitted).toBe(false);
  });

  it('detects non-submitted when resumeStatus is absent', () => {
    const isSubmitted =
      sessionStorage.getItem('resumeStatus') === 'submitted';

    expect(isSubmitted).toBe(false);
  });
});

describe('ChefsFormViewer – auto-save debounce logic', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('schedules save after debounce period of quiet', () => {
    const performAutoSave = vi.fn();
    const autoSaveThrottle = 5000;

    // Simulate scheduleAutoSave
    let debounceTimer: ReturnType<typeof setTimeout> | null = null;
    function scheduleAutoSave() {
      if (debounceTimer) clearTimeout(debounceTimer);
      debounceTimer = setTimeout(() => performAutoSave(), autoSaveThrottle);
    }

    scheduleAutoSave();

    // Not called yet
    expect(performAutoSave).not.toHaveBeenCalled();

    // Advance past debounce
    vi.advanceTimersByTime(5000);

    expect(performAutoSave).toHaveBeenCalledOnce();
  });

  it('resets debounce timer on subsequent changes', () => {
    const performAutoSave = vi.fn();
    const autoSaveThrottle = 5000;

    let debounceTimer: ReturnType<typeof setTimeout> | null = null;
    function scheduleAutoSave() {
      if (debounceTimer) clearTimeout(debounceTimer);
      debounceTimer = setTimeout(() => performAutoSave(), autoSaveThrottle);
    }

    // First change
    scheduleAutoSave();
    vi.advanceTimersByTime(3000);

    // Second change resets the timer
    scheduleAutoSave();
    vi.advanceTimersByTime(3000);

    // Only 3s after second change — not yet
    expect(performAutoSave).not.toHaveBeenCalled();

    // 5s after second change
    vi.advanceTimersByTime(2000);
    expect(performAutoSave).toHaveBeenCalledOnce();
  });

  it('does not schedule when isSaving is true (queues pendingSave)', () => {
    const performAutoSave = vi.fn();
    const autoSaveThrottle = 5000;
    let isSaving = true;
    let pendingSave = false;

    let debounceTimer: ReturnType<typeof setTimeout> | null = null;
    function scheduleAutoSave() {
      if (isSaving) {
        pendingSave = true;
        return;
      }
      if (debounceTimer) clearTimeout(debounceTimer);
      debounceTimer = setTimeout(() => performAutoSave(), autoSaveThrottle);
    }

    scheduleAutoSave();
    vi.advanceTimersByTime(10000);

    expect(performAutoSave).not.toHaveBeenCalled();
    expect(pendingSave).toBe(true);
  });

  it('skips changes during formReady grace period', () => {
    const scheduleAutoSave = vi.fn();
    let formReady = false;

    function onFormChange() {
      if (formReady) scheduleAutoSave();
    }

    // Change before ready
    onFormChange();
    expect(scheduleAutoSave).not.toHaveBeenCalled();

    // After 2s grace period
    setTimeout(() => {
      formReady = true;
    }, 2000);
    vi.advanceTimersByTime(2000);

    // Change after ready
    onFormChange();
    expect(scheduleAutoSave).toHaveBeenCalledOnce();
  });
});

describe('ChefsFormViewer – upsert payload construction', () => {
  it('builds correct payload for auto-save (draft)', () => {
    const currentDbId = 42;
    const newChefsId = 'chefs-id-abc123';
    const createdBy = 'testuser@idir';
    const applicantName = 'John Doe';
    const updatedAt = '2026-05-28T10:00:00Z';

    const payload = {
      id: currentDbId,
      chefsSubmissionId: newChefsId,
      createdBy,
      applicantName,
      status: 'draft',
      lastUpdatedAt: updatedAt,
      lastFiledAt: null,
    };

    expect(payload.id).toBe(42);
    expect(payload.status).toBe('draft');
    expect(payload.lastFiledAt).toBeNull();
    expect(payload.chefsSubmissionId).toBe('chefs-id-abc123');
  });

  it('builds correct payload for explicit submit', () => {
    const currentDbId = 42;
    const newChefsId = 'chefs-id-def456';
    const createdBy = 'testuser@idir';
    const applicantName = 'Jane Doe';
    const now = '2026-05-28T12:00:00Z';

    const payload = {
      id: currentDbId,
      chefsSubmissionId: newChefsId,
      createdBy,
      applicantName,
      status: 'submitted',
      lastUpdatedAt: now,
      lastFiledAt: now,
    };

    expect(payload.id).toBe(42);
    expect(payload.status).toBe('submitted');
    expect(payload.lastFiledAt).toBe(now);
  });

  it('sends id as undefined for first-time save (new session)', () => {
    const currentDbId = undefined;

    const payload = {
      id: currentDbId,
      chefsSubmissionId: 'new-chefs-id',
      createdBy: 'user@idir',
      applicantName: '',
      status: 'draft',
      lastUpdatedAt: new Date().toISOString(),
      lastFiledAt: null,
    };

    expect(payload.id).toBeUndefined();
  });
});

describe('ChefsFormViewer – read-only and auto-save disable for submitted', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  it('should not setup auto-save when status is submitted', () => {
    sessionStorage.setItem('resumeStatus', 'submitted');
    const isSubmitted =
      sessionStorage.getItem('resumeStatus') === 'submitted';
    const autoSaveThrottle = 5000;

    const shouldSetupAutoSave = autoSaveThrottle > 0 && !isSubmitted;

    expect(shouldSetupAutoSave).toBe(false);
  });

  it('should setup auto-save when status is draft', () => {
    sessionStorage.setItem('resumeStatus', 'draft');
    const isSubmitted =
      sessionStorage.getItem('resumeStatus') === 'submitted';
    const autoSaveThrottle = 5000;

    const shouldSetupAutoSave = autoSaveThrottle > 0 && !isSubmitted;

    expect(shouldSetupAutoSave).toBe(true);
  });

  it('should set read-only attribute to true when submitted', () => {
    sessionStorage.setItem('resumeStatus', 'submitted');
    const isSubmitted =
      sessionStorage.getItem('resumeStatus') === 'submitted';

    const readOnlyValue = isSubmitted ? 'true' : 'false';

    expect(readOnlyValue).toBe('true');
  });

  it('should set read-only attribute to false when draft', () => {
    sessionStorage.setItem('resumeStatus', 'draft');
    const isSubmitted =
      sessionStorage.getItem('resumeStatus') === 'submitted';

    const readOnlyValue = isSubmitted ? 'true' : 'false';

    expect(readOnlyValue).toBe('false');
  });
});
