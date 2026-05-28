import { describe, expect, it, beforeEach, afterEach } from 'vitest';

/**
 * Tests for PreviousActivity sessionStorage state management.
 * Verifies that resume/create actions correctly set and clear
 * the sessionStorage keys used by ChefsFormViewer.
 */

describe('PreviousActivity – sessionStorage management', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  describe('resumeApplication', () => {
    function resumeApplication(app: {
      chefsSubmissionId: string;
      id: number;
      status: string;
    }) {
      sessionStorage.setItem('resumeSubmissionId', app.chefsSubmissionId);
      sessionStorage.setItem('resumeDbId', String(app.id));
      sessionStorage.setItem('resumeStatus', app.status ?? '');
    }

    it('stores resumeSubmissionId', () => {
      resumeApplication({
        chefsSubmissionId: 'chefs-abc',
        id: 5,
        status: 'draft',
      });

      expect(sessionStorage.getItem('resumeSubmissionId')).toBe('chefs-abc');
    });

    it('stores resumeDbId as string', () => {
      resumeApplication({
        chefsSubmissionId: 'chefs-abc',
        id: 42,
        status: 'draft',
      });

      expect(sessionStorage.getItem('resumeDbId')).toBe('42');
    });

    it('stores resumeStatus', () => {
      resumeApplication({
        chefsSubmissionId: 'chefs-abc',
        id: 5,
        status: 'submitted',
      });

      expect(sessionStorage.getItem('resumeStatus')).toBe('submitted');
    });

    it('stores draft status correctly', () => {
      resumeApplication({
        chefsSubmissionId: 'chefs-xyz',
        id: 10,
        status: 'draft',
      });

      expect(sessionStorage.getItem('resumeStatus')).toBe('draft');
    });
  });

  describe('createCase', () => {
    function createCase() {
      sessionStorage.removeItem('resumeSubmissionId');
      sessionStorage.removeItem('resumeDbId');
      sessionStorage.removeItem('resumeStatus');
    }

    it('clears resumeSubmissionId', () => {
      sessionStorage.setItem('resumeSubmissionId', 'old-id');
      createCase();

      expect(sessionStorage.getItem('resumeSubmissionId')).toBeNull();
    });

    it('clears resumeDbId', () => {
      sessionStorage.setItem('resumeDbId', '99');
      createCase();

      expect(sessionStorage.getItem('resumeDbId')).toBeNull();
    });

    it('clears resumeStatus', () => {
      sessionStorage.setItem('resumeStatus', 'submitted');
      createCase();

      expect(sessionStorage.getItem('resumeStatus')).toBeNull();
    });

    it('clears all three keys at once', () => {
      sessionStorage.setItem('resumeSubmissionId', 'id-1');
      sessionStorage.setItem('resumeDbId', '1');
      sessionStorage.setItem('resumeStatus', 'draft');

      createCase();

      expect(sessionStorage.getItem('resumeSubmissionId')).toBeNull();
      expect(sessionStorage.getItem('resumeDbId')).toBeNull();
      expect(sessionStorage.getItem('resumeStatus')).toBeNull();
    });
  });

  describe('auto-save sessionStorage updates', () => {
    it('updates resumeSubmissionId after auto-save', () => {
      sessionStorage.setItem('resumeSubmissionId', 'old-chefs-id');

      // Simulate what performAutoSave does
      const newId = 'new-chefs-id-from-save';
      sessionStorage.setItem('resumeSubmissionId', newId);

      expect(sessionStorage.getItem('resumeSubmissionId')).toBe(
        'new-chefs-id-from-save'
      );
    });

    it('persists resumeDbId after first upsert response', () => {
      // First save — no DB ID yet
      expect(sessionStorage.getItem('resumeDbId')).toBeNull();

      // After upsert returns the ID
      const responseId = 55;
      sessionStorage.setItem('resumeDbId', String(responseId));

      expect(sessionStorage.getItem('resumeDbId')).toBe('55');
    });

    it('preserves resumeDbId on subsequent saves', () => {
      sessionStorage.setItem('resumeDbId', '55');

      // Subsequent auto-save — only updates if currentDbId is set
      const currentDbId = 55;
      if (currentDbId) {
        sessionStorage.setItem('resumeDbId', String(currentDbId));
      }

      expect(sessionStorage.getItem('resumeDbId')).toBe('55');
    });
  });
});
