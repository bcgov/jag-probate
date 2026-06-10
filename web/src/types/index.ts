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

export interface SubmissionResponseDto {
  id: number;
  chefsSubmissionId: string;
  applicantName: string;
  createdBy: string;
  status: string;
  lastUpdatedAt: string;
  lastFiledAt: string;
  createdAt: string;
}

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
