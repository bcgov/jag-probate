import { forEachRelatedPerson, isApplicant, isYes } from './relatedPeople';

export interface NotifyRecipientSummary {
  hasRecipients: boolean;
  hasMinorOrIncapable: boolean;
}

const MINOR_CHECK_TYPES = [
  'spouse',
  'child',
  'parent',
  'sibling',
  'creditorPerson',
  'grandchild',
];
const COUNT_TYPES = [...MINOR_CHECK_TYPES, 'creditorOrg', 'citor'];
const GATED_TYPES = ['spouse', 'child', 'parent', 'sibling', 'grandchild'];

export interface NotifyRecipientRow {
  recipientName: string;
  recipientRole: string;
  isPGT: boolean;
  isFirstNation: boolean;
  firstNationCode: string;
  p1Delivered: string;
  delivererName: string;
  deliveryDate: string;
  deliveryMethod: string;
  electronicReceipt: string;
  electronicRetain: boolean;
  [key: string]: unknown;
}

/** Same completeness rule the Notify People table script uses. */
export function isNotifyRowComplete(row: Record<string, any>): boolean {
  if (!row?.recipientName) return false;
  if (!row.p1Delivered || row.p1Delivered === 'no') return false;
  if (!row.delivererName || !row.deliveryDate || !row.deliveryMethod) {
    return false;
  }
  if (row.deliveryMethod === 'electronic') {
    return row.electronicReceipt === 'yes' && row.electronicRetain === true;
  }
  return true;
}

const PGT_NAME = 'The Public Guardian and Trustee (PGT)';

const TYPE_OPTIONS: Record<
  string,
  { pgtOnMinor: boolean; personalRepRequiresDied5DaysAfter: boolean }
> = {
  spouse: { pgtOnMinor: true, personalRepRequiresDied5DaysAfter: true },
  child: { pgtOnMinor: true, personalRepRequiresDied5DaysAfter: true },
  parent: { pgtOnMinor: true, personalRepRequiresDied5DaysAfter: true },
  sibling: { pgtOnMinor: true, personalRepRequiresDied5DaysAfter: true },
  creditorPerson: {
    pgtOnMinor: true,
    personalRepRequiresDied5DaysAfter: false,
  },
  citor: { pgtOnMinor: false, personalRepRequiresDied5DaysAfter: false },
  grandchild: { pgtOnMinor: true, personalRepRequiresDied5DaysAfter: true },
};

/**
 * Host-side port of the Notify step's notifyPeopleData calculateValue, so the
 * recipient list is correct without opening that step. Rows that still apply
 * keep their user-entered delivery fields (taken from existingRows).
 */
export function buildNotifyPeopleRows(
  data: Record<string, any>,
  existingRows: unknown
): NotifyRecipientRow[] {
  const applicant = data.applicant ?? {};
  const applicantName = String(applicant.applicantName ?? '');
  const selfRep = applicantName.match(
    /^(.*) \((?:Guardian|Nominee|Personal Representative) of .*\)$/
  );
  const expected: Array<
    Pick<
      NotifyRecipientRow,
      | 'recipientName'
      | 'recipientRole'
      | 'isPGT'
      | 'isFirstNation'
      | 'firstNationCode'
    >
  > = [];

  const add = (
    name: unknown,
    role = '',
    isPGT = false,
    isFirstNation = false,
    firstNationCode = ''
  ) => {
    if (
      !name ||
      name === applicant.applicantName ||
      (selfRep && String(name).trim() === selfRep[1].trim())
    ) {
      return;
    }
    expected.push({
      recipientName: String(name),
      recipientRole: role,
      isPGT,
      isFirstNation,
      firstNationCode,
    });
  };
  const formatRole = (name: string, role: string) => `${name}'s ${role}`;

  forEachRelatedPerson(data, (person, config) => {
    if (config.key === 'creditorOrg') {
      add(person.name);
      return;
    }
    if (config.key === 'grandchild') {
      const parentChild = person.parent?.record;
      if (
        !parentChild ||
        parentChild.status !== 'deceased' ||
        parentChild.died5DaysAfter
      ) {
        return;
      }
    }
    const options = TYPE_OPTIONS[config.key];
    if (!options) return;

    if (person.status === 'adult-competent') {
      add(person.name);
    } else if (person.status === 'minor') {
      if (person.representative) {
        add(person.representative.name, formatRole(person.name, 'Guardian'));
      } else {
        add(person.name);
      }
      if (options.pgtOnMinor) add(PGT_NAME, '', true);
    } else if (person.status === 'incompetent-adult') {
      if (person.representative) {
        add(person.representative.name, formatRole(person.name, 'Nominee'));
        if (!person.representative.formal) add(person.name);
      } else {
        add(person.name);
      }
      add(PGT_NAME, '', true);
    } else if (person.status === 'deceased') {
      const gateOk = options.personalRepRequiresDied5DaysAfter
        ? person.died5DaysAfter
        : true;
      if (gateOk && person.representative) {
        add(
          person.representative.name,
          formatRole(person.name, 'Personal Representative')
        );
      }
    }
  });

  const includeSuccessors =
    applicant.applicantNameSelection === '__other__' &&
    applicant.applicantHasBeenNominated === 'no' &&
    applicant.applicantIsOtherSuccessor === 'yes' &&
    applicant.applicantHasSamePrioritySuccessors === 'yes' &&
    ((applicant.applicantStep3PeopleExist === true &&
      applicant.applicantCourtAppointed === 'yes') ||
      (applicant.applicantStep3PeopleExist !== true &&
        (applicant.applicantMajorityInInterestConsent === 'yes' ||
          applicant.applicantMajorityInInterestConsent === 'no')));
  if (includeSuccessors) {
    const raw = applicant.applicantSamePrioritySuccessorNames;
    const names = Array.isArray(raw) ? raw : raw ? [raw] : [];
    for (const entry of names) {
      const line = String(
        (entry && typeof entry === 'object'
          ? entry.applicantSuccessorEntry
          : entry) ?? ''
      ).trim();
      if (!line) continue;
      const match = line.match(/^(.*?)\s*\((.*)\)\s*$/);
      add(match ? match[1].trim() : line, match ? match[2].trim() : '');
    }
  }

  if (data.deceasedFirstNationYN === 'yes' && data.firstNationMembership) {
    add(
      data.firstNationMembershipDisplayName,
      '',
      false,
      true,
      data.firstNationMembership
    );
  }

  const existingByName = new Map<string, Record<string, any>>();
  for (const row of Array.isArray(existingRows) ? existingRows : []) {
    const name = row?.recipientName ? String(row.recipientName) : '';
    if (name && !existingByName.has(name)) existingByName.set(name, row);
  }

  const seen = new Set<string>();
  const rows: NotifyRecipientRow[] = [];
  for (const item of expected) {
    if (seen.has(item.recipientName)) continue;
    seen.add(item.recipientName);
    const previous = existingByName.get(item.recipientName);
    rows.push({
      p1Delivered: '',
      delivererName: '',
      deliveryDate: '',
      deliveryMethod: '',
      electronicReceipt: '',
      electronicRetain: false,
      ...(previous ?? {}),
      ...item,
    });
  }
  return rows;
}

/**
 * Host-side mirror of the rules the Notify CHEFS form uses to decide whether
 * anyone needs notice, so dependent steps/forms stay correct without visiting
 * the Notify step.
 */
export function summarizeNotifyRecipients(
  data: Record<string, any>
): NotifyRecipientSummary {
  let hasMinorOrIncapable = false;
  let peopleCount = 0;

  forEachRelatedPerson(data, (person, config) => {
    if (config.key === 'grandchild') {
      const parentChild = person.parent?.record;
      if (
        !parentChild ||
        parentChild.status !== 'deceased' ||
        parentChild.died5DaysAfter
      ) {
        return;
      }
    }

    if (
      MINOR_CHECK_TYPES.includes(config.key) &&
      (person.status === 'minor' || person.status === 'incompetent-adult')
    ) {
      hasMinorOrIncapable = true;
    }

    if (!COUNT_TYPES.includes(config.key)) return;
    const repName = person.representative?.name;
    if (person.status === 'deceased') {
      if (
        repName &&
        (!GATED_TYPES.includes(config.key) || person.died5DaysAfter) &&
        !isApplicant(data, repName)
      ) {
        peopleCount++;
      }
      return;
    }
    if (person.name && !isApplicant(data, person.name)) peopleCount++;
    if (repName && !isApplicant(data, repName)) peopleCount++;
  });

  const rawNames = data.applicant?.applicantSamePrioritySuccessorNames;
  const successorNames = Array.isArray(rawNames)
    ? rawNames
    : rawNames
      ? [rawNames]
      : [];
  const hasSamePrioritySuccessors = successorNames.some(
    (entry: any) =>
      String(
        (entry && typeof entry === 'object'
          ? entry.applicantSuccessorEntry
          : entry) ?? ''
      ).trim() !== ''
  );

  return {
    hasMinorOrIncapable,
    hasRecipients:
      peopleCount > 0 ||
      hasMinorOrIncapable ||
      isYes(data.deceasedFirstNationYN) ||
      hasSamePrioritySuccessors,
  };
}
