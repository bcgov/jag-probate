import { describe, it, expect, beforeEach } from 'vitest';
import {
  RELATED_PEOPLE_TYPES,
  isYes,
  hasMinorConcept,
  classify,
  getRepresentative,
  describeRepresentative,
  forEachRelatedPerson,
  collectAllRelatedPeople,
  isApplicant,
  registerRelatedPeopleGlobal,
} from './relatedPeople';
import type { RelatedPeopleTypeConfig } from '@/types/relatedPeople';

function getConfig(key: string): RelatedPeopleTypeConfig {
  const config = RELATED_PEOPLE_TYPES.find((t) => t.key === key);
  if (!config) throw new Error(`no registry entry for ${key}`);
  return config;
}

describe('isYes', () => {
  it('treats "yes"/"y" (any case) as true', () => {
    expect(isYes('yes')).toBe(true);
    expect(isYes('YES')).toBe(true);
    expect(isYes('y')).toBe(true);
    expect(isYes('Y')).toBe(true);
  });

  it('treats everything else (including missing values) as false', () => {
    expect(isYes('no')).toBe(false);
    expect(isYes('')).toBe(false);
    expect(isYes(undefined)).toBe(false);
    expect(isYes(null)).toBe(false);
  });
});

describe('hasMinorConcept', () => {
  it('is true by default for types that never set the flag', () => {
    expect(hasMinorConcept(getConfig('spouse'))).toBe(true);
    expect(hasMinorConcept(getConfig('child'))).toBe(true);
    expect(hasMinorConcept(getConfig('grandchild'))).toBe(true);
  });

  it('is false for parent/sibling - they have no isAdult/guardian question', () => {
    expect(hasMinorConcept(getConfig('parent'))).toBe(false);
    expect(hasMinorConcept(getConfig('sibling'))).toBe(false);
  });
});

describe('classify', () => {
  const alive = { isAlive: true, isAdult: true, isCompetent: true };

  it('returns "org" for org-only types regardless of other input', () => {
    expect(classify(alive, getConfig('creditorOrg'))).toBe('org');
  });

  it('returns "deceased" when not alive', () => {
    expect(classify({ ...alive, isAlive: false }, getConfig('spouse'))).toBe(
      'deceased'
    );
  });

  it('returns "minor" for a non-adult on a type with the minor concept', () => {
    expect(classify({ ...alive, isAdult: false }, getConfig('child'))).toBe(
      'minor'
    );
  });

  it('never returns "minor" for parent/sibling - skips straight to competence', () => {
    // parent/sibling records never really have isAdult=false, but even if
    // normalizeRecord somehow produced one, classify must not treat it as minor.
    expect(classify({ ...alive, isAdult: false }, getConfig('parent'))).toBe(
      'adult-competent'
    );
    expect(
      classify(
        { ...alive, isAdult: false, isCompetent: false },
        getConfig('sibling')
      )
    ).toBe('incompetent-adult');
  });

  it('returns "incompetent-adult" for an adult who is not competent', () => {
    expect(classify({ ...alive, isCompetent: false }, getConfig('child'))).toBe(
      'incompetent-adult'
    );
  });

  it('returns "adult-competent" otherwise', () => {
    expect(classify(alive, getConfig('child'))).toBe('adult-competent');
  });
});

describe('getRepresentative', () => {
  const noReps = {
    hasGuardian: false,
    guardianName: '',
    hasNominee: false,
    nomineeName: '',
    nomineeFormal: false,
    hasPersonalRep: false,
    personalRepName: '',
  };

  it('returns a guardian for minors who have one, else null', () => {
    expect(
      getRepresentative(
        { ...noReps, hasGuardian: true, guardianName: 'Gary Guardian' },
        'minor'
      )
    ).toEqual({ role: 'guardian', name: 'Gary Guardian' });
    expect(getRepresentative(noReps, 'minor')).toBeNull();
  });

  it('returns a nominee (with formal flag) for incompetent adults who have one, else null', () => {
    expect(
      getRepresentative(
        {
          ...noReps,
          hasNominee: true,
          nomineeName: 'Nina Nominee',
          nomineeFormal: true,
        },
        'incompetent-adult'
      )
    ).toEqual({ role: 'nominee', name: 'Nina Nominee', formal: true });
    expect(getRepresentative(noReps, 'incompetent-adult')).toBeNull();
  });

  it('returns a personal rep for deceased people who have one, else null', () => {
    expect(
      getRepresentative(
        { ...noReps, hasPersonalRep: true, personalRepName: 'Pat Rep' },
        'deceased'
      )
    ).toEqual({ role: 'personalRep', name: 'Pat Rep' });
    expect(getRepresentative(noReps, 'deceased')).toBeNull();
  });

  it('returns null for adult-competent and org statuses', () => {
    expect(getRepresentative(noReps, 'adult-competent')).toBeNull();
    expect(getRepresentative(noReps, 'org')).toBeNull();
  });
});

describe('describeRepresentative', () => {
  it('formats as "{name} ({Role} of {principal})"', () => {
    expect(
      describeRepresentative(
        { role: 'guardian', name: 'Gary Guardian' },
        'Timmy Tot'
      )
    ).toBe('Gary Guardian (Guardian of Timmy Tot)');
    expect(
      describeRepresentative(
        { role: 'personalRep', name: 'Pat Rep' },
        'Sam Spouse'
      )
    ).toBe('Pat Rep (Personal Representative of Sam Spouse)');
  });
});

describe('forEachRelatedPerson / collectAllRelatedPeople', () => {
  it('collects every flat type from its registered data path', () => {
    const data = {
      spouse: {
        spouseData: [
          {
            spouseName: 'Sam Spouse',
            spouseIsAlive: 'yes',
            spouseIsAdult: 'yes',
            spouseIsCompetent: 'yes',
          },
        ],
      },
      child: {
        childData: [
          {
            childName: 'Charlie Child',
            childIsAlive: 'yes',
            childIsAdult: 'yes',
            childIsCompetent: 'yes',
          },
        ],
      },
      parent: {
        parentData: [
          {
            parentName: 'Pam Parent',
            parentIsAlive: 'yes',
            parentIsCompetent: 'yes',
          },
        ],
      },
      sibling: {
        siblingData: [
          {
            siblingName: 'Sid Sibling',
            siblingIsAlive: 'yes',
            siblingIsCompetent: 'yes',
          },
        ],
      },
      creditor: {
        creditorPersonData: [
          {
            creditorPersonName: 'Cora Creditor',
            creditorPersonIsAlive: 'yes',
            creditorPersonIsAdult: 'yes',
            creditorPersonIsCompetent: 'yes',
          },
        ],
        creditorOrgData: [{ creditorOrgName: 'Acme Lending Co' }],
      },
      applicant: {
        citorData: [
          {
            citorName: 'Cindy Citor',
            citorIsAlive: 'yes',
            citorIsAdult: 'yes',
            citorIsCompetent: 'yes',
          },
        ],
      },
    };

    const people = collectAllRelatedPeople(data);
    const byType = Object.fromEntries(people.map((p) => [p.type, p.name]));

    expect(byType).toEqual({
      spouse: 'Sam Spouse',
      child: 'Charlie Child',
      parent: 'Pam Parent',
      sibling: 'Sid Sibling',
      creditorPerson: 'Cora Creditor',
      creditorOrg: 'Acme Lending Co',
      citor: 'Cindy Citor',
    });
    expect(people.find((p) => p.type === 'creditorOrg')?.status).toBe('org');
  });

  it('skips records with a missing/blank name', () => {
    const data = {
      spouse: {
        spouseData: [
          { spouseName: '' },
          { spouseIsAlive: 'yes' }, // no spouseName key at all
          {
            spouseName: 'Real Spouse',
            spouseIsAlive: 'yes',
            spouseIsAdult: 'yes',
            spouseIsCompetent: 'yes',
          },
        ],
      },
    };

    const people = collectAllRelatedPeople(data);
    expect(people).toHaveLength(1);
    expect(people[0].name).toBe('Real Spouse');
  });

  it('ignores a type entirely when its container/array is missing', () => {
    expect(collectAllRelatedPeople({})).toEqual([]);
    expect(collectAllRelatedPeople({ spouse: {} })).toEqual([]);
  });

  it('visits nested grandchildren under each child row, linking them to the normalized parent record', () => {
    const data = {
      child: {
        childData: [
          {
            childName: 'Deceased Child',
            childIsAlive: 'no',
            childDied5DaysAfter: 'no',
            gchildData: [
              {
                grandchildName: 'Gracie Grandchild',
                grandchildIsAlive: 'yes',
                grandchildIsAdult: 'yes',
                grandchildIsCompetent: 'yes',
              },
            ],
          },
        ],
      },
    };

    const people = collectAllRelatedPeople(data);
    expect(people.map((p) => p.name)).toEqual([
      'Deceased Child',
      'Gracie Grandchild',
    ]);

    const grandchild = people.find((p) => p.type === 'grandchild')!;
    expect(grandchild.parent).toMatchObject({
      type: 'child',
      name: 'Deceased Child',
    });
    expect(grandchild.parent?.record.status).toBe('deceased');
    expect(grandchild.parent?.record.died5DaysAfter).toBe(false);
  });

  it('collects grandchildren from multiple children independently', () => {
    const data = {
      child: {
        childData: [
          {
            childName: 'Child One',
            childIsAlive: 'no',
            gchildData: [{ grandchildName: 'Grandchild One A' }],
          },
          {
            childName: 'Child Two',
            childIsAlive: 'yes',
            gchildData: [{ grandchildName: 'Grandchild Two A' }],
          },
        ],
      },
    };

    const people = collectAllRelatedPeople(data);
    const grandchildren = people.filter((p) => p.type === 'grandchild');
    expect(grandchildren.map((g) => g.parent?.name)).toEqual([
      'Child One',
      'Child Two',
    ]);
  });

  it('calls back with the matching registry config alongside each person', () => {
    const data = {
      spouse: {
        spouseData: [
          {
            spouseName: 'Sam Spouse',
            spouseIsAlive: 'yes',
            spouseIsAdult: 'yes',
            spouseIsCompetent: 'yes',
          },
        ],
      },
    };

    const seen: string[] = [];
    forEachRelatedPerson(data, (person, config) => {
      seen.push(`${config.key}:${person.name}`);
    });

    expect(seen).toEqual(['spouse:Sam Spouse']);
  });

  it('normalizes parent/sibling without an isAdult question as adult-competent', () => {
    const data = {
      parent: {
        parentData: [
          {
            parentName: 'Pam Parent',
            parentIsAlive: 'yes',
            parentIsCompetent: 'yes',
          },
        ],
      },
      sibling: {
        siblingData: [
          {
            siblingName: 'Sid Sibling',
            siblingIsAlive: 'yes',
            siblingIsCompetent: 'no',
            siblingHasNominee: 'yes',
            siblingNomineeName: 'Nina Nominee',
          },
        ],
      },
    };

    const people = collectAllRelatedPeople(data);
    const parent = people.find((p) => p.type === 'parent')!;
    const sibling = people.find((p) => p.type === 'sibling')!;

    expect(parent.status).toBe('adult-competent');
    expect(parent.isAdult).toBe(true);

    expect(sibling.status).toBe('incompetent-adult');
    expect(sibling.representative).toEqual({
      role: 'nominee',
      name: 'Nina Nominee',
      formal: false,
    });
  });
});

describe('isApplicant', () => {
  it('matches case-insensitively and ignores surrounding whitespace', () => {
    const data = { applicant: { applicantName: '  Sam Spouse  ' } };
    expect(isApplicant(data, 'sam spouse')).toBe(true);
    expect(isApplicant(data, 'SAM SPOUSE')).toBe(true);
    expect(isApplicant(data, 'Someone Else')).toBe(false);
  });

  it('returns false when there is no applicant name or no name to check', () => {
    expect(isApplicant({}, 'Sam Spouse')).toBe(false);
    expect(
      isApplicant({ applicant: { applicantName: 'Sam Spouse' } }, '')
    ).toBe(false);
  });
});

describe('registerRelatedPeopleGlobal', () => {
  beforeEach(() => {
    delete (window as { RelatedPeople?: unknown }).RelatedPeople;
  });

  it('attaches a fully-populated API to window.RelatedPeople', () => {
    registerRelatedPeopleGlobal();

    expect(window.RelatedPeople?.TYPES).toBe(RELATED_PEOPLE_TYPES);
    expect(window.RelatedPeople?.forEach).toBe(forEachRelatedPerson);
    expect(window.RelatedPeople?.collectAll).toBe(collectAllRelatedPeople);
    expect(typeof window.RelatedPeople?.isYes).toBe('function');
    expect(typeof window.RelatedPeople?.classify).toBe('function');
    expect(typeof window.RelatedPeople?.hasMinorConcept).toBe('function');
    expect(typeof window.RelatedPeople?.getRepresentative).toBe('function');
    expect(typeof window.RelatedPeople?.describeRepresentative).toBe(
      'function'
    );
    expect(typeof window.RelatedPeople?.isApplicant).toBe('function');
  });

  it('is usable end-to-end once registered (what CHEFS schema scripts actually call)', () => {
    registerRelatedPeopleGlobal();

    const data = {
      spouse: {
        spouseData: [
          {
            spouseName: 'Sam Spouse',
            spouseIsAlive: 'yes',
            spouseIsAdult: 'yes',
            spouseIsCompetent: 'yes',
          },
        ],
      },
    };

    const names: string[] = [];
    window.RelatedPeople?.forEach(data, (person) => names.push(person.name));

    expect(names).toEqual(['Sam Spouse']);
  });
});
