/**
 * Shared value helpers and the Step 4 applicant derivations, ported from the
 * CHEFS `form-applicant` / `form-notify` scripts. Those scripts repeat the same
 * derivations ~15 times; this is the single Vue-side copy used by the pathway
 * bans (applicantPathwayBanWatcher) and the Step 5 review rules
 * (reviewSection4aTableHtml row visibility).
 */

export type FormData = Record<string, any>;

export const isYesNo = (v: unknown): boolean => v === 'yes' || v === 'no';

/** `String(v || '').trim()` — the CHEFS scripts' blank check. */
export const trimmed = (v: unknown): string => String(v || '').trim();

/** Review address cell: any one non-empty part is enough. */
export function joinAddress(source: FormData | undefined, prefix: string) {
  const s = source ?? {};
  return [
    s[`${prefix}Street`],
    s[`${prefix}City`],
    s[`${prefix}Province`] || s[`${prefix}State`] || s[`${prefix}ProvinceText`],
    s[`${prefix}PostalCode`],
  ]
    .filter(Boolean)
    .join(', ');
}

/** True when the datagrid has at least one row with a non-blank name. */
export function hasNamedRow(list: unknown, nameKey: string): boolean {
  return (
    Array.isArray(list) &&
    list.some((r) => r && r[nameKey] && String(r[nameKey]).trim() !== '')
  );
}

/** Non-blank `applicantSamePrioritySuccessorNames` entries (datagrid or scalar). */
export function successorNames(raw: unknown): string[] {
  const list = Array.isArray(raw) ? raw : raw ? [raw] : [];
  return list
    .map((s) =>
      String(
        (s && typeof s === 'object' ? s.applicantSuccessorEntry : s) || ''
      ).trim()
    )
    .filter(Boolean);
}

export type NominatorKind = '' | 'spouse' | 'child' | 'intestate';

export interface ApplicantFacts {
  other: boolean;
  cred: string;
  gc: boolean;
  child: boolean;
  gcPriority: boolean;
  gcDirect: boolean;
  childSpousePath: boolean;
  childDirect: boolean;
  kind: NominatorKind;
  consentOk: boolean;
  nominated: boolean;
  notNominated: boolean;
  /** Calculated applicantName, or rebuilt from the selection when missing. */
  applicantName: string;
  hasName: boolean;
  /** Not other / creditor / grandchild: the plain relative path. */
  base: boolean;
  individualReady: boolean;
  courtPath: boolean;
  orgPath: boolean;
  orgReady: boolean;
  courtIndividualReady: boolean;
  successorPath: boolean;
  hasNames: boolean;
  successorReady: boolean;
  infoStop: boolean;
  successorCourtStop: boolean;
  courtStop: boolean;
  lawyerStop: boolean;
}

function nominatorKind(nominator: string): NominatorKind {
  if (nominator.indexOf('spouse::') === 0) return 'spouse';
  if (nominator.indexOf('child::') === 0) return 'child';
  return nominator ? 'intestate' : '';
}

export function deriveApplicantFacts(
  applicant: FormData | undefined
): ApplicantFacts {
  const a = applicant ?? {};
  const other = a.applicantNameSelection === '__other__';
  const cred = String(a.applicantCreditorType || '');
  const gc = !!a.applicantIsGrandchild;
  const child = !!a.applicantIsChild;
  const gcPriority =
    gc && !!(a.applicantGrandchildHasSpouse || a.applicantGrandchildHasChild);
  const gcDirect = gc && !gcPriority;
  const childSpousePath = child && !!a.applicantChildHasSpouse;
  const childDirect = child && !childSpousePath;

  let kind = nominatorKind(String(a.applicantNominator || ''));
  const gcAllowed: NominatorKind = a.applicantGrandchildHasSpouse
    ? 'spouse'
    : a.applicantGrandchildHasChild
      ? 'child'
      : '';
  if (gc && kind !== gcAllowed) kind = '';
  if (child && kind !== (a.applicantChildHasSpouse ? 'spouse' : '')) kind = '';

  const consentOk =
    kind === 'spouse' ||
    (kind === 'child' && a.applicantChildConsent === 'yes') ||
    (kind === 'intestate' && a.applicantIntestateConsent === 'yes');
  const nominated =
    (other || !!cred || gcPriority || childSpousePath) &&
    a.applicantHasBeenNominated === 'yes';
  const notNominated = other && a.applicantHasBeenNominated === 'no';

  const applicantName =
    a.applicantName ||
    (other ? trimmed(a.applicantOtherName) : trimmed(a.applicantNameSelection));
  const hasName = !!applicantName;
  const base = !other && !cred && !gc;

  const individualReady =
    nominated &&
    consentOk &&
    (gc || (cred ? cred === 'individual' : a.applicantNomineeIsOrg === 'no'));
  const courtPath =
    (other &&
      a.applicantHasBeenNominated === 'no' &&
      a.applicantIsOtherSuccessor === 'no') ||
    (!!cred && a.applicantHasBeenNominated === 'no') ||
    (gcPriority && a.applicantHasBeenNominated === 'no') ||
    (childSpousePath && a.applicantHasBeenNominated === 'no');
  const orgPath =
    (nominated &&
      consentOk &&
      !gc &&
      (cred ? cred === 'organization' : a.applicantNomineeIsOrg === 'yes')) ||
    (courtPath &&
      a.applicantCourtAppointed === 'yes' &&
      !gc &&
      (cred ? cred === 'organization' : a.applicantCourtOrg === 'yes'));
  const orgReady =
    orgPath &&
    a.applicantOrgAuthorized === 'yes' &&
    !!trimmed(a.applicantOrgTitle) &&
    !!trimmed(a.applicantOrgAddressStreet);
  const courtIndividualReady =
    courtPath &&
    a.applicantCourtAppointed === 'yes' &&
    (gc ||
      (cred
        ? cred === 'individual'
        : a.applicantCourtOrg === 'no' &&
          !!trimmed(a.applicantCourtRelationship)));
  const successorPath = notNominated && a.applicantIsOtherSuccessor === 'yes';
  const hasNames =
    successorNames(a.applicantSamePrioritySuccessorNames).length > 0;
  const successorReady =
    (successorPath &&
      (a.applicantStep3PeopleExist !== true ||
        a.applicantCourtAppointed === 'yes') &&
      (a.applicantHasSamePrioritySuccessors === 'no' ||
        (a.applicantHasSamePrioritySuccessors === 'yes' &&
          hasNames &&
          (a.applicantStep3PeopleExist === true ||
            isYesNo(a.applicantMajorityInInterestConsent))))) ||
    (gcDirect && isYesNo(a.applicantMajorityInInterestConsent));

  // applicantPathwayBanWatcher
  const infoStop =
    (nominated && kind === 'child' && a.applicantChildConsent === 'no') ||
    (nominated &&
      kind === 'intestate' &&
      a.applicantIntestateConsent === 'no') ||
    (nominated && String(a.applicantNominatorOptionsSync) === '0') ||
    (nominated &&
      consentOk &&
      !gc &&
      !child &&
      (cred ? cred === 'organization' : a.applicantNomineeIsOrg === 'yes') &&
      a.applicantOrgAuthorized === 'no');
  const successorCourtStop =
    other &&
    a.applicantHasBeenNominated === 'no' &&
    a.applicantIsOtherSuccessor === 'yes' &&
    a.applicantStep3PeopleExist === true &&
    a.applicantCourtAppointed === 'no';
  const courtStop =
    successorCourtStop ||
    (courtPath &&
      (a.applicantCourtAppointed === 'no' ||
        (a.applicantCourtAppointed === 'yes' &&
          !gc &&
          !child &&
          (cred ? cred === 'organization' : a.applicantCourtOrg === 'yes') &&
          a.applicantOrgAuthorized === 'no')));
  const lawyerStop = a.applicantHasLawyer === 'yes';

  return {
    other,
    cred,
    gc,
    child,
    gcPriority,
    gcDirect,
    childSpousePath,
    childDirect,
    kind,
    consentOk,
    nominated,
    notNominated,
    applicantName: String(applicantName || ''),
    hasName,
    base,
    individualReady,
    courtPath,
    orgPath,
    orgReady,
    courtIndividualReady,
    successorPath,
    hasNames,
    successorReady,
    infoStop,
    successorCourtStop,
    courtStop,
    lawyerStop,
  };
}

/**
 * Citation question shown, as reviewSection4cTableHtml computes it. This
 * variant differs from the applicant form (no child-path handling, stricter
 * successorReady, no court-individual path) — kept as-is so the Citor review
 * row matches what the review renders today.
 */
export function citationShownForCitorReview(
  applicant: FormData | undefined
): boolean {
  const ap = applicant ?? {};
  const other = ap.applicantNameSelection === '__other__';
  const cred = String(ap.applicantCreditorType || '');
  const gc = !!ap.applicantIsGrandchild;
  const gcPriority =
    gc && !!(ap.applicantGrandchildHasSpouse || ap.applicantGrandchildHasChild);
  const gcDirect = gc && !gcPriority;
  let kind = nominatorKind(String(ap.applicantNominator || ''));
  const gcAllowed: NominatorKind = ap.applicantGrandchildHasSpouse
    ? 'spouse'
    : ap.applicantGrandchildHasChild
      ? 'child'
      : '';
  if (gc && kind !== gcAllowed) kind = '';
  const consentOk =
    kind === 'spouse' ||
    (kind === 'child' && ap.applicantChildConsent === 'yes') ||
    (kind === 'intestate' && ap.applicantIntestateConsent === 'yes');
  const nominated =
    (other || !!cred || gcPriority) && ap.applicantHasBeenNominated === 'yes';
  const individualReady =
    nominated &&
    consentOk &&
    (gc || (cred ? cred === 'individual' : ap.applicantNomineeIsOrg === 'no'));
  const orgReady =
    nominated &&
    consentOk &&
    !gc &&
    (cred ? cred === 'organization' : ap.applicantNomineeIsOrg === 'yes') &&
    ap.applicantOrgAuthorized === 'yes' &&
    !!trimmed(ap.applicantOrgTitle) &&
    !!trimmed(ap.applicantOrgAddressStreet);
  const notNominated = other && ap.applicantHasBeenNominated === 'no';
  const successorPath = notNominated && ap.applicantIsOtherSuccessor === 'yes';
  const hasNames =
    successorNames(ap.applicantSamePrioritySuccessorNames).length > 0;
  const successorReady =
    (successorPath &&
      (ap.applicantHasSamePrioritySuccessors === 'no' ||
        (ap.applicantHasSamePrioritySuccessors === 'yes' &&
          hasNames &&
          ap.applicantMajorityInInterestConsent === 'yes'))) ||
    (gcDirect && isYesNo(ap.applicantMajorityInInterestConsent));
  const hasName = other
    ? !!trimmed(ap.applicantOtherName)
    : !!(ap.applicantNameSelection || ap.applicantName);
  return (
    hasName &&
    ((!other && !cred && !gc) || individualReady || orgReady || successorReady)
  );
}
