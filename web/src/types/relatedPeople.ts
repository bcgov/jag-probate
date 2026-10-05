// ── Related People domain types ────────────────────────────────────────────────
// Shared model for every "related person" collected in step 3 (spouse, child,
// parent, sibling, creditor person/org, citor) plus nested grandchildren, so
// step 4 (applicant) and step 5 (notify) scripts can traverse them generically
// instead of each re-implementing the same per-type loop.

export type RelatedPersonStatus =
  'org' | 'deceased' | 'adult-competent' | 'minor' | 'incompetent-adult';

export type RepresentativeRole = 'guardian' | 'nominee' | 'personalRep';

export interface Representative {
  role: RepresentativeRole;
  name: string;
  /** Only meaningful for role === 'nominee'. */
  formal?: boolean;
}

export interface RelatedPersonParentRef {
  type: string;
  name: string;
  record: NormalizedPerson;
}

export interface NormalizedPerson {
  type: string;
  name: string;
  isAlive: boolean;
  isAdult: boolean;
  isCompetent: boolean;
  hasGuardian: boolean;
  guardianName: string;
  hasNominee: boolean;
  nomineeName: string;
  nomineeFormal: boolean;
  hasPersonalRep: boolean;
  personalRepName: string;
  died5DaysAfter: boolean;
  status: RelatedPersonStatus;
  representative: Representative | null;
  /** Set for nested types (e.g. grandchild nested under child). */
  parent: RelatedPersonParentRef | null;
  /** The raw (prefixed) Form.io record this was normalized from. */
  raw: Record<string, unknown>;
}

/** A flat type reads its array directly from `data[containerKey][arrayKey]`. */
export interface FlatRelatedPeopleTypeConfig {
  key: string;
  prefix: string;
  dataPath: [containerKey: string, arrayKey: string];
  parentKey?: undefined;
  /** Organizations have no alive/adult/competent concept (e.g. creditorOrg). */
  orgOnly?: boolean;
  /** False when the type has no isAdult/guardian question at all (e.g. parent - always treated as adult). Defaults to true. */
  hasMinorConcept?: boolean;
}

/** A nested type's array lives inside each raw record of its parent type. */
export interface NestedRelatedPeopleTypeConfig {
  key: string;
  prefix: string;
  dataPath?: undefined;
  parentKey: string;
  /** Property name on the parent's RAW record holding this type's array. */
  nestedArrayKey: string;
  orgOnly?: boolean;
  /** False when the type has no isAdult/guardian question at all (e.g. parent - always treated as adult). Defaults to true. */
  hasMinorConcept?: boolean;
}

export type RelatedPeopleTypeConfig =
  FlatRelatedPeopleTypeConfig | NestedRelatedPeopleTypeConfig;

export function isFlatRelatedPeopleType(
  config: RelatedPeopleTypeConfig
): config is FlatRelatedPeopleTypeConfig {
  return !config.parentKey;
}

export interface RelatedPeopleApi {
  TYPES: RelatedPeopleTypeConfig[];
  isYes: (value: unknown) => boolean;
  classify: (
    input: { isAlive: boolean; isAdult: boolean; isCompetent: boolean },
    config: RelatedPeopleTypeConfig
  ) => RelatedPersonStatus;
  hasMinorConcept: (config: RelatedPeopleTypeConfig) => boolean;
  getRepresentative: (
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
  ) => Representative | null;
  describeRepresentative: (
    representative: Representative,
    principalName: string
  ) => string;
  forEach: (
    data: Record<string, unknown>,
    callback: (
      person: NormalizedPerson,
      config: RelatedPeopleTypeConfig
    ) => void
  ) => void;
  collectAll: (data: Record<string, unknown>) => NormalizedPerson[];
  isApplicant: (data: Record<string, unknown>, name: string) => boolean;
}

declare global {
  interface Window {
    RelatedPeople?: RelatedPeopleApi;
  }
}
