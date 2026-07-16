import type { WizardNavState, WizardStep } from '@/types/applicationStep';

/**
 * Static step configuration for the Probate application wizard.
 * Extracted from the CHEFS "step wizard schema (raw).json".
 *
 * To add a step: append an entry here. No other code changes needed.
 * To hide a step on load: add its key to PROBATE_WIZARD_INITIAL_NAV_STATE.hiddenSteps.
 */
export const PROBATE_WIZARD_STEPS: WizardStep[] = [
  {
    key: 'step1',
    number: 1,
    title: 'Deceased',
    icon: 'skull',
    defaultSubstep: 'step1',
    substeps: [{ key: 'step1', label: 'Information About Deceased' }],
  },
  {
    key: 'step2',
    number: 2,
    title: "Deceased's Will",
    icon: 'book',
    defaultSubstep: 'step2',
    substeps: [{ key: 'step2', label: "Deceased's Will" }],
  },
  {
    key: 'step3',
    number: 3,
    title: 'Related People',
    icon: 'users',
    defaultSubstep: 'step3_spouse',
    substeps: [
      { key: 'step3_spouse', label: 'Spouse' },
      { key: 'step3_children', label: 'Children' },
      { key: 'step3_creditors', label: 'Creditors' },
    ],
  },
  {
    key: 'step4',
    number: 4,
    title: 'Applicant',
    icon: 'user-tie',
    defaultSubstep: 'step4_information_about_applicant',
    substeps: [
      {
        key: 'step4_information_about_applicant',
        label: 'Information About Applicant',
      },
      { key: 'step4_citor', label: 'Citor' },
      { key: 'step4_applicant_service', label: 'Applicant Service' },
    ],
  },
  {
    key: 'step5',
    number: 5,
    title: 'Notify',
    icon: 'envelope-open-text',
    defaultSubstep: 'step5_review_your_answers',
    substeps: [
      { key: 'step5_review_your_answers', label: 'Review Your Answers' },
      {
        key: 'step5_tell_people_you_are_applying',
        label: 'Tell People You Are Applying',
      },
      { key: 'step5_preview_p1', label: 'Preview P1' },
      { key: 'step5_preview_pgt', label: 'Preview PGT' },
      { key: 'step5_notify_people', label: 'Notify People' },
    ],
  },
  {
    key: 'step6',
    number: 6,
    title: 'Assets',
    icon: 'coins',
    defaultSubstep: 'step6_assets_details',
    substeps: [{ key: 'step6_assets_details', label: 'Assets Details' }],
  },
  {
    key: 'step7',
    number: 7,
    title: 'Application Documents',
    icon: 'file-lines',
    defaultSubstep: 'step7_preview_p9',
    substeps: [
      { key: 'step7_preview_p9', label: 'Preview P9' },
      { key: 'step7_form_p5', label: 'Fill out a Form P5' },
      { key: 'step7_form_p10', label: 'Fill out a Form P10' },
      {
        key: 'step7_search_of_wills_notice',
        label: 'Fill out an Application for Search of Wills Notice',
      },
      { key: 'step7_form_p2', label: 'Fill out a Form P2' },
    ],
  },
];

/**
 * Initial visibility state: only step1 visible on load.
 * Matches the CHEFS schema default wizardNavState.
 */
export const PROBATE_WIZARD_INITIAL_NAV_STATE: WizardNavState = {
  hiddenSteps: {
    step2: true,
    step3: true,
    step4: true,
    step5: true,
    step6: true,
    step7: true,
  },
  hiddenSubsteps: {
    step4_citor: true,
  },
};

/**
 * Initially disabled (not clickable) substeps.
 * Matches the CHEFS schema default wizardDisabledSteps.
 */
export const PROBATE_WIZARD_INITIAL_DISABLED_STEPS: Record<string, boolean> = {
  step3_children: true,
  step3_creditors: true,
};
