// ── Wizard domain types ───────────────────────────────────────────────────────

export interface WizardSubstep {
  key: string;
  label: string;
}

export interface WizardStep {
  /** Top-level step key, e.g. "step3". */
  key: string;
  /** Display number shown in the sidebar icon (fallback when no icon set). */
  number: number;
  /** Short title shown in the sidebar, e.g. "Related People". */
  title: string;
  /**
   * Font Awesome free-solid icon name, e.g. "book", "users".
   * When provided, replaces the number circle in the sidebar.
   */
  icon?: string;
  /**
   * Substep key to navigate to when the step header row is clicked.
   * Must be one of this step's substep keys.
   */
  defaultSubstep: string;
  substeps: WizardSubstep[];
}

export interface WizardNavState {
  hiddenSteps: Record<string, boolean>;
  hiddenSubsteps: Record<string, boolean>;
}

export type StepStatus = 'completed' | 'incomplete' | 'error';

export interface WizardState {
  activeSubstep: string;
  navState: WizardNavState;
  statusMap: Record<string, StepStatus>;
  disabledMap: Record<string, boolean>;
}

// ── Window function bridge types ──────────────────────────────────────────────

declare global {
  interface Window {
    // Navigation
    wizardUpdateSidebar?: (stepKey: string, sourceStepKey?: string) => void;
    wizardNavigateNext?: (sourceStepKey?: string) => void;
    wizardNavigatePrevious?: (sourceStepKey?: string) => void;
    // Validation (returns true/false; stub until Step 4)
    wizardValidateStep?: (substep: string, sourceStepKey?: string) => boolean;
    // Status
    wizardSetStepStatus?: (
      substep: string,
      status: StepStatus | null,
      sourceStepKey?: string
    ) => void;
    wizardSetAllStatuses?: (
      statusMap: Record<string, StepStatus>,
      sourceStepKey?: string
    ) => void;
    // Visibility
    wizardSetStepVisibility?: (
      stepKey: string,
      isVisible: boolean,
      sourceStepKey?: string
    ) => void;
    wizardSetSubstepVisibility?: (
      substepKey: string,
      isVisible: boolean,
      sourceStepKey?: string
    ) => void;
    wizardSetAllVisibility?: (
      nextState: WizardNavState,
      sourceStepKey?: string
    ) => void;
    // Disabled
    wizardSetStepClickable?: (
      stepKey: string,
      isClickable: boolean,
      sourceStepKey?: string
    ) => void;
    // Review edit navigation
    wizardGoToField?: (
      substepKey: string,
      fieldKey: string,
      sourceStepKey?: string
    ) => void;
    // Immediate save on navigation (bypasses the auto-save debounce)
    wizardSaveStep?: (
      substepKey: string,
      sourceStepKey?: string
    ) => Promise<void>;
    // Flush all pending auto-saves immediately (used by Save and Exit).
    wizardFlushAll?: () => Promise<void>;
    // Returns a save-ready payload (business data + wizard state metadata).
    wizardGetPersistedPayload?: () => unknown;
    // Callback registration (added in Step 4)
    wizardRegisterCallback?: (
      event: 'navigate' | 'validation',
      fn: (...args: unknown[]) => void
    ) => void;
  }
}
