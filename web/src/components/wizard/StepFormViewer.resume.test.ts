import { describe, expect, it } from 'vitest';
import {
  getPreferredWizardStateCarrierKey,
  resolveLatestResumeState,
  sanitizeStepDataForSave,
} from './resumeWizardState';

describe('resume wizard state helpers', () => {
  it('prefers the survey step as the carrier even when another step is active', () => {
    const carrierKey = getPreferredWizardStateCarrierKey(
      ['survey', 'step1', 'step2'],
      'survey',
      'step2',
      {
        survey: { formioInstance: { data: {} } },
        step2: { formioInstance: { data: {} } },
      }
    );

    expect(carrierKey).toBe('survey');
  });

  it('prefers the survey step even before it is loaded', () => {
    const carrierKey = getPreferredWizardStateCarrierKey(
      ['survey', 'step1', 'step2'],
      'survey',
      'step2',
      {
        step1: { formioInstance: { data: {} } },
        step2: { formioInstance: { data: {} } },
      }
    );

    expect(carrierKey).toBe('survey');
  });

  it('ignores non-survey hostWizardState rows when restoring resume state', () => {
    const state = resolveLatestResumeState(
      [
        {
          formId: 'step2',
          data: JSON.stringify({
            hostWizardState: JSON.stringify({
              activeStep: 'step2',
              activeSubstep: 'step2-sub1',
              hiddenSteps: {},
              hiddenSubsteps: {},
              statusMap: {},
              disabledMap: {},
            }),
          }),
          updatedAt: '2025-01-01T00:00:00Z',
        },
        {
          formId: 'survey',
          data: JSON.stringify({
            hostWizardState: JSON.stringify({
              activeStep: 'step1',
              activeSubstep: 'step1-sub2',
              hiddenSteps: { step2: true },
              hiddenSubsteps: {},
              statusMap: { 'step1-sub2': 'completed' },
              disabledMap: { step2: true },
            }),
          }),
          updatedAt: '2025-01-03T00:00:00Z',
        },
      ],
      'survey'
    );

    expect(state).toEqual({
      activeStep: 'step1',
      activeSubstep: 'step1-sub2',
      hiddenSteps: { step2: true },
      hiddenSubsteps: {},
      statusMap: { 'step1-sub2': 'completed' },
      disabledMap: { step2: true },
    });
  });

  it('sanitizes hostWizardState out of non-survey step payloads', () => {
    const cleaned = sanitizeStepDataForSave('step2', 'survey', {
      hostWizardState: JSON.stringify({ activeStep: 'step2' }),
      someField: 'x',
    });

    expect(cleaned).toEqual({ someField: 'x' });
    expect(cleaned).not.toHaveProperty('hostWizardState');
  });
});
