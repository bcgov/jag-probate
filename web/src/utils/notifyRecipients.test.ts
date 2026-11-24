import { describe, expect, it } from 'vitest';
import {
  buildNotifyPeopleRows,
  summarizeNotifyRecipients,
} from './notifyRecipients';

const spouse = {
  spouseName: 'Sam Spouse',
  spouseIsAlive: 'yes',
  spouseIsAdult: 'yes',
  spouseIsCompetent: 'yes',
};
const minorChild = {
  childName: 'Kid',
  childIsAlive: 'yes',
  childIsAdult: 'no',
  childIsCompetent: 'yes',
};

describe('buildNotifyPeopleRows', () => {
  const base = {
    applicant: { applicantName: 'Sam Spouse' },
    spouse: { spouseData: [spouse] },
  };

  it('adds the PGT for a minor child and keeps entered delivery data', () => {
    const rows = buildNotifyPeopleRows(
      { ...base, child: { childData: [minorChild] } },
      [{ recipientName: 'Kid', deliveryDate: '2026-01-01' }]
    );
    expect(rows.map((r) => r.recipientName)).toEqual([
      'Kid',
      'The Public Guardian and Trustee (PGT)',
    ]);
    expect(rows[0].deliveryDate).toBe('2026-01-01');
  });

  it('drops recipients when the child is removed', () => {
    const rows = buildNotifyPeopleRows({ ...base, child: { childData: [] } }, [
      { recipientName: 'Kid' },
    ]);
    expect(rows).toEqual([]);
  });
});

describe('summarizeNotifyRecipients', () => {
  it('needs notice for a minor child', () => {
    const result = summarizeNotifyRecipients({
      applicant: { applicantName: 'Sam Spouse' },
      spouse: { spouseData: [spouse] },
      child: { childData: [minorChild] },
    });
    expect(result).toEqual({ hasRecipients: true, hasMinorOrIncapable: true });
  });

  it('needs no notice once the minor child is removed', () => {
    const result = summarizeNotifyRecipients({
      applicant: { applicantName: 'Sam Spouse' },
      spouse: { spouseData: [spouse] },
      child: { childData: [] },
    });
    expect(result).toEqual({
      hasRecipients: false,
      hasMinorOrIncapable: false,
    });
  });
});
