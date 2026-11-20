import type { WizardStep } from '@/types/applicationStep';

/** One CHEFS form (top-level step) and the substeps of it to validate. */
export interface StepValidationTarget {
  stepKey: string;
  substepKeys: string[];
}

export type StepValidationStatus =
  'pending' | 'loading' | 'checking' | 'valid' | 'invalid' | 'error';

export interface ApplicationValidationResult {
  valid: boolean;
  /** Substeps whose form section failed validation, in wizard order. */
  failedSubsteps: string[];
  /** Steps whose CHEFS form could not be loaded, so were not validated. */
  loadFailedSteps: string[];
}

export type SubmitPhase = 'validating' | 'invalid' | 'saving' | 'error';

export interface SubmitStepProgress {
  key: string;
  title: string;
  status: StepValidationStatus;
}

export interface SubmitIssue {
  substepKey: string;
  label: string;
}

/** Every step/substep currently visible in the sidebar, in wizard order. */
export function getValidationTargets(
  steps: WizardStep[],
  isStepHidden: (stepKey: string) => boolean,
  isSubstepHidden: (substepKey: string) => boolean
): StepValidationTarget[] {
  return steps
    .filter((step) => !isStepHidden(step.key))
    .map((step) => ({
      stepKey: step.key,
      substepKeys: step.substeps
        .map((substep) => substep.key)
        .filter((key) => !isSubstepHidden(key)),
    }))
    .filter((target) => target.substepKeys.length > 0);
}
