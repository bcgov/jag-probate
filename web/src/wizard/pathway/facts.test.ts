import { describe, expect, it } from 'vitest';
import { deriveApplicantFacts } from './applicantFacts';
import {
  deriveFacts,
  deriveHideOutsideBcAssets,
  deriveNotifyAudience,
  deriveWillPathOpen,
  isNotifyRowComplete,
} from './facts';

const OPEN = {
  HaveYouLookedForDeceasedWillWellYN: 'yes',
  willExists: 'no',
};
const FOUND_WILL = {
  ...OPEN,
  willsNoticeResultsReceived: 'yes',
  willsNoticeHasResults: 'yes',
  willsNoticeFoundIndicatedWills: 'yes',
};

describe('deriveWillPathOpen (guidedPathController)', () => {
  it.each([
    ['nothing answered', {}, false],
    ['looked, no will', OPEN, true],
    [
      'did not look',
      { ...OPEN, HaveYouLookedForDeceasedWillWellYN: 'no' },
      false,
    ],
    ['made a will', { ...OPEN, willExists: 'yes' }, false],
    [
      'found document to recognize',
      {
        ...OPEN,
        willsNoticeResultsReceived: 'yes',
        willsNoticeHasResults: 'no',
        willsNoticeFoundWillOrOtherTestDocs: 'yes',
        willsNoticeRecognizeFoundWillOrOtherTestDocs: 'yes',
      },
      false,
    ],
    [
      'indicated will to recognize',
      {
        ...FOUND_WILL,
        willsNoticeIndicatedWillsOtherDocExists: 'no',
        willWantToRecognizedYN: 'yes',
      },
      false,
    ],
    [
      'indicated will, documents list not finished',
      {
        ...FOUND_WILL,
        willsNoticeIndicatedWillsOtherDocExists: 'no',
        willWantToRecognizedYN: 'no',
        willsNoticeIndicatedWillsOtherDocReasonsFinished: 'no',
      },
      false,
    ],
    [
      'missing indicated will, other document to recognize',
      {
        ...OPEN,
        willsNoticeResultsReceived: 'yes',
        willsNoticeHasResults: 'yes',
        willsNoticeFoundIndicatedWills: 'no',
        willsNoticeDidNotFindIndicatedWillsOtherDocExists: 'yes',
        willsNoticeDidNotFindIndicatedWillsOtherDocRecognize: 'yes',
      },
      false,
    ],
    [
      'grant exists, limitations unconfirmed',
      {
        ...OPEN,
        willGrantExists: 'yes',
        existedGrantConfirmLimitations: false,
      },
      false,
    ],
    // Only an explicit `false` blocks.
    [
      'grant exists, confirmation pending',
      { ...OPEN, willGrantExists: 'yes' },
      true,
    ],
    [
      'grant exists, confirmed',
      { ...OPEN, willGrantExists: 'yes', existedGrantConfirmLimitations: true },
      true,
    ],
  ])('%s → %s', (_label, data, expected) => {
    expect(deriveWillPathOpen(data)).toBe(expected);
  });
});

describe('deriveHideOutsideBcAssets (assetLocationVisibilityController)', () => {
  it.each([
    [{}, false],
    [
      {
        deceasedAddressCountry: ' Canada ',
        deceasedAddressProvince: 'british columbia',
      },
      false,
    ],
    [
      { deceasedAddressCountry: 'Canada', deceasedAddressProvince: 'Alberta' },
      true,
    ],
    [{ deceasedAddressCountry: 'USA' }, true],
    [{ deceasedAddressProvince: 'British Columbia' }, false],
  ])('%j → %s', (data, expected) => {
    expect(deriveHideOutsideBcAssets(data)).toBe(expected);
  });
});

describe('deriveApplicantFacts (applicantPathwayBanWatcher)', () => {
  it('stops at the service substep when the applicant has a lawyer', () => {
    const f = deriveApplicantFacts({ applicantHasLawyer: 'yes' });
    expect(f.lawyerStop).toBe(true);
    expect(f.infoStop || f.courtStop).toBe(false);
  });

  it('stops when a nominating child does not consent', () => {
    const f = deriveApplicantFacts({
      applicantNameSelection: '__other__',
      applicantOtherName: 'Pat',
      applicantHasBeenNominated: 'yes',
      applicantNominator: 'child::Kid',
      applicantChildConsent: 'no',
    });
    expect(f.infoStop).toBe(true);
  });

  it('ignores a nominator kind the grandchild path does not allow', () => {
    const f = deriveApplicantFacts({
      applicantIsGrandchild: true,
      applicantGrandchildHasSpouse: true,
      applicantHasBeenNominated: 'yes',
      applicantNominator: 'child::Kid',
      applicantChildConsent: 'no',
    });
    expect(f.kind).toBe('');
    expect(f.infoStop).toBe(false);
  });

  it('stops on the court path when not court appointed', () => {
    const base = {
      applicantNameSelection: '__other__',
      applicantOtherName: 'Pat',
      applicantHasBeenNominated: 'no',
      applicantIsOtherSuccessor: 'no',
    };
    expect(
      deriveApplicantFacts({ ...base, applicantCourtAppointed: 'no' }).courtStop
    ).toBe(true);
    expect(
      deriveApplicantFacts({ ...base, applicantCourtAppointed: 'yes' })
        .courtStop
    ).toBe(false);
  });

  it('stops a listed-people successor without a court appointment', () => {
    const f = deriveApplicantFacts({
      applicantNameSelection: '__other__',
      applicantHasBeenNominated: 'no',
      applicantIsOtherSuccessor: 'yes',
      applicantStep3PeopleExist: true,
      applicantCourtAppointed: 'no',
    });
    expect(f.successorCourtStop).toBe(true);
    expect(f.courtStop).toBe(true);
  });

  it('rebuilds a missing calculated applicantName from the selection', () => {
    expect(
      deriveApplicantFacts({ applicantNameSelection: ' Jo ' }).applicantName
    ).toBe('Jo');
    expect(
      deriveApplicantFacts({
        applicantNameSelection: '__other__',
        applicantOtherName: 'Pat',
      }).applicantName
    ).toBe('Pat');
  });
});

describe('isNotifyRowComplete (notifyPeopleValidationWatcher)', () => {
  const row = {
    recipientName: 'A',
    p1Delivered: 'yes',
    delivererName: 'B',
    deliveryDate: '2024-01-01',
    deliveryMethod: 'mail',
  };
  it('requires every delivery detail', () => {
    expect(isNotifyRowComplete(row)).toBe(true);
    expect(isNotifyRowComplete({ ...row, p1Delivered: 'no' })).toBe(false);
    expect(isNotifyRowComplete({ ...row, deliveryDate: '' })).toBe(false);
  });
  it('requires receipt and retention for electronic delivery', () => {
    const e = {
      ...row,
      deliveryMethod: 'electronic',
      electronicReceipt: 'yes',
    };
    expect(isNotifyRowComplete(e)).toBe(false);
    expect(isNotifyRowComplete({ ...e, electronicRetain: true })).toBe(true);
  });
});

describe('deriveNotifyAudience (step5ReviewWatcher)', () => {
  const child = (overrides: Record<string, unknown>) => ({
    childName: 'Kid',
    childIsAlive: 'no',
    gchildData: [
      {
        grandchildName: 'Grandkid',
        grandchildIsAlive: 'yes',
        grandchildIsAdult: 'no',
      },
    ],
    ...overrides,
  });

  it('counts a grandchild only when the parent child died before the cutoff', () => {
    const bypassed = deriveNotifyAudience({
      child: { childData: [child({})] },
    });
    expect(bypassed).toEqual({ hasMinorOrIncapable: true, peopleCount: 2 });

    const survived = deriveNotifyAudience({
      child: { childData: [child({ childDied5DaysAfter: 'yes' })] },
    });
    expect(survived).toEqual({ hasMinorOrIncapable: false, peopleCount: 1 });
  });

  it('does not count the applicant', () => {
    const data = {
      spouse: {
        spouseData: [
          {
            spouseName: 'John',
            spouseIsAlive: 'yes',
            spouseIsCompetent: 'yes',
            spouseIsAdult: 'yes',
          },
        ],
      },
      applicant: { applicantName: 'john ' },
    };
    expect(deriveNotifyAudience(data).peopleCount).toBe(0);
  });
});

describe('deriveFacts', () => {
  it('treats a Step 2 answer as proof Step 1 was continued (older drafts)', () => {
    expect(deriveFacts({}).step1Continued).toBe(false);
    expect(deriveFacts({ showStep1Items: true }).step1Continued).toBe(true);
    expect(
      deriveFacts({ HaveYouLookedForDeceasedWillWellYN: 'no' }).step1Continued
    ).toBe(true);
  });

  it('derives Parent/Sibling visibility from the earlier relatives', () => {
    const none = {
      spouse: { spouseExists: 'no' },
      child: { childExists: 'no' },
    };
    expect(deriveFacts(none).parentVisible).toBe(true);
    expect(deriveFacts(none).siblingVisible).toBe(false);
    expect(
      deriveFacts({ ...none, parent: { parentExists: 'no' } }).siblingVisible
    ).toBe(true);
    expect(deriveFacts({ spouse: { spouseExists: 'no' } }).parentVisible).toBe(
      false
    );
  });
});
