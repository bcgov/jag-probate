/** Shared test fixtures for the pathway machine (mirrors api/appsettings.json). */
import { mapSidebarStepsToWizardSteps } from '@/config/wizardSteps';
import type { SidebarStepDto } from '@/types';
import type { WizardStep } from '@/types/applicationStep';

type Child = [key: string, disabled?: boolean];

const CONFIG: Array<[string, Child[]]> = [
  ['form-deceased', [['form-deceased']]],
  [
    'form-testamentary-docs',
    [['form-wills-notice'], ['form-testamentary-docs']],
  ],
  [
    'form-related-people',
    [
      ['form-spouse'],
      ['form-children', true],
      ['form-parent', true],
      ['form-sibling', true],
      ['form-creditors', true],
    ],
  ],
  [
    'form-applicant',
    [
      ['form-applicant-information'],
      ['form-citor', true],
      ['form-applicant-service'],
    ],
  ],
  [
    'form-notify',
    [
      ['form-full-review'],
      ['form-tell-people'],
      ['form-preview-p1'],
      ['form-preview-pgt'],
      ['form-notify-people'],
    ],
  ],
  [
    'form-assets',
    [
      ['form-assets-details'],
      ['form-assets-real-property-bc'],
      ['form-assets-tangible-personal-property-bc'],
      ['form-assets-intangible-property'],
      ['form-assets-real-property-outside-bc'],
      ['form-assets-tangible-personal-property-outside-bc'],
      ['form-assets-additional-information'],
    ],
  ],
  [
    'form-application-documents',
    [
      ['form-preview-p9'],
      ['form-preview-p5'],
      ['form-preview-p18'],
      ['form-preview-p10'],
      ['form-preview-p11'],
      ['form-preview-p2'],
    ],
  ],
  ['form-filing', [['form-filing-instructions']]],
];

const STEP_DTOS: SidebarStepDto[] = CONFIG.map(([key, children], i) => ({
  key,
  order: i + 1,
  title: key,
  icon: '',
  children: children.map(([childKey, disabled]) => ({
    key: childKey,
    title: childKey,
    icon: '',
    disabled: !!disabled,
    hiddenIsGhost: true,
  })),
}));

export const STEPS: WizardStep[] = mapSidebarStepsToWizardSteps(STEP_DTOS);

/**
 * Accumulated data of a finished application on the plain relative path
 * (spouse applies, no will, BC address, assets confirmed). Every gate passes
 * and the Step 5 review has nothing missing.
 */
export function completeApplication(): Record<string, any> {
  return {
    showStep1Items: true,
    firstNameGivenName: 'Jane',
    lastNameFamilyNameOrSurname: 'Doe',
    deceasedName: 'Jane Doe',
    deceasedDeathDate: '2024-01-02',
    deceasedAddressStreet: '1 Main St',
    deceasedAddressCity: 'Victoria',
    deceasedAddressCountry: 'Canada',
    deceasedAddressProvince: 'British Columbia',
    deceasedFirstNationYN: 'no',
    HaveYouLookedForDeceasedWillWellYN: 'yes',
    willExists: 'no',
    willsNoticeResultsReceived: 'yes',
    willsNoticeHasResults: 'no',
    willsNoticeFoundWillOrOtherTestDocs: 'no',
    willGrantExists: 'no',
    noExistingGrantAwareOfOtherApplicantsYesNo: 'no',
    spouse: {
      spouseExists: 'yes',
      spouseCompleted2: 'yes',
      spouseData: [
        {
          spouseName: 'John Doe',
          spouseIsAlive: 'yes',
          spouseIsAdult: 'yes',
          spouseIsCompetent: 'yes',
        },
      ],
    },
    child: { childExists: 'no', childCompleted2: 'yes' },
    creditor: {
      creditorPersonExists: 'no',
      creditorOrgExists: 'no',
      creditorCompleted: 'yes',
    },
    applicant: {
      applicantNameSelection: 'John Doe',
      applicantName: 'John Doe',
      applicantOccupation: 'Teacher',
      applicantOrdinaryAddressStreet: '1 Main St',
      applicantOrdinaryAddressReceiveMail: 'yes',
      applicantCited: 'no',
      applicantHasLawyer: 'no',
      applicantServiceAddressStreet: '1 Main St',
      applicantServicePhone: '250-555-0100',
      applicantServiceEmail: 'john@example.com',
      applicantHasFax: 'no',
    },
    notifyPeople: { notifyPeopleData: [] },
    q42CanIdentifyAssets: 'yes',
    q424AssetsConfirmed: 'yes',
  };
}
