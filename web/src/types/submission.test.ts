import { describe, expect, it } from 'vitest';
import { formatSubmissionStatus } from './submission';

describe('formatSubmissionStatus', () => {
  it('maps ready_to_print to "Ready to Print"', () => {
    expect(formatSubmissionStatus('ready_to_print')).toBe('Ready to Print');
  });

  it('maps the legacy submitted status to "Ready to Print"', () => {
    expect(formatSubmissionStatus('submitted')).toBe('Ready to Print');
  });

  it('maps draft to "Draft"', () => {
    expect(formatSubmissionStatus('draft')).toBe('Draft');
  });

  it('falls back to the raw code for unknown statuses', () => {
    expect(formatSubmissionStatus('something_else')).toBe('something_else');
  });

  it('returns an empty string for null/undefined', () => {
    expect(formatSubmissionStatus(null)).toBe('');
    expect(formatSubmissionStatus(undefined)).toBe('');
  });
});
