import type { SidebarStepDto } from '@/types';
import type { WizardNavState, WizardStep } from '@/types/applicationStep';

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
      substeps: dto.children.map((c) => ({ key: c.key, label: c.title })),
    }));
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
