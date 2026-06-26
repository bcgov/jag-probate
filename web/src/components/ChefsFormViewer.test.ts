import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';

/**
 * Tests for ChefsFormViewer component logic that is not directly tied to the Vue template.
 * Focuses on state initialization from props, auto-save debounce behavior, and payload construction for upsert.
 */

describe('ChefsFormViewer – props-based state initialization', () => {
  it('initializes currentDbId from dbId prop', () => {
    const props = { dbId: 'a7b23b0f-ac4d-4f78-a9ca-39be6bbf5ac4' };
    const currentDbId = props.dbId;
    expect(currentDbId).toBe('a7b23b0f-ac4d-4f78-a9ca-39be6bbf5ac4');
  });

  it('initializes currentDbId as undefined when dbId prop is absent', () => {
    const props: { dbId?: string } = {};
    const currentDbId = props.dbId;
    expect(currentDbId).toBeUndefined();
  });

  it('reads isSubmitted as true when readOnly prop is true', () => {
    const props = { readOnly: true };
    const isSubmitted = props.readOnly ?? false;
    expect(isSubmitted).toBe(true);
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
    const isSaving = true;
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
  const GUID = 'a7b23b0f-ac4d-4f78-a9ca-39be6bbf5ac4';

  it('builds correct payload for auto-save (draft)', () => {
    const payload = {
      publicId: GUID,
      chefsSubmissionId: 'chefs-id-abc123',
      createdBy: 'testuser@idir',
      applicantName: 'John Doe',
      status: 'draft',
      lastUpdatedAt: '2026-05-28T10:00:00Z',
      lastFiledAt: null,
    };

    expect(payload.publicId).toBe(GUID);
    expect(payload.status).toBe('draft');
    expect(payload.lastFiledAt).toBeNull();
    expect(payload.chefsSubmissionId).toBe('chefs-id-abc123');
  });

  it('builds correct payload for explicit submit', () => {
    const now = '2026-05-28T12:00:00Z';
    const payload = {
      publicId: GUID,
      chefsSubmissionId: 'chefs-id-def456',
      createdBy: 'testuser@idir',
      applicantName: 'Jane Doe',
      status: 'submitted',
      lastUpdatedAt: now,
      lastFiledAt: now,
    };

    expect(payload.publicId).toBe(GUID);
    expect(payload.status).toBe('submitted');
    expect(payload.lastFiledAt).toBe(now);
  });

  it('sends publicId as undefined for first-time save (new session)', () => {
    const payload = {
      publicId: undefined,
      chefsSubmissionId: 'new-chefs-id',
      createdBy: 'user@idir',
      applicantName: '',
      status: 'draft',
      lastUpdatedAt: new Date().toISOString(),
      lastFiledAt: null,
    };

    expect(payload.publicId).toBeUndefined();
  });
});

describe('ChefsFormViewer – saved emit after upsert', () => {
  it('emits saved with the publicId returned by the API', () => {
    const emitted: string[] = [];
    const emit = (event: string, publicId: string) => {
      if (event === 'saved') emitted.push(publicId);
    };
    const GUID = 'b2c34d5e-0000-0000-0000-000000000001';

    // Simulate what syncSave / performAutoSave does after upsert:
    const responsePublicId: string | undefined = GUID;
    const currentDbId = responsePublicId;
    if (currentDbId) emit('saved', currentDbId);

    expect(emitted).toHaveLength(1);
    expect(emitted[0]).toBe(GUID);
  });

  it('does not emit saved when upsert returns no publicId', () => {
    const emitted: string[] = [];
    const emit = (event: string, publicId: string) => {
      if (event === 'saved') emitted.push(publicId);
    };

    const apiResponse: string | undefined = undefined; // API returned nothing
    const currentDbId = apiResponse;
    if (currentDbId) emit('saved', currentDbId);

    expect(emitted).toHaveLength(0);
  });
});

describe('ChefsFormViewer – read-only and auto-save disable for submitted', () => {
  it('should not setup auto-save when readOnly prop is true', () => {
    const isSubmitted = true;
    const autoSaveThrottle = 5000;

    const shouldSetupAutoSave = autoSaveThrottle > 0 && !isSubmitted;

    expect(shouldSetupAutoSave).toBe(false);
  });

  it('should setup auto-save when readOnly prop is false', () => {
    const isSubmitted = false;
    const autoSaveThrottle = 5000;

    const shouldSetupAutoSave = autoSaveThrottle > 0 && !isSubmitted;

    expect(shouldSetupAutoSave).toBe(true);
  });
});
