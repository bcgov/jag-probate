/**
 * Vue port of every "Required" cell of the Step 5 review (`form-notify`
 * reviewSection*TableHtml scripts). A row is missing when the review would
 * render it (row-visibility rule holds) with an empty value. `substep`/`field`
 * match the review's edit-button data-step/data-field so the list can drive
 * window.wizardGoToField.
 *
 * Keep in sync with the CHEFS review until the forms are cleaned up: the form
 * still renders its own badges (display only).
 */
import {
  citationShownForCitorReview,
  deriveApplicantFacts,
  type FormData,
  hasNamedRow,
  isYesNo,
  joinAddress,
  successorNames,
  trimmed,
} from './applicantFacts';

export interface ReviewItem {
  section: string;
  label: string;
  substep: string;
  field: string;
}

export interface WillSectionRow {
  label: string;
  /** '' renders the Required badge. */
  value: string;
  field: string;
}

export interface WillSectionState {
  isValid: boolean;
  rows: WillSectionRow[];
}

/**
 * Port of `window.computeWillSectionState` (the `form-notify` copy, which is
 * the one the review renders with; the `form-testamentary-docs` copy predates
 * the empty-list early return and the will-not-recognized → document-list
 * branch).
 */
export function computeWillSectionState(d: FormData): WillSectionState {
  const apos = "'";
  const yn = (v: unknown) => (v === 'yes' ? 'Yes' : v === 'no' ? 'No' : '');
  const name = d.deceasedName || 'the deceased';
  const rows: WillSectionRow[] = [];
  const add = (label: string, val: unknown, field: string) =>
    rows.push({ label, value: val ? String(val) : '', field });
  const invalid = (): WillSectionState => ({ isValid: false, rows });

  function docsSummary(list: unknown): string {
    if (!Array.isArray(list) || !list.length) return '';
    const parts: string[] = [];
    for (const r of list) {
      if (r && r.type)
        parts.push(r.type + (r.identifier ? ' (' + r.identifier + ')' : ''));
    }
    return parts.length
      ? parts.length + ' document(s): ' + parts.join(', ')
      : '';
  }

  function grantStage(): WillSectionState {
    add(
      'Does a Representation Grant for ' + name + ' already exist?',
      yn(d.willGrantExists),
      'willGrantExists'
    );
    if (d.willGrantExists === 'yes') {
      add(
        'I confirm the limitations do not apply to my situation.',
        d.existedGrantConfirmLimitations === true ? 'Confirmed' : '',
        'existedGrantConfirmLimitations'
      );
      if (d.existedGrantConfirmLimitations !== true) return invalid();
      add(
        'Please list the grant(s) of probate or administration, or equivalent, issued in British Columbia or another jurisdiction.',
        d.existedGrantsIssuedTxt,
        'existedGrantsIssuedTxt'
      );
      add(
        'Please list the reasons why you believe the grant(s), or equivalent, are not relevant to this application.',
        d.existedGrantNotRelevantReasonsTxt,
        'existedGrantNotRelevantReasonsTxt'
      );
      return {
        isValid:
          !!d.existedGrantsIssuedTxt && !!d.existedGrantNotRelevantReasonsTxt,
        rows,
      };
    }
    if (d.willGrantExists === 'no') {
      add(
        'Are you aware of anyone else who has applied or is applying for a grant relating to ' +
          name +
          apos +
          '?',
        yn(d.noExistingGrantAwareOfOtherApplicantsYesNo),
        'noExistingGrantAwareOfOtherApplicantsYesNo'
      );
      if (d.noExistingGrantAwareOfOtherApplicantsYesNo === 'no')
        return { isValid: true, rows };
      if (d.noExistingGrantAwareOfOtherApplicantsYesNo === 'yes') {
        add(
          'What are the names of the persons who have applied?',
          d.noExistingGrantOtherApplicantsNamesTxt,
          'noExistingGrantOtherApplicantsNamesTxt'
        );
        return { isValid: !!d.noExistingGrantOtherApplicantsNamesTxt, rows };
      }
    }
    return invalid();
  }

  function docsListStage(
    editorField: string,
    finishedField: string
  ): WillSectionState {
    const summary = docsSummary(d[editorField]);
    add('Testamentary Documents Listed', summary, editorField);
    if (!summary) return invalid();
    add(
      'Have you finished listing all other testamentary documents?',
      yn(d[finishedField]),
      finishedField
    );
    if (d[finishedField] === 'yes') return grantStage();
    return invalid();
  }

  add(
    'Have you looked for ' + name + apos + 's will?',
    yn(d.HaveYouLookedForDeceasedWillWellYN),
    'HaveYouLookedForDeceasedWillWellYN'
  );
  if (d.HaveYouLookedForDeceasedWillWellYN !== 'yes') return invalid();

  add('Did ' + name + ' make a Will?', yn(d.willExists), 'willExists');
  if (d.willExists !== 'no') return invalid();

  add(
    'Have you received Results of Search for Wills Notice from Vital Statistics?',
    yn(d.willsNoticeResultsReceived),
    'willsNoticeResultsReceived'
  );
  if (d.willsNoticeResultsReceived === 'no') return grantStage();
  if (d.willsNoticeResultsReceived !== 'yes') return invalid();

  add(
    'Did the Search for Wills Notice indicate any results?',
    yn(d.willsNoticeHasResults),
    'willsNoticeHasResults'
  );

  if (d.willsNoticeHasResults === 'no') {
    add(
      'In your search of ' +
        name +
        apos +
        's belongings, did you find a will or other testamentary document(s)?',
      yn(d.willsNoticeFoundWillOrOtherTestDocs),
      'willsNoticeFoundWillOrOtherTestDocs'
    );
    if (d.willsNoticeFoundWillOrOtherTestDocs === 'no') return grantStage();
    if (d.willsNoticeFoundWillOrOtherTestDocs === 'yes') {
      add(
        'Do you want to have the will and/or other testamentary document(s) recognized as ' +
          name +
          apos +
          's will?',
        yn(d.willsNoticeRecognizeFoundWillOrOtherTestDocs),
        'willsNoticeRecognizeFoundWillOrOtherTestDocs'
      );
      if (d.willsNoticeRecognizeFoundWillOrOtherTestDocs === 'no') {
        return docsListStage(
          'willsNoticeOtherTestamentaryDocumentsEditor',
          'willsNoticeOtherDocReasonsFinished'
        );
      }
    }
    return invalid();
  }

  if (d.willsNoticeHasResults === 'yes') {
    add(
      'Did you find the will(s) indicated on the Wills Notice?',
      yn(d.willsNoticeFoundIndicatedWills),
      'willsNoticeFoundIndicatedWills'
    );

    if (d.willsNoticeFoundIndicatedWills === 'yes') {
      add(
        'Did ' + name + ' make any other testamentary documents?',
        yn(d.willsNoticeIndicatedWillsOtherDocExists),
        'willsNoticeIndicatedWillsOtherDocExists'
      );
      if (d.willsNoticeIndicatedWillsOtherDocExists === 'no') {
        add(
          'Do you want to have the will(s) recognized as ' +
            name +
            apos +
            's will?',
          yn(d.willWantToRecognizedYN),
          'willWantToRecognizedYN'
        );
        if (d.willWantToRecognizedYN === 'no') {
          return docsListStage(
            'willsNoticeIndicatedWillsOtherTestamentaryDocumentsEditor',
            'willsNoticeIndicatedWillsOtherDocReasonsFinished'
          );
        }
        return invalid();
      }
      if (d.willsNoticeIndicatedWillsOtherDocExists === 'yes') {
        add(
          'Do you want to have the will(s) and/or other testamentary documents recognized as ' +
            name +
            apos +
            's Will?',
          yn(d.willsNoticeIndicatedWillsOtherDocRecognize1),
          'willsNoticeIndicatedWillsOtherDocRecognize1'
        );
        if (d.willsNoticeIndicatedWillsOtherDocRecognize1 === 'no') {
          return docsListStage(
            'willsNoticeIndicatedWillsOtherTestamentaryDocumentsEditor',
            'willsNoticeIndicatedWillsOtherDocReasonsFinished'
          );
        }
      }
      return invalid();
    }

    if (d.willsNoticeFoundIndicatedWills === 'no') {
      add(
        'Did ' + name + ' make any other testamentary documents?',
        yn(d.willsNoticeDidNotFindIndicatedWillsOtherDocExists),
        'willsNoticeDidNotFindIndicatedWillsOtherDocExists'
      );
      if (d.willsNoticeDidNotFindIndicatedWillsOtherDocExists === 'no')
        return grantStage();
      if (d.willsNoticeDidNotFindIndicatedWillsOtherDocExists === 'yes') {
        add(
          'Do you want to have these other testamentary documents recognized as ' +
            name +
            apos +
            's Will?',
          yn(d.willsNoticeDidNotFindIndicatedWillsOtherDocRecognize),
          'willsNoticeDidNotFindIndicatedWillsOtherDocRecognize'
        );
        if (d.willsNoticeDidNotFindIndicatedWillsOtherDocRecognize === 'no') {
          return docsListStage(
            'willsNoticeDidNotFindIndicatedWillsOtherTestamentaryDocumentsEditor',
            'willsNoticeDidNotFindIndicatedWillsOtherDocReasonsFinished'
          );
        }
      }
      return invalid();
    }
  }

  return invalid();
}

export interface ReviewOptions {
  /** Machine result for the `form-citor` substep (review's wizardIsSubstepVisible). */
  citorVisible: boolean;
}

export function missingReviewItems(
  data: FormData,
  { citorVisible }: ReviewOptions
): ReviewItem[] {
  const d = data ?? {};
  const missing: ReviewItem[] = [];
  const req = (
    section: string,
    label: string,
    substep: string,
    field: string
  ) => missing.push({ section, label, substep, field });

  // ── 1 Deceased ──
  const S1 = 'form-deceased';
  const fullName = [
    d.firstNameGivenName,
    d.middleNameS,
    d.lastNameFamilyNameOrSurname,
  ]
    .filter(Boolean)
    .join(' ');
  if (!fullName) req('1', 'Name of the deceased', S1, 'firstNameGivenName');
  if (!d.deceasedDeathDate) req('1', 'Date of death', S1, 'deceasedDeathDate');
  if (!joinAddress(d, 'deceasedAddress'))
    req('1', 'Last known address', S1, 'deceasedAddressStreet');
  if (!isYesNo(d.deceasedFirstNationYN))
    req('1', 'Nisga’a / Treaty First Nation', S1, 'deceasedFirstNationYN');
  if (d.deceasedFirstNationYN === 'yes' && !d.firstNationMembershipDisplayName)
    req('1', 'First Nation membership', S1, 'firstNationMembership');

  // ── 2 Will ──
  for (const row of computeWillSectionState(d).rows) {
    if (!row.value) req('2', row.label, 'form-testamentary-docs', row.field);
  }

  // ── 3 Related people ──
  const people: Array<[string, string, string, string, string, string]> = [
    ['3a', 'spouse', 'spouseExists', 'spouseData', 'spouseName', 'form-spouse'],
    ['3b', 'child', 'childExists', 'childData', 'childName', 'form-children'],
    ['3e', 'parent', 'parentExists', 'parentData', 'parentName', 'form-parent'],
    [
      '3f',
      'sibling',
      'siblingExists',
      'siblingData',
      'siblingName',
      'form-sibling',
    ],
    [
      '3c',
      'creditor',
      'creditorPersonExists',
      'creditorPersonData',
      'creditorPersonName',
      'form-creditors',
    ],
    [
      '3d',
      'creditor',
      'creditorOrgExists',
      'creditorOrgData',
      'creditorOrgName',
      'form-creditors',
    ],
  ];
  for (const [
    section,
    container,
    existsKey,
    listKey,
    nameKey,
    substep,
  ] of people) {
    const c = d[container] ?? {};
    if (c[existsKey] === 'yes' && !hasNamedRow(c[listKey], nameKey))
      req(section, `${container} information`, substep, existsKey);
  }

  // ── 4a Applicant ──
  const S4A = 'form-applicant-information';
  const ap: FormData = d.applicant ?? {};
  const f = deriveApplicantFacts(ap);
  const yn4a = (
    visible: boolean,
    value: unknown,
    label: string,
    field: string
  ) => {
    if (visible && !isYesNo(value)) req('4a', label, S4A, field);
  };
  const txt4a = (
    visible: boolean,
    value: unknown,
    label: string,
    field: string
  ) => {
    if (visible && !trimmed(value)) req('4a', label, S4A, field);
  };

  const nameShown = f.other
    ? trimmed(ap.applicantOtherName)
    : String(ap.applicantNameSelection || ap.applicantName || '').trim();
  if (!nameShown)
    req(
      '4a',
      'Applicant name',
      S4A,
      f.other ? 'applicantOtherName' : 'applicantNameSelection'
    );

  const contact =
    f.hasName &&
    (f.base || f.individualReady || f.courtIndividualReady || f.successorReady);
  const contactOrOrg = f.hasName && (contact || f.orgReady);
  if (contact && !ap.applicantOccupation)
    req('4a', 'Occupation', S4A, 'applicantOccupation');
  if (contact && !joinAddress(ap, 'applicantOrdinaryAddress'))
    req('4a', 'Ordinary address', S4A, 'applicantOrdinaryAddressStreet');
  yn4a(
    contactOrOrg,
    ap.applicantOrdinaryAddressReceiveMail,
    'Receive mail at ordinary address',
    'applicantOrdinaryAddressReceiveMail'
  );
  if (
    contactOrOrg &&
    ap.applicantOrdinaryAddressReceiveMail === 'no' &&
    !joinAddress(ap, 'applicantMailingAddress')
  )
    req('4a', 'Mailing address', S4A, 'applicantMailingAddressStreet');
  yn4a(contactOrOrg, ap.applicantCited, 'Cited', 'applicantCited');
  yn4a(
    contactOrOrg && ap.applicantCited === 'yes',
    ap.applicantCitorNewExists,
    'Citor not listed',
    'applicantCitorNewExists'
  );

  const nominatedRowShown = !!(
    (f.other && trimmed(ap.applicantOtherName)) ||
    f.cred ||
    f.gcPriority ||
    (f.child && ap.applicantChildHasSpouse)
  );
  yn4a(
    nominatedRowShown,
    ap.applicantHasBeenNominated,
    'Nominated',
    'applicantHasBeenNominated'
  );
  if (f.nominated && !ap.applicantNominator)
    req('4a', 'Nominator', S4A, 'applicantNominator');
  yn4a(
    f.nominated && f.kind === 'child',
    ap.applicantChildConsent,
    'Child consent',
    'applicantChildConsent'
  );
  yn4a(
    f.nominated && f.kind === 'intestate',
    ap.applicantIntestateConsent,
    'Intestate consent',
    'applicantIntestateConsent'
  );
  yn4a(
    f.nominated && f.consentOk && !f.cred && !f.gc && !f.child,
    ap.applicantNomineeIsOrg,
    'Nominee is an organization',
    'applicantNomineeIsOrg'
  );
  txt4a(
    f.orgPath && f.cred !== 'organization',
    ap.applicantOrgName,
    'Organization name',
    'applicantOrgName'
  );
  txt4a(
    creditorRepRowShown(ap),
    ap.applicantCreditorRepName,
    'Creditor representative',
    'applicantCreditorRepName'
  );
  yn4a(
    f.orgPath &&
      !!trimmed(ap.applicantOrgName) &&
      (f.cred !== 'organization' || !!trimmed(ap.applicantCreditorRepName)),
    ap.applicantOrgAuthorized,
    'Authorized by organization',
    'applicantOrgAuthorized'
  );
  txt4a(
    f.orgPath && ap.applicantOrgAuthorized === 'yes',
    ap.applicantOrgTitle,
    'Title in organization',
    'applicantOrgTitle'
  );
  if (
    f.orgPath &&
    ap.applicantOrgAuthorized === 'yes' &&
    trimmed(ap.applicantOrgTitle) &&
    !joinAddress(ap, 'applicantOrgAddress')
  )
    req('4a', 'Organization address', S4A, 'applicantOrgAddressStreet');
  yn4a(
    f.notNominated,
    ap.applicantIsOtherSuccessor,
    'Other intestate successor',
    'applicantIsOtherSuccessor'
  );
  const samePriorityShown =
    f.successorPath &&
    (ap.applicantStep3PeopleExist !== true ||
      ap.applicantCourtAppointed === 'yes');
  yn4a(
    samePriorityShown,
    ap.applicantHasSamePrioritySuccessors,
    'Same-priority successors',
    'applicantHasSamePrioritySuccessors'
  );
  if (
    samePriorityShown &&
    ap.applicantHasSamePrioritySuccessors === 'yes' &&
    !successorNames(ap.applicantSamePrioritySuccessorNames).length
  )
    req(
      '4a',
      'Same-priority successor names',
      S4A,
      'applicantSamePrioritySuccessorNames'
    );
  yn4a(
    f.gcDirect ||
      !!ap.applicantIsSibling ||
      f.childDirect ||
      (f.successorPath &&
        ap.applicantStep3PeopleExist !== true &&
        ap.applicantHasSamePrioritySuccessors === 'yes' &&
        f.hasNames),
    ap.applicantMajorityInInterestConsent,
    'Majority in interest consent',
    'applicantMajorityInInterestConsent'
  );
  yn4a(
    f.courtPath ||
      (f.other &&
        ap.applicantHasBeenNominated === 'no' &&
        ap.applicantIsOtherSuccessor === 'yes' &&
        ap.applicantStep3PeopleExist === true),
    ap.applicantCourtAppointed,
    'Court appointed',
    'applicantCourtAppointed'
  );
  yn4a(
    f.courtPath &&
      ap.applicantCourtAppointed === 'yes' &&
      !f.cred &&
      !f.gc &&
      !f.child,
    ap.applicantCourtOrg,
    'Court appointed organization',
    'applicantCourtOrg'
  );
  txt4a(
    f.courtPath &&
      ap.applicantCourtAppointed === 'yes' &&
      ap.applicantCourtOrg === 'no',
    ap.applicantCourtRelationship,
    'Relationship to the deceased',
    'applicantCourtRelationship'
  );

  // ── 4c Citor ── (rendered after 4a in the review)
  if (
    citorVisible &&
    citationShownForCitorReview(ap) &&
    ap.applicantCited === 'yes' &&
    ap.applicantCitorNewExists === 'yes' &&
    !hasNamedRow(ap.citorData, 'citorName')
  )
    req('4c', 'Citor information', 'form-citor', 'applicantCitorNewExists');

  // ── 4b Service ── (courthouse row always has a label, never Required)
  const S4B = 'form-applicant-service';
  const noLawyer = ap.applicantHasLawyer === 'no';
  if (!isYesNo(ap.applicantHasLawyer))
    req('4b', 'Has a lawyer', S4B, 'applicantHasLawyer');
  if (noLawyer) {
    if (!joinAddress(ap, 'applicantServiceAddress'))
      req('4b', 'Address for service', S4B, 'applicantServiceAddressStreet');
    if (!ap.applicantServicePhone)
      req('4b', 'Phone', S4B, 'applicantServicePhone');
    if (!ap.applicantServiceEmail)
      req('4b', 'Email', S4B, 'applicantServiceEmail');
    if (!isYesNo(ap.applicantHasFax))
      req('4b', 'Has fax', S4B, 'applicantHasFax');
    if (ap.applicantHasFax === 'yes' && !ap.applicantServiceFax)
      req('4b', 'Fax', S4B, 'applicantServiceFax');
  }

  return missing;
}

/**
 * rev4a-creditorrep-row's own (creditor-only) derivation: the nominator kind
 * is not reset for grandchild/child here and `nominated`/`courtPath` are
 * creditor-local.
 */
function creditorRepRowShown(a: FormData): boolean {
  const cred = String(a.applicantCreditorType || '');
  if (cred !== 'organization') return false;
  const nom = String(a.applicantNominator || '');
  const kind =
    nom.indexOf('spouse::') === 0
      ? 'spouse'
      : nom.indexOf('child::') === 0
        ? 'child'
        : nom
          ? 'intestate'
          : '';
  const consentOk =
    kind === 'spouse' ||
    (kind === 'child' && a.applicantChildConsent === 'yes') ||
    (kind === 'intestate' && a.applicantIntestateConsent === 'yes');
  const nominated = a.applicantHasBeenNominated === 'yes';
  const courtPath = a.applicantHasBeenNominated === 'no';
  return (
    (nominated && consentOk) ||
    (courtPath && a.applicantCourtAppointed === 'yes')
  );
}
