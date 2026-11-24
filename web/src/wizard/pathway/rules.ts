/**
 * Declarative availability rules, one row per CHEFS script decision
 * (`source` names the schema component it replaces). Evaluated by
 * pathwayMachine.ts against facts.ts.
 *
 * Gate chain (each gate = own predicate ∧ previous gate):
 *   G1 deceasedContinued → G2 willPathOpen → G5 reviewValid → G6 assetsAnswered
 * so Step 7 = G2 ∧ G5 ∧ G6 and Step 8 = G2 ∧ G5.
 */
import type { PathwayFacts } from './facts';
import { RELATED_PEOPLE_BAN_CHECKS } from './facts';

export interface Gates {
  deceasedContinued: boolean;
  willPathOpen: boolean;
  reviewValid: boolean;
  assetsAnswered: boolean;
}

export type PathwayState =
  | 'deceased'
  | 'deceasedContinued'
  | 'willPathOpen'
  | 'reviewValid'
  | 'assetsAnswered';

export function computeGates(f: PathwayFacts): Gates {
  const deceasedContinued = f.step1Continued;
  const willPathOpen = deceasedContinued && f.willPathOpen;
  const reviewValid = willPathOpen && f.reviewValid;
  const assetsAnswered = reviewValid && f.assetsAnswered;
  return { deceasedContinued, willPathOpen, reviewValid, assetsAnswered };
}

/** Highest gate reached (for debugging / telemetry). */
export function currentState(g: Gates): PathwayState {
  if (g.assetsAnswered) return 'assetsAnswered';
  if (g.reviewValid) return 'reviewValid';
  if (g.willPathOpen) return 'willPathOpen';
  if (g.deceasedContinued) return 'deceasedContinued';
  return 'deceased';
}

export interface VisibilityRule {
  kind: 'step' | 'substep';
  target: string;
  visible: (f: PathwayFacts, g: Gates) => boolean;
  source: string;
}

export interface BanRule {
  /** Ban everything after this substep (config order). */
  after: string;
  active: (f: PathwayFacts) => boolean;
  source: string;
}

export interface ClickableRule {
  target: string;
  /** Only applied while this step itself isn't banned. */
  parentStep: string;
  when: (f: PathwayFacts) => boolean;
  source: string;
}

const notifyAudience = (f: PathwayFacts) =>
  f.peopleCount > 0 ||
  f.hasMinorOrIncapable ||
  f.isFirstNation ||
  f.hasSamePrioritySuccessors;

export const VISIBILITY_RULES: VisibilityRule[] = [
  // Step 1 — form-deceased
  {
    kind: 'step',
    target: 'form-testamentary-docs',
    visible: (_f, g) => g.deceasedContinued,
    source: 'form-deceased.step1ProgressionController',
  },
  {
    kind: 'substep',
    target: 'form-testamentary-docs',
    visible: (_f, g) => g.deceasedContinued,
    source: 'form-deceased.step1ProgressionController',
  },
  {
    kind: 'substep',
    target: 'form-assets-real-property-outside-bc',
    visible: (f) => !f.hideOutsideBcAssets,
    source: 'form-deceased.assetLocationVisibilityController',
  },
  {
    kind: 'substep',
    target: 'form-assets-tangible-personal-property-outside-bc',
    visible: (f) => !f.hideOutsideBcAssets,
    source: 'form-deceased.assetLocationVisibilityController',
  },

  // Step 2 — form-testamentary-docs guidedPathController (G2)
  ...['form-related-people', 'form-applicant', 'form-notify'].map(
    (target): VisibilityRule => ({
      kind: 'step',
      target,
      visible: (_f, g) => g.willPathOpen,
      source: 'form-testamentary-docs.guidedPathController',
    })
  ),
  ...[
    'form-spouse',
    'form-children',
    'form-creditors',
    'form-applicant-information',
    'form-full-review',
  ].map((target): VisibilityRule => ({
    kind: 'substep',
    target,
    visible: (_f, g) => g.willPathOpen,
    source: 'form-testamentary-docs.guidedPathController',
  })),

  // Step 3 — form-related-people
  {
    kind: 'substep',
    target: 'form-parent',
    visible: (f, g) => g.willPathOpen && f.parentVisible,
    source: 'form-related-people.parentVisibilityController',
  },
  {
    kind: 'substep',
    target: 'form-sibling',
    visible: (f, g) => g.willPathOpen && f.siblingVisible,
    source: 'form-related-people.siblingVisibilityController',
  },

  // Step 4 — form-applicant
  {
    kind: 'substep',
    target: 'form-citor',
    visible: (f, g) => g.willPathOpen && f.citorVisible,
    source: 'form-applicant.step4CitorVisibilityHelper / applicantCited',
  },

  // Step 5 — form-notify step5ReviewWatcher (G5)
  {
    kind: 'substep',
    target: 'form-tell-people',
    visible: (_f, g) => g.reviewValid,
    source: 'form-notify.step5ReviewWatcher',
  },
  {
    kind: 'substep',
    target: 'form-preview-p1',
    visible: (f, g) => g.reviewValid && notifyAudience(f),
    source: 'form-notify.step5ReviewWatcher',
  },
  {
    kind: 'substep',
    target: 'form-preview-pgt',
    visible: (f, g) => g.reviewValid && f.hasMinorOrIncapable,
    source: 'form-notify.step5ReviewWatcher',
  },
  {
    kind: 'substep',
    target: 'form-notify-people',
    visible: (f, g) => g.reviewValid && notifyAudience(f),
    source: 'form-notify.step5ReviewWatcher',
  },
  {
    kind: 'step',
    target: 'form-assets',
    visible: (_f, g) => g.reviewValid,
    source: 'form-notify.step5ReviewWatcher',
  },
  {
    kind: 'substep',
    target: 'form-assets-details',
    visible: (_f, g) => g.reviewValid,
    source: 'form-notify.step5ReviewWatcher',
  },
  // D1: Filing waits for a valid review; Step 2 no longer opens it.
  {
    kind: 'step',
    target: 'form-filing',
    visible: (_f, g) => g.reviewValid,
    source: 'form-notify.step5ReviewWatcher',
  },
  {
    kind: 'substep',
    target: 'form-filing-instructions',
    visible: (_f, g) => g.reviewValid,
    source: 'form-notify.step5ReviewWatcher',
  },

  // Step 6 — form-asset requiresP18 (G6). D2: sole controller of Step 7.
  {
    kind: 'step',
    target: 'form-application-documents',
    visible: (_f, g) => g.assetsAnswered,
    source: 'form-asset.requiresP18',
  },
  {
    kind: 'substep',
    target: 'form-preview-p5',
    visible: (_f, g) => g.assetsAnswered,
    source: 'form-asset.requiresP18',
  },
  {
    kind: 'substep',
    target: 'form-preview-p9',
    visible: (f, g) => g.assetsAnswered && f.notifyPeopleRowCount > 0,
    source: 'form-asset.requiresP18',
  },

  // Step 7 — form-application-documents formActivationTest
  {
    kind: 'substep',
    target: 'form-preview-p18',
    visible: (f, g) =>
      g.assetsAnswered && f.generateP18 && f.willsResultsReceived,
    source: 'form-application-documents.formActivationTest',
  },
  {
    kind: 'substep',
    target: 'form-preview-p10',
    visible: (f, g) =>
      g.assetsAnswered && f.generateP10 && f.willsResultsReceived,
    source: 'form-application-documents.formActivationTest',
  },
  {
    kind: 'substep',
    target: 'form-preview-p11',
    visible: (f, g) =>
      g.assetsAnswered && f.generateP11 && f.willsResultsReceived,
    source: 'form-application-documents.formActivationTest',
  },
  {
    kind: 'substep',
    target: 'form-preview-p2',
    visible: (f, g) => g.assetsAnswered && f.willsResultsReceived,
    source: 'form-application-documents.formActivationTest',
  },
];

/**
 * Ban points. All active ones are applied together, so the earliest wins and
 * clearing it exposes the next — one step's answers can no longer lift
 * another step's ban.
 */
export const BAN_RULES: BanRule[] = [
  ...RELATED_PEOPLE_BAN_CHECKS.map((c): BanRule => ({
    after: c.substep,
    active: (f) => f.relatedPeopleIncomplete[c.substep],
    source: 'form-related-people.relatedPeoplePathwayBanWatcher',
  })),
  {
    after: 'form-applicant-information',
    active: (f) => f.applicantInfoStop,
    source: 'form-applicant.applicantPathwayBanWatcher',
  },
  {
    after: 'form-applicant-service',
    active: (f) => f.applicantServiceStop,
    source: 'form-applicant.applicantPathwayBanWatcher',
  },
  {
    after: 'form-notify-people',
    active: (f) => f.notifyPeopleRowCount > 0 && !f.notifyPeopleComplete,
    source: 'form-notify.notifyPeopleValidationWatcher',
  },
  {
    after: 'form-preview-p5',
    active: (f) => f.willsResultsNotReceived,
    source: 'form-application-documents.formActivationTest',
  },
];

export const CLICKABLE_RULES: ClickableRule[] = [
  {
    target: 'form-assets-additional-information',
    parentStep: 'form-assets',
    when: (f) => f.cannotIdentifyAssets,
    source: 'form-asset.requiresP18',
  },
];
