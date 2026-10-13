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
  { key: 'sibling', prefix: 'sibling', dataPath: ['sibling', 'siblingData'] },
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

/** True unless the type config explicitly opts out (parent - no isAdult/guardian question exists for it). */
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
      Record<string, unknown> | undefined;
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

/** Generic field-name reset shared by every type's add/edit scratch object: booleans -> false, arrays -> [] (e.g. child's nested gchildData), everything else -> "", then control flags re-asserted (matches the existing per-type clear*Form() scripts' own convention). */
function resetScratch(scratch: Record<string, unknown>, prefix: string): void {
  Object.keys(scratch).forEach((key) => {
    const current = scratch[key];
    scratch[key] = Array.isArray(current)
      ? []
      : typeof current === 'boolean'
        ? false
        : '';
  });
  scratch[`${prefix}FormOpen`] = false;
  scratch[`${prefix}FormShowErrors`] = false;
  scratch[`${prefix}EditIndex`] = '-1';
}

/**
 * Clears a flat related-people type's committed array (e.g. data.spouse.spouseData)
 * and its add/edit scratch object (data.spouse._spouseAddEdit) — driven entirely by
 * the registry, no per-type hardcoding. Called from a CHEFS gate-question watcher
 * when the user flips "Did the deceased have a spouse?" etc. from yes to no.
 */
export function clearRelatedPeopleType(
  data: Record<string, unknown>,
  typeKey: string
): void {
  const config = RELATED_PEOPLE_TYPES.find((c) => c.key === typeKey);
  if (!config || !isFlatRelatedPeopleType(config)) return;

  const [containerKey, arrayKey] = config.dataPath;
  const container = (data[containerKey] ??= {}) as Record<string, unknown>;
  container[arrayKey] = [];

  const scratchKey = `_${config.prefix}AddEdit`;
  const scratch = (container[scratchKey] ??= {}) as Record<string, unknown>;
  resetScratch(scratch, config.prefix);
}

/**
 * Clears a nested type's in-progress array on its parent's currently-open
 * scratch object — e.g. grandchild's `gchildData` on `data.child._childAddEdit`
 * when a per-child gate ("Did this child have any children?") flips to no.
 * Driven by the registry's parentKey/nestedArrayKey, no hardcoded field names.
 * No-op for flat/unknown types or when the parent's scratch isn't open.
 */
export function clearNestedRelatedPeopleType(
  data: Record<string, unknown>,
  typeKey: string
): void {
  const config = RELATED_PEOPLE_TYPES.find((c) => c.key === typeKey);
  if (!config || isFlatRelatedPeopleType(config)) return;

  const parentConfig = RELATED_PEOPLE_TYPES.find(
    (c) => c.key === config.parentKey
  );
  if (!parentConfig || !isFlatRelatedPeopleType(parentConfig)) return;

  const [containerKey] = parentConfig.dataPath;
  const container = data[containerKey] as Record<string, unknown> | undefined;
  const scratch = container?.[`_${parentConfig.prefix}AddEdit`] as
    Record<string, unknown> | undefined;
  if (!scratch) return;

  scratch[config.nestedArrayKey] = [];
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
    clear: clearRelatedPeopleType,
    clearNested: clearNestedRelatedPeopleType,
  };
  window.RelatedPeople = api;
}
