import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { completeApplication } from './fixtures';
import {
  computeWillSectionState,
  missingReviewItems,
} from './reviewRequirements';

const missing = (data: Record<string, any>, citorVisible = false) =>
  missingReviewItems(data, { citorVisible }).map((i) => i.field);

describe('missingReviewItems', () => {
  it('is empty for a complete application', () => {
    expect(missing(completeApplication())).toEqual([]);
  });

  const REFERENCE_DIR = resolve(
    __dirname,
    '../../../../../chefs-agent/reference'
  );
  it.each(['probate form data sample.json', 'P9-probate data sample.json'])(
    'is empty for the reference sample %s',
    (file) => {
      const path = resolve(REFERENCE_DIR, file);
      // The reference samples live outside the jag-probate repo.
      if (!existsSync(path)) return;
      expect(missing(JSON.parse(readFileSync(path, 'utf8')))).toEqual([]);
    }
  );

  it('flags every unanswered row of an empty application', () => {
    expect(missing({})).toEqual([
      'firstNameGivenName',
      'deceasedDeathDate',
      'deceasedAddressStreet',
      'deceasedFirstNationYN',
      'HaveYouLookedForDeceasedWillWellYN',
      'applicantNameSelection',
      'applicantHasLawyer',
    ]);
  });

  it('asks for the First Nation only when the deceased was a member', () => {
    const data = { ...completeApplication(), deceasedFirstNationYN: 'yes' };
    expect(missing(data)).toEqual(['firstNationMembership']);
    expect(missing({ ...data, firstNationMembershipDisplayName: 'X' })).toEqual(
      []
    );
  });

  it('requires a named relative only when the relative exists', () => {
    const data = completeApplication();
    data.spouse.spouseData = [{ spouseName: '  ' }];
    expect(missing(data)).toEqual(['spouseExists']);
    data.spouse.spouseExists = 'no';
    expect(missing(data)).toEqual([]);
    data.creditor.creditorOrgExists = 'yes';
    expect(missing(data)).toEqual(['creditorOrgExists']);
  });

  it('asks for a mailing address only when mail goes elsewhere', () => {
    const data = completeApplication();
    data.applicant.applicantOrdinaryAddressReceiveMail = 'no';
    expect(missing(data)).toEqual(['applicantMailingAddressStreet']);
    data.applicant.applicantMailingAddressCity = 'Victoria';
    expect(missing(data)).toEqual([]);
  });

  it('drops the service rows when the applicant has a lawyer', () => {
    const data = completeApplication();
    data.applicant = { ...data.applicant, applicantHasFax: 'yes' };
    expect(missing(data)).toEqual(['applicantServiceFax']);
    data.applicant.applicantHasLawyer = 'yes';
    expect(missing(data)).toEqual([]);
  });

  it('rebuilds a missing calculated applicantName before deciding row visibility', () => {
    const data = completeApplication();
    delete data.applicant.applicantName;
    data.applicant.applicantOccupation = '';
    expect(missing(data)).toEqual(['applicantOccupation']);
  });

  it('requires citors only while the Citor substep is visible', () => {
    const data = completeApplication();
    data.applicant.applicantCited = 'yes';
    data.applicant.applicantCitorNewExists = 'yes';
    expect(missing(data, true)).toEqual(['applicantCitorNewExists']);
    expect(missing(data, false)).toEqual([]);
    data.applicant.citorData = [{ citorName: 'C' }];
    expect(missing(data, true)).toEqual([]);
  });

  it('follows the nominated-organization path', () => {
    const data = completeApplication();
    data.applicant = {
      ...data.applicant,
      applicantNameSelection: '__other__',
      applicantOtherName: 'Acme rep',
      applicantName: 'Acme rep',
      applicantHasBeenNominated: 'yes',
      applicantNominator: 'spouse::John Doe',
      applicantNomineeIsOrg: 'yes',
    };
    expect(missing(data)).toEqual(['applicantOrgName']);

    data.applicant.applicantOrgName = 'Acme';
    expect(missing(data)).toEqual(['applicantOrgAuthorized']);

    data.applicant.applicantOrgAuthorized = 'yes';
    expect(missing(data)).toEqual(['applicantOrgTitle']);

    data.applicant.applicantOrgTitle = 'CEO';
    expect(missing(data)).toEqual(['applicantOrgAddressStreet']);
  });

  it('asks the other-successor questions when not nominated', () => {
    const data = completeApplication();
    data.applicant = {
      ...data.applicant,
      applicantNameSelection: '__other__',
      applicantOtherName: 'Pat',
      applicantName: 'Pat',
      applicantHasBeenNominated: 'no',
    };
    expect(missing(data)).toEqual(['applicantIsOtherSuccessor']);
    data.applicant.applicantIsOtherSuccessor = 'yes';
    expect(missing(data)).toEqual(['applicantHasSamePrioritySuccessors']);
    data.applicant.applicantHasSamePrioritySuccessors = 'yes';
    expect(missing(data)).toEqual(['applicantSamePrioritySuccessorNames']);
    data.applicant.applicantSamePrioritySuccessorNames = [
      { applicantSuccessorEntry: 'Sam' },
    ];
    expect(missing(data)).toEqual(['applicantMajorityInInterestConsent']);
  });
});

describe('computeWillSectionState', () => {
  const base = {
    HaveYouLookedForDeceasedWillWellYN: 'yes',
    willExists: 'no',
    willsNoticeResultsReceived: 'yes',
    willsNoticeHasResults: 'yes',
    willsNoticeFoundIndicatedWills: 'yes',
    willsNoticeIndicatedWillsOtherDocExists: 'no',
    willWantToRecognizedYN: 'no',
  };
  const emptyRows = (d: Record<string, any>) =>
    computeWillSectionState(d)
      .rows.filter((r) => !r.value)
      .map((r) => r.field);

  it('stops at the document list until a document is listed', () => {
    expect(emptyRows(base)).toEqual([
      'willsNoticeIndicatedWillsOtherTestamentaryDocumentsEditor',
    ]);
  });

  it('continues to the grant questions once the list is finished', () => {
    const d = {
      ...base,
      willsNoticeIndicatedWillsOtherTestamentaryDocumentsEditor: [
        { type: 'Codicil', identifier: 'A' },
      ],
      willsNoticeIndicatedWillsOtherDocReasonsFinished: 'yes',
    };
    const state = computeWillSectionState(d);
    expect(
      state.rows.find(
        (r) =>
          r.field ===
          'willsNoticeIndicatedWillsOtherTestamentaryDocumentsEditor'
      )?.value
    ).toBe('1 document(s): Codicil (A)');
    expect(emptyRows(d)).toEqual(['willGrantExists']);
    expect(state.isValid).toBe(false);
  });

  it('requires the grant details once a grant exists and is confirmed', () => {
    const d = {
      HaveYouLookedForDeceasedWillWellYN: 'yes',
      willExists: 'no',
      willsNoticeResultsReceived: 'no',
      willGrantExists: 'yes',
      existedGrantConfirmLimitations: true,
    };
    expect(emptyRows(d)).toEqual([
      'existedGrantsIssuedTxt',
      'existedGrantNotRelevantReasonsTxt',
    ]);
  });
});
