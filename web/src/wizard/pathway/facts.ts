/**
 * deriveFacts(accumulatedData) → flat, typed facts the pathway machine reads.
 * Every predicate is ported from a CHEFS schema script (named per field) so the
 * forms can later drop their availability logic. Pure: no store, no window.
 */
import {
  forEachRelatedPerson,
  isApplicant,
  isYes,
} from '@/utils/relatedPeople';
import {
  type ApplicantFacts,
  deriveApplicantFacts,
  type FormData,
  successorNames,
} from './applicantFacts';
import { missingReviewItems, type ReviewItem } from './reviewRequirements';

export interface PathwayFacts {
  /** form-deceased step1ProgressionController: "Continue" pressed. */
  step1Continued: boolean;
  /** form-deceased assetLocationVisibilityController. */
  hideOutsideBcAssets: boolean;
  /** form-testamentary-docs guidedPathController `show`. */
  willPathOpen: boolean;
  /** form-related-people parent/siblingVisibilityController. */
  parentVisible: boolean;
  siblingVisible: boolean;
  /** relatedPeoplePathwayBanWatcher: substeps whose "identified everyone?" is 'no'. */
  relatedPeopleIncomplete: Record<string, boolean>;
  applicant: ApplicantFacts;
  /** step4CitorVisibilityHelper / applicantCited customAction. */
  citorVisible: boolean;
  /** applicantPathwayBanWatcher. */
  applicantInfoStop: boolean;
  applicantServiceStop: boolean;
  reviewMissing: ReviewItem[];
  reviewValid: boolean;
  /** form-notify step5ReviewWatcher. */
  hasMinorOrIncapable: boolean;
  peopleCount: number;
  isFirstNation: boolean;
  hasSamePrioritySuccessors: boolean;
  /** form-notify notifyPeopleValidationWatcher. */
  notifyPeopleRowCount: number;
  notifyPeopleComplete: boolean;
  /** form-asset requiresP18. */
  assetsAnswered: boolean;
  cannotIdentifyAssets: boolean;
  /** form-application-documents formActivationTest. */
  willsResultsReceived: boolean;
  willsResultsNotReceived: boolean;
  generateP10: boolean;
  generateP11: boolean;
  generateP18: boolean;
}

export const RELATED_PEOPLE_BAN_CHECKS = [
  { container: 'spouse', field: 'spouseCompleted2', substep: 'form-spouse' },
  { container: 'child', field: 'childCompleted2', substep: 'form-children' },
  { container: 'parent', field: 'parentCompleted2', substep: 'form-parent' },
  { container: 'sibling', field: 'siblingCompleted2', substep: 'form-sibling' },
  {
    container: 'creditor',
    field: 'creditorCompleted',
    substep: 'form-creditors',
  },
] as const;

/** Any non-empty value (not blank string / empty array). */
function answered(v: unknown): boolean {
  if (v === undefined || v === null || v === '') return false;
  return !(Array.isArray(v) && v.length === 0);
}

export function deriveWillPathOpen(data: FormData): boolean {
  const d = data;
  const isActiveWillPath =
    d.HaveYouLookedForDeceasedWillWellYN === 'yes' && d.willExists === 'no';
  const searchResults =
    isActiveWillPath && d.willsNoticeResultsReceived === 'yes';
  const hasNoSearchResult = searchResults && d.willsNoticeHasResults === 'no';
  const hasFoundIndicatedWill =
    searchResults &&
    d.willsNoticeHasResults === 'yes' &&
    d.willsNoticeFoundIndicatedWills === 'yes';
  const hasMissingIndicatedWill =
    searchResults &&
    d.willsNoticeHasResults === 'yes' &&
    d.willsNoticeFoundIndicatedWills === 'no';

  // Only explicit banned answers are blocking.
  const isBlockedByWill =
    d.HaveYouLookedForDeceasedWillWellYN === 'no' || d.willExists === 'yes';
  const isBlockedByNoSearchResultDocument =
    hasNoSearchResult &&
    d.willsNoticeFoundWillOrOtherTestDocs === 'yes' &&
    d.willsNoticeRecognizeFoundWillOrOtherTestDocs === 'yes';
  const isBlockedByNoSearchResultTable =
    hasNoSearchResult &&
    d.willsNoticeFoundWillOrOtherTestDocs === 'yes' &&
    d.willsNoticeRecognizeFoundWillOrOtherTestDocs === 'no' &&
    d.willsNoticeOtherDocReasonsFinished === 'no';
  const isBlockedByFoundWill =
    hasFoundIndicatedWill &&
    d.willsNoticeIndicatedWillsOtherDocExists === 'no' &&
    d.willWantToRecognizedYN === 'yes';
  const isBlockedByFoundWillOtherDocument =
    hasFoundIndicatedWill &&
    d.willsNoticeIndicatedWillsOtherDocExists === 'yes' &&
    d.willsNoticeIndicatedWillsOtherDocRecognize1 === 'yes';
  const isBlockedByFoundWillTable =
    hasFoundIndicatedWill &&
    ((d.willsNoticeIndicatedWillsOtherDocExists === 'no' &&
      d.willWantToRecognizedYN === 'no') ||
      (d.willsNoticeIndicatedWillsOtherDocExists === 'yes' &&
        d.willsNoticeIndicatedWillsOtherDocRecognize1 === 'no')) &&
    d.willsNoticeIndicatedWillsOtherDocReasonsFinished === 'no';
  const isBlockedByMissingWillOtherDocument =
    hasMissingIndicatedWill &&
    d.willsNoticeDidNotFindIndicatedWillsOtherDocExists === 'yes' &&
    d.willsNoticeDidNotFindIndicatedWillsOtherDocRecognize === 'yes';
  const isBlockedByMissingWillTable =
    hasMissingIndicatedWill &&
    d.willsNoticeDidNotFindIndicatedWillsOtherDocExists === 'yes' &&
    d.willsNoticeDidNotFindIndicatedWillsOtherDocRecognize === 'no' &&
    d.willsNoticeDidNotFindIndicatedWillsOtherDocReasonsFinished === 'no';
  // Blocked only while the limitations are explicitly unconfirmed.
  const isBlockedByExistingGrant =
    d.willGrantExists === 'yes' && d.existedGrantConfirmLimitations === false;

  return (
    isActiveWillPath &&
    !isBlockedByWill &&
    !isBlockedByNoSearchResultDocument &&
    !isBlockedByNoSearchResultTable &&
    !isBlockedByFoundWill &&
    !isBlockedByFoundWillOtherDocument &&
    !isBlockedByFoundWillTable &&
    !isBlockedByMissingWillOtherDocument &&
    !isBlockedByMissingWillTable &&
    !isBlockedByExistingGrant
  );
}

export function deriveHideOutsideBcAssets(data: FormData): boolean {
  const norm = (v: unknown) =>
    typeof v === 'string' ? v.trim().toLowerCase() : '';
  const country = norm(data.deceasedAddressCountry);
  const province = norm(data.deceasedAddressProvince);
  return (
    (country !== '' && country !== 'canada') ||
    (province !== '' && province !== 'british columbia')
  );
}

const MINOR_CHECK_TYPES = new Set([
  'spouse',
  'child',
  'parent',
  'sibling',
  'creditorPerson',
  'grandchild',
]);
const COUNT_TYPES = new Set([
  'spouse',
  'child',
  'parent',
  'sibling',
  'creditorPerson',
  'creditorOrg',
  'citor',
  'grandchild',
  'intestateSuccessor',
]);

/** step5ReviewWatcher people pass (grandchild only when the parent child was bypassed). */
export function deriveNotifyAudience(data: FormData) {
  let hasMinorOrIncapable = false;
  let peopleCount = 0;
  forEachRelatedPerson(data, (person, config) => {
    if (config.key === 'grandchild') {
      const parentChild = person.parent?.record;
      if (
        !parentChild ||
        parentChild.status !== 'deceased' ||
        parentChild.died5DaysAfter
      )
        return;
    }
    if (
      MINOR_CHECK_TYPES.has(config.key) &&
      (person.status === 'minor' || person.status === 'incompetent-adult')
    ) {
      hasMinorOrIncapable = true;
    }
    if (COUNT_TYPES.has(config.key)) {
      if (person.name && !isApplicant(data, person.name)) peopleCount++;
      if (person.representative?.name) peopleCount++;
    }
  });
  return { hasMinorOrIncapable, peopleCount };
}

/** notifyPeopleValidationWatcher rowComplete. */
export function isNotifyRowComplete(row: FormData | null | undefined): boolean {
  if (!row || !row.recipientName) return false;
  if (row.p1Delivered === 'no' || !row.p1Delivered) return false;
  if (!row.delivererName || !row.deliveryDate || !row.deliveryMethod)
    return false;
  if (row.deliveryMethod === 'electronic') {
    return row.electronicReceipt === 'yes' && row.electronicRetain === true;
  }
  return true;
}

export function deriveFacts(data: FormData): PathwayFacts {
  const d = data ?? {};
  const applicant = deriveApplicantFacts(d.applicant);
  const ap: FormData = d.applicant ?? {};

  const citorVisible =
    ap.applicantCited === 'yes' && ap.applicantCitorNewExists === 'yes';

  const relatedPeopleIncomplete: Record<string, boolean> = {};
  for (const c of RELATED_PEOPLE_BAN_CHECKS) {
    relatedPeopleIncomplete[c.substep] = d[c.container]?.[c.field] === 'no';
  }

  const reviewMissing = missingReviewItems(d, { citorVisible });
  const { hasMinorOrIncapable, peopleCount } = deriveNotifyAudience(d);

  const notifyRows: unknown[] = Array.isArray(d.notifyPeople?.notifyPeopleData)
    ? d.notifyPeople.notifyPeopleData
    : [];

  const assetsAnswered =
    d.q42CanIdentifyAssets === 'yes' || d.q42CanIdentifyAssets === 'no';
  const assetsComplete = d.q424AssetsConfirmed === 'yes';
  const isBCAddress =
    d.deceasedAddressCountry === 'Canada' &&
    d.deceasedAddressProvince === 'British Columbia';

  return {
    // Older drafts may predate the persisted Continue flag; a Step 2 answer
    // proves Step 1 was continued.
    step1Continued:
      d.showStep1Items === true ||
      answered(d.HaveYouLookedForDeceasedWillWellYN),
    hideOutsideBcAssets: deriveHideOutsideBcAssets(d),
    willPathOpen: deriveWillPathOpen(d),
    parentVisible:
      d.spouse?.spouseExists === 'no' && d.child?.childExists === 'no',
    siblingVisible:
      d.spouse?.spouseExists === 'no' &&
      d.child?.childExists === 'no' &&
      d.parent?.parentExists === 'no',
    relatedPeopleIncomplete,
    applicant,
    citorVisible,
    applicantInfoStop: applicant.infoStop || applicant.courtStop,
    applicantServiceStop: applicant.lawyerStop,
    reviewMissing,
    reviewValid: reviewMissing.length === 0,
    hasMinorOrIncapable,
    peopleCount,
    isFirstNation: isYes(d.deceasedFirstNationYN),
    hasSamePrioritySuccessors:
      successorNames(ap.applicantSamePrioritySuccessorNames).length > 0,
    notifyPeopleRowCount: notifyRows.length,
    notifyPeopleComplete:
      notifyRows.length > 0 &&
      notifyRows.every((r) => isNotifyRowComplete(r as FormData)),
    assetsAnswered,
    cannotIdentifyAssets: d.q42CanIdentifyAssets === 'no',
    willsResultsReceived: d.willsNoticeResultsReceived === 'yes',
    willsResultsNotReceived: d.willsNoticeResultsReceived === 'no',
    generateP10: assetsComplete && isBCAddress,
    generateP11: assetsComplete && !isBCAddress,
    generateP18: assetsAnswered && !assetsComplete,
  };
}
