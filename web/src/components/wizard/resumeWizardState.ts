export interface ResumeWizardState {
  activeStep: string;
  activeSubstep: string;
  hiddenSteps: Record<string, boolean>;
  hiddenSubsteps: Record<string, boolean>;
  statusMap: Record<string, string>;
  disabledMap: Record<string, boolean>;
}

interface ResumeWizardStep {
  key: string;
  substeps: Array<{ key: string }>;
}

export function repairResumeVisibility(
  state: ResumeWizardState,
  steps: ResumeWizardStep[]
): ResumeWizardState {
  const hiddenSteps = { ...state.hiddenSteps };
  const hiddenSubsteps = { ...state.hiddenSubsteps };

  for (const step of steps) {
    const hasVisitedSubstep = step.substeps.some(
      (substep) => state.statusMap[substep.key] !== undefined
    );
    if (step.key === state.activeStep || hasVisitedSubstep) {
      delete hiddenSteps[step.key];
    }
    for (const substep of step.substeps) {
      if (
        substep.key === state.activeSubstep ||
        state.statusMap[substep.key] !== undefined
      ) {
        delete hiddenSubsteps[substep.key];
      }
    }
  }

  return { ...state, hiddenSteps, hiddenSubsteps };
}

export function getPreferredWizardStateCarrierKey(
  stepKeys: string[],
  surveyStepKey: string,
  activeStep: string,
  stepEls: Record<string, any>
): string {
  // Survey is the sole state owner. This is the only key that should receive
  // hostWizardState during both resume and live navigation, even if some other
  // form is currently active or still loading in the background.
  if (surveyStepKey && stepKeys.includes(surveyStepKey)) return surveyStepKey;

  if (activeStep && stepEls[activeStep]) return activeStep;

  return stepKeys.find((key) => stepEls[key]) ?? '';
}

export function sanitizeStepDataForSave(
  stepKey: string,
  surveyStepKey: string,
  data: Record<string, any>
): Record<string, any> {
  if (!data || typeof data !== 'object') return {};

  const sanitized = { ...data };
  if (stepKey !== surveyStepKey) {
    delete sanitized.hostWizardState;
  }
  return sanitized;
}

export function resolveLatestResumeState(
  rows: Array<{
    formId?: string | null;
    data?: string | null;
    updatedAt?: string | null;
    createdAt?: string | null;
  }>,
  surveyStepKey: string
): ResumeWizardState | null {
  let latest: {
    updatedAt: string;
    state: ResumeWizardState;
  } | null = null;

  for (const row of rows) {
    if (!row?.data || row.formId !== surveyStepKey) continue;

    try {
      const parsed = JSON.parse(row.data);
      if (!parsed || typeof parsed !== 'object' || !parsed.hostWizardState) {
        continue;
      }

      const saved = JSON.parse(
        parsed.hostWizardState
      ) as Partial<ResumeWizardState>;
      const candidate: ResumeWizardState = {
        activeStep: saved.activeStep ?? '',
        activeSubstep: saved.activeSubstep ?? '',
        hiddenSteps: saved.hiddenSteps ?? {},
        hiddenSubsteps: saved.hiddenSubsteps ?? {},
        statusMap: saved.statusMap ?? {},
        disabledMap: saved.disabledMap ?? {},
      };

      const updatedAt = row.updatedAt ?? row.createdAt ?? '';
      if (!latest || updatedAt > latest.updatedAt) {
        latest = { updatedAt, state: candidate };
      }
    } catch {
      // Ignore malformed resume payloads; fall back to default new-session state.
    }
  }

  return latest?.state ?? null;
}
