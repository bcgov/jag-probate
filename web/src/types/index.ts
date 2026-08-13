export interface UserInfo {
  isAuthenticated: boolean;
  name: string | null;
  authenticationType: string | null;
  claims: { type: string; value: string }[];
}

export interface ChefsAuthToken {
  /** Short-lived JWT for the CHEFS web component. */
  token: string;
  /** CHEFS form GUID to load. */
  formId: string;
  /** CHEFS base URL the web component must use (matches the token issuer). */
  baseUrl: string;
}

/** Sidebar substep (panel) metadata. */
export interface SidebarSubstepDto {
  key: string;
  title: string;
  icon: string;
  disabled: boolean;
}

/** Sidebar step metadata for a single top-level step.*/
export interface SidebarStepDto {
  key: string;
  order: number;
  title: string;
  icon: string;
  children: SidebarSubstepDto[];
}

export type {
  PreviousApplication,
  StepDataResponseDto,
  SubmissionResponseDto,
  UpsertStepDataDto,
  UpsertSubmissionDto,
} from './submission';

export interface CourtAddress {
  addressLine1?: string;
  addressLine2?: string;
  addressLine3?: string;
  postalCode?: string;
  cityName?: string;
  provinceName?: string;
  countryName?: string;
}

export interface CourtLocationModel {
  id?: number;
  identifierCode?: string;
  name?: string;
  code?: string;
  isSupremeCourt: boolean;
  address?: CourtAddress;
}

export interface CourtLocationResult {
  courts: CourtLocationModel[];
}

export interface GenerateReportRequest {
  /** Logical template key, e.g. "P1". */
  templateKey: string;
  /** The raw CHEFS form submission data. */
  submissionData: unknown;
}

export interface GenerateReportResponse {
  /** Short-lived URL to stream the PDF (valid 10 min). Use as iframe src or anchor href. */
  url: string;
  /** Suggested filename, e.g. "P1.pdf". */
  fileName: string;
}
