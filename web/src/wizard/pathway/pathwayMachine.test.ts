import { describe, expect, it } from 'vitest';
import { deriveFacts } from './facts';
import { completeApplication, STEPS } from './fixtures';
import { evaluatePathway } from './pathwayMachine';

const evaluate = (data: Record<string, any>) =>
  evaluatePathway(STEPS, deriveFacts(data));

const visibleSteps = (data: Record<string, any>) => {
  const r = evaluate(data);
  return STEPS.map((s) => s.key).filter((k) => !r.hiddenSteps[k]);
};

const ALL_STEPS = STEPS.map((s) => s.key);
const UP_TO_STEP2 = ['form-deceased', 'form-testamentary-docs'];
const UP_TO_STEP5 = [
  ...UP_TO_STEP2,
  'form-related-people',
  'form-applicant',
  'form-notify',
];

describe('evaluatePathway', () => {
  it('opens every step for a complete application', () => {
    const r = evaluate(completeApplication());
    expect(r.state).toBe('assetsAnswered');
    expect(visibleSteps(completeApplication())).toEqual(ALL_STEPS);
    expect(r.disabledMap).toEqual({});
    expect(r.hiddenSubsteps['form-preview-p5']).toBeUndefined();
  });

  it('starts with only Step 1 when nothing is answered', () => {
    expect(visibleSteps({})).toEqual(['form-deceased']);
    expect(evaluate({}).state).toBe('deceased');
  });

  // Regression: revisiting Step 2 used to re-close Step 7.
  it('keeps Step 7 open when Step 2 is re-evaluated with unchanged answers', () => {
    const data = completeApplication();
    const first = evaluate(data);
    const again = evaluate({ ...data });
    expect(first.hiddenSteps['form-application-documents']).toBeUndefined();
    expect(again).toEqual(first);
  });

  it('closes Steps 3–8 on a banned Step 2 answer and reopens them when it is undone', () => {
    const data = completeApplication();
    expect(visibleSteps({ ...data, willExists: 'yes' })).toEqual(UP_TO_STEP2);
    expect(visibleSteps({ ...data, willExists: 'no' })).toEqual(ALL_STEPS);
  });

  it('reopens Step 7 only when the Step 5 review and Step 6 gates still hold', () => {
    const data = completeApplication();
    data.applicant.applicantOccupation = '';
    // Step 2 open again, but the review now has a missing item.
    expect(visibleSteps(data)).toEqual(UP_TO_STEP5);

    const unanswered = { ...completeApplication(), q42CanIdentifyAssets: '' };
    expect(visibleSteps(unanswered)).toEqual([
      ...UP_TO_STEP5,
      'form-assets',
      'form-filing',
    ]);
  });

  it('keeps Filing hidden until the review is valid (D1)', () => {
    const data = completeApplication();
    delete data.deceasedDeathDate;
    const r = evaluate(data);
    expect(r.state).toBe('willPathOpen');
    expect(r.hiddenSteps['form-filing']).toBe(true);
    expect(r.hiddenSubsteps['form-filing-instructions']).toBe(true);
  });

  it('lets only Step 6 open Step 7, regardless of Notify People (D2)', () => {
    const withIncompleteNotify = completeApplication();
    withIncompleteNotify.notifyPeople.notifyPeopleData = [
      { recipientName: 'Someone' },
    ];
    const r = evaluate(withIncompleteNotify);
    expect(r.hiddenSteps['form-application-documents']).toBeUndefined();
    expect(r.hiddenSubsteps['form-preview-p9']).toBeUndefined();

    const notAnswered = { ...completeApplication(), q42CanIdentifyAssets: '' };
    expect(
      evaluate(notAnswered).hiddenSteps['form-application-documents']
    ).toBe(true);
  });

  describe('bans', () => {
    it('applies the earliest active ban point; clearing it exposes the next', () => {
      const data = completeApplication();
      data.spouse.spouseCompleted2 = 'no';
      data.applicant.applicantHasLawyer = 'yes';

      const r = evaluate(data);
      expect(r.activeBans).toEqual(['form-spouse', 'form-applicant-service']);
      expect(r.disabledMap['form-children']).toBe(true);
      expect(r.disabledMap['form-applicant']).toBe(true);
      expect(r.disabledMap['form-related-people']).toBeUndefined();

      data.spouse.spouseCompleted2 = 'yes';
      const next = evaluate(data);
      expect(next.activeBans).toEqual(['form-applicant-service']);
      expect(next.disabledMap['form-applicant']).toBeUndefined();
      expect(next.disabledMap['form-notify']).toBe(true);
      expect(next.disabledMap['form-full-review']).toBe(true);
    });

    // Step 3's "lift" used to wipe Step 4's ban on every recalculation.
    it("keeps a Step 4 ban while Step 3's answers are valid", () => {
      const data = completeApplication();
      data.applicant.applicantHasLawyer = 'yes';
      const r = evaluate(data);
      expect(r.disabledMap['form-notify']).toBe(true);
      expect(r.disabledMap['form-spouse']).toBeUndefined();
    });

    it("keeps the Notify People ban independent of Step 4's answers", () => {
      const data = completeApplication();
      data.deceasedFirstNationYN = 'yes';
      data.firstNationMembershipDisplayName = 'Nisga’a';
      data.notifyPeople.notifyPeopleData = [{ recipientName: 'Nation' }];
      const r = evaluate(data);
      expect(r.activeBans).toEqual(['form-notify-people']);
      expect(r.disabledMap['form-assets']).toBe(true);
      expect(r.disabledMap['form-filing']).toBe(true);
      expect(r.disabledMap['form-notify']).toBeUndefined();
    });

    it('ignores a stale ban answer on a substep that is now hidden', () => {
      const data = completeApplication();
      data.parent = { parentExists: 'no', parentCompleted2: 'no' };
      // Spouse exists → Parent substep hidden → its answer no longer bans.
      expect(evaluate(data).activeBans).toEqual([]);

      data.spouse = { spouseExists: 'no', spouseCompleted2: 'yes' };
      expect(evaluate(data).activeBans).toEqual(['form-parent']);
    });

    it('bans after P5 when the wills search results were not received', () => {
      const data = completeApplication();
      data.willsNoticeResultsReceived = 'no';
      const r = evaluate(data);
      expect(r.activeBans).toEqual(['form-preview-p5']);
      expect(r.disabledMap['form-preview-p2']).toBe(true);
      expect(r.disabledMap['form-filing']).toBe(true);
      expect(r.hiddenSubsteps['form-preview-p2']).toBe(true);
    });
  });

  describe('substeps', () => {
    it('reveals Parent, then Sibling, as earlier relatives are ruled out', () => {
      const data = completeApplication();
      data.spouse = { spouseExists: 'no' };
      data.child = { childExists: 'no' };
      let r = evaluate(data);
      expect(r.hiddenSubsteps['form-parent']).toBeUndefined();
      expect(r.hiddenSubsteps['form-sibling']).toBe(true);

      data.parent = { parentExists: 'no' };
      r = evaluate(data);
      expect(r.hiddenSubsteps['form-sibling']).toBeUndefined();
    });

    it('shows Citor only when cited with a new citor', () => {
      const data = completeApplication();
      data.applicant.applicantCited = 'yes';
      data.applicant.applicantCitorNewExists = 'yes';
      expect(evaluate(data).hiddenSubsteps['form-citor']).toBeUndefined();
      data.applicant.applicantCitorNewExists = 'no';
      expect(evaluate(data).hiddenSubsteps['form-citor']).toBe(true);
    });

    it('hides outside-BC asset substeps for a non-BC address', () => {
      const data = completeApplication();
      data.deceasedAddressCountry = 'United States';
      const r = evaluate(data);
      expect(r.hiddenSubsteps['form-assets-real-property-outside-bc']).toBe(
        true
      );
      expect(
        r.hiddenSubsteps['form-assets-tangible-personal-property-outside-bc']
      ).toBe(true);
    });

    it('picks the Step 7 previews from the asset and address answers', () => {
      const bc = evaluate(completeApplication());
      expect(bc.hiddenSubsteps['form-preview-p10']).toBeUndefined();
      expect(bc.hiddenSubsteps['form-preview-p11']).toBe(true);
      expect(bc.hiddenSubsteps['form-preview-p18']).toBe(true);
      expect(bc.hiddenSubsteps['form-preview-p9']).toBe(true);

      const unconfirmed = evaluate({
        ...completeApplication(),
        q424AssetsConfirmed: 'no',
      });
      expect(unconfirmed.hiddenSubsteps['form-preview-p18']).toBeUndefined();
      expect(unconfirmed.hiddenSubsteps['form-preview-p10']).toBe(true);
    });

    it('shows the notify substeps only for an audience', () => {
      const none = evaluate(completeApplication());
      expect(none.hiddenSubsteps['form-tell-people']).toBeUndefined();
      expect(none.hiddenSubsteps['form-preview-p1']).toBe(true);
      expect(none.hiddenSubsteps['form-notify-people']).toBe(true);

      const data = completeApplication();
      data.child = {
        childExists: 'yes',
        childData: [
          { childName: 'Kid', childIsAlive: 'yes', childIsAdult: 'no' },
        ],
      };
      const r = evaluate(data);
      expect(r.hiddenSubsteps['form-preview-p1']).toBeUndefined();
      expect(r.hiddenSubsteps['form-preview-pgt']).toBeUndefined();
      expect(r.hiddenSubsteps['form-notify-people']).toBeUndefined();
    });
  });

  it('ignores rules for keys missing from the step config', () => {
    const steps = STEPS.filter((s) => s.key !== 'form-filing');
    const r = evaluatePathway(steps, deriveFacts(completeApplication()));
    expect(r.hiddenSteps['form-filing']).toBeUndefined();
    expect('form-filing-instructions' in r.hiddenSubsteps).toBe(false);
  });
});
