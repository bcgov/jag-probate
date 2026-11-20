import type { SidebarStepDto } from '@/types';
import type { WizardNavState, WizardStep } from '@/types/applicationStep';

export function getOrderedSubstepKeys(steps: WizardStep[]): string[] {
  return steps.flatMap((step) =>
    step.substeps.length
      ? step.substeps.map((substep) => substep.key)
      : [step.key]
  );
}

export function getVisibleSubstepKeys(
  orderedKeys: string[],
  parentOf: (key: string) => string | null,
  isStepHidden: (key: string) => boolean,
  isSubstepHidden: (key: string) => boolean
): string[] {
  return orderedKeys.filter((key) => {
    if (isSubstepHidden(key)) return false;
    const parent = parentOf(key);
    return !(parent && isStepHidden(parent));
  });
}

/**
 * Maps the backend sidebar structure (GET /api/Chefs/Sidebar) to the
 * WizardStep[] shape the sidebar component renders. FormId/ApiKey are never
 * present in the DTO. Order follows the backend's configured order.
 */
export function mapSidebarStepsToWizardSteps(
  dtos: SidebarStepDto[]
): WizardStep[] {
  return dtos
    .slice()
    .sort((a, b) => a.order - b.order)
    .map((dto, index) => ({
      key: dto.key,
      // Number visible wizard steps sequentially (step0 is not shown in sidebar).
      number: index + 1,
      title: dto.title,
      icon: dto.icon || undefined,
      defaultSubstep: dto.children[0]?.key ?? dto.key,
      substeps: dto.children.map((c) => ({
        key: c.key,
        label: c.title,
        hiddenIsGhost: c.hiddenIsGhost,
        ...(c.disabled ? { disabled: true } : {}),
      })),
    }));
}

/**
 * Same baseline as deriveInitialNavState, from the mapped WizardStep[] shape:
 * every step but the first hidden, Disabled substeps hidden.
 */
export function deriveBaselineNavState(steps: WizardStep[]): WizardNavState {
  const hiddenSteps: Record<string, boolean> = {};
  const hiddenSubsteps: Record<string, boolean> = {};
  steps.forEach((step, index) => {
    if (index > 0) hiddenSteps[step.key] = true;
    step.substeps.forEach((sub) => {
      if (sub.disabled) hiddenSubsteps[sub.key] = true;
    });
  });
  return { hiddenSteps, hiddenSubsteps };
}

/**
 * Keys disabled by banning the pathway after stepKey: every substep after it
 * in config order plus their parent steps. A substep key bans from its own
 * position (including its later siblings); a top-level step key bans after
 * that step's final substep. The cutoff's own parent step is never included,
 * so the step the user is on stays usable. Unknown key → [].
 */
export function getKeysBannedAfter(
  orderedKeys: string[],
  parentOf: (key: string) => string | null,
  stepKey: string
): string[] {
  let lastIdx = orderedKeys.indexOf(stepKey);
  if (lastIdx === -1) {
    orderedKeys.forEach((key, i) => {
      if (parentOf(key) === stepKey) lastIdx = i;
    });
  }
  if (lastIdx === -1) return [];

  const laterKeys = orderedKeys.slice(lastIdx + 1);
  const cutoffParentKey = parentOf(stepKey) ?? stepKey;
  const laterStepKeys = new Set(
    laterKeys
      .map((key) => parentOf(key) ?? key)
      .filter((key) => key !== cutoffParentKey)
  );
  return [...laterKeys, ...laterStepKeys];
}

/**
 * Derives the initial nav visibility state from the backend sidebar structure:
 * - All steps except the first (by order) start hidden.
 * - Substeps flagged Disabled in config start hidden.
 */
export function deriveInitialNavState(dtos: SidebarStepDto[]): WizardNavState {
  const sorted = dtos.slice().sort((a, b) => a.order - b.order);
  const hiddenSteps: Record<string, boolean> = {};
  sorted.slice(1).forEach((dto) => {
    hiddenSteps[dto.key] = true;
  });

  const hiddenSubsteps: Record<string, boolean> = {};
  sorted.forEach((dto) => {
    dto.children.forEach((c) => {
      if (c.disabled) hiddenSubsteps[c.key] = true;
    });
  });

  return { hiddenSteps, hiddenSubsteps };
}
