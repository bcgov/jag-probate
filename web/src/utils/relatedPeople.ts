import type {
  NormalizedPerson,
  RelatedPeopleApi,
  RelatedPeopleTypeConfig,
  RelatedPersonParentRef,
  RelatedPersonStatus,
  Representative,
  RepresentativeRole,
} from '@/types/relatedPeople';
import { isFlatRelatedPeopleType } from '@/types/relatedPeople';

// ── Registry ────────────────────────────────────────────────────────────────────
// Add a new relative type here only — no traversal code anywhere needs to change.
// `grandchild` demonstrates the nested shape: its array lives at
// `child.childData[i].gchildData`, not at a fixed path from the form root.
export const RELATED_PEOPLE_TYPES: RelatedPeopleTypeConfig[] = [
  { key: 'spouse', prefix: 'spouse', dataPath: ['spouse', 'spouseData'] },
  { key: 'child', prefix: 'child', dataPath: ['child', 'childData'] },
  {
    key: 'parent',
    prefix: 'parent',
    dataPath: ['parent', 'parentData'],
    hasMinorConcept: false,
  },
  {
    key: 'sibling',
    prefix: 'sibling',
    dataPath: ['sibling', 'siblingData'],
    hasMinorConcept: false,
  },
  {
    key: 'creditorPerson',
    prefix: 'creditorPerson',
    dataPath: ['creditor', 'creditorPersonData'],
  },
  {
    key: 'creditorOrg',
    prefix: 'creditorOrg',
    dataPath: ['creditor', 'creditorOrgData'],
    orgOnly: true,
  },
  { key: 'citor', prefix: 'citor', dataPath: ['applicant', 'citorData'] },
  {
    key: 'grandchild',
    prefix: 'grandchild',
    parentKey: 'child',
    nestedArrayKey: 'gchildData',
  },
];

const ROLE_LABELS: Record<RepresentativeRole, string> = {
  guardian: 'Guardian',
  nominee: 'Nominee',
  personalRep: 'Personal Representative',
};

export function isYes(value: unknown): boolean {
  const s = String(value ?? '').toLowerCase();
  return s === 'y' || s === 'yes';
}

/** True unless the type config explicitly opts out (parent, sibling - no isAdult/guardian question exists for them). */
export function hasMinorConcept(config: RelatedPeopleTypeConfig): boolean {
  return config.hasMinorConcept !== false;
}

export function classify(
  input: { isAlive: boolean; isAdult: boolean; isCompetent: boolean },
  config: RelatedPeopleTypeConfig
): RelatedPersonStatus {
  if (config.orgOnly) return 'org';
  if (!input.isAlive) return 'deceased';
  if (hasMinorConcept(config) && !input.isAdult) return 'minor';
  if (!input.isCompetent) return 'incompetent-adult';
  return 'adult-competent';
}

/**
 * Resolves WHICH representative applies for a status (guardian for minors,
 * nominee for incompetent adults, personal rep for deceased) — returns null if
 * the record has none. Does NOT decide self/PGT-fallback inclusion rules:
 * those differ per consumer (notify vs applicant vs info-box) by design.
 */
export function getRepresentative(
  record: Pick<
    NormalizedPerson,
    | 'hasGuardian'
    | 'guardianName'
    | 'hasNominee'
    | 'nomineeName'
    | 'nomineeFormal'
    | 'hasPersonalRep'
    | 'personalRepName'
  >,
  status: RelatedPersonStatus
): Representative | null {
  if (status === 'minor') {
    return record.hasGuardian
      ? { role: 'guardian', name: record.guardianName }
      : null;
  }
  if (status === 'incompetent-adult') {
    return record.hasNominee
      ? {
          role: 'nominee',
          name: record.nomineeName,
          formal: record.nomineeFormal,
        }
      : null;
  }
  if (status === 'deceased') {
    return record.hasPersonalRep
      ? { role: 'personalRep', name: record.personalRepName }
      : null;
  }
  return null;
}

/** "{name} ({Role} of {principalName})" — matches existing applicant-text format. */
export function describeRepresentative(
  representative: Representative,
  principalName: string
): string {
  return `${representative.name} (${ROLE_LABELS[representative.role]} of ${principalName})`;
}

function normalizeRecord(
  raw: Record<string, unknown>,
  config: RelatedPeopleTypeConfig,
  parent: RelatedPersonParentRef | null
): NormalizedPerson {
  const p = config.prefix;
  const field = (suffix: string) => raw?.[`${p}${suffix}`];
  const name = String(field('Name') ?? '');

  const isAlive = config.orgOnly ? true : isYes(field('IsAlive'));
  const isAdult =
    config.orgOnly || !hasMinorConcept(config) ? true : isYes(field('IsAdult'));
  const isCompetent = config.orgOnly ? true : isYes(field('IsCompetent'));
  const hasGuardian = isYes(field('HasGuardian'));
  const hasNominee = isYes(field('HasNominee'));
  const hasPersonalRep = isYes(field('HasPersonalRep'));

  const record = {
    hasGuardian,
    guardianName: String(field('GuardianName') ?? ''),
    hasNominee,
    nomineeName: String(field('NomineeName') ?? ''),
    nomineeFormal: isYes(field('NomineeFormal')),
    hasPersonalRep,
    personalRepName: String(field('PersonalRepName') ?? ''),
  };

  const status = classify({ isAlive, isAdult, isCompetent }, config);
  const representative = getRepresentative(record, status);

  return {
    type: config.key,
    name,
    isAlive,
    isAdult,
    isCompetent,
    died5DaysAfter: isYes(field('Died5DaysAfter')),
    status,
    representative,
    parent,
    raw,
    ...record,
  };
}

function getNestedConfigsByParent(): Map<string, RelatedPeopleTypeConfig[]> {
  const map = new Map<string, RelatedPeopleTypeConfig[]>();
  for (const config of RELATED_PEOPLE_TYPES) {
    if (!config.parentKey) continue;
    const list = map.get(config.parentKey) ?? [];
    list.push(config);
    map.set(config.parentKey, list);
  }
  return map;
}

export function forEachRelatedPerson(
  data: Record<string, unknown>,
  callback: (person: NormalizedPerson, config: RelatedPeopleTypeConfig) => void
): void {
  const nestedConfigsByParent = getNestedConfigsByParent();

  function visit(
    rawRecords: unknown,
    config: RelatedPeopleTypeConfig,
    parent: RelatedPersonParentRef | null
  ) {
    if (!Array.isArray(rawRecords)) return;

    for (const raw of rawRecords as Record<string, unknown>[]) {
      if (!raw || !raw[`${config.prefix}Name`]) continue;

      const person = normalizeRecord(raw, config, parent);
      callback(person, config);

      for (const nestedConfig of nestedConfigsByParent.get(config.key) ?? []) {
        if (isFlatRelatedPeopleType(nestedConfig)) continue; // nested configs always have nestedArrayKey
        visit(raw[nestedConfig.nestedArrayKey], nestedConfig, {
          type: config.key,
          name: person.name,
          record: person,
        });
      }
    }
  }

  for (const config of RELATED_PEOPLE_TYPES) {
    if (!isFlatRelatedPeopleType(config)) continue; // nested types are visited via their parent
    const containerKey = config.dataPath[0];
    const arrayKey = config.dataPath[1];
    const container = data?.[containerKey] as
      | Record<string, unknown>
      | undefined;
    visit(container?.[arrayKey], config, null);
  }
}

export function collectAllRelatedPeople(
  data: Record<string, unknown>
): NormalizedPerson[] {
  const out: NormalizedPerson[] = [];
  forEachRelatedPerson(data, (person) => out.push(person));
  return out;
}

export function isApplicant(
  data: Record<string, unknown>,
  name: string
): boolean {
  const applicantName = (data?.applicant as Record<string, unknown>)
    ?.applicantName;
  return !!(
    name &&
    applicantName &&
    String(name).trim().toLowerCase() ===
      String(applicantName).trim().toLowerCase()
  );
}

/**
 * Attaches the API to `window.RelatedPeople` so CHEFS form scripts (which run
 * in separate Form.io instances per step) can call it. Must run before any
 * CHEFS <chefs-form-viewer> initializes — called once from main.ts bootstrap.
 */
export function registerRelatedPeopleGlobal(): void {
  const api: RelatedPeopleApi = {
    TYPES: RELATED_PEOPLE_TYPES,
    isYes,
    classify,
    hasMinorConcept,
    getRepresentative,
    describeRepresentative,
    forEach: forEachRelatedPerson,
    collectAll: collectAllRelatedPeople,
    isApplicant,
  };
  window.RelatedPeople = api;
}
