/** API response shape returned by GET /api/Submissions and GET /api/Submissions/{id}. */
export interface SubmissionResponseDto {
  publicId: string; // Guid
  chefsSubmissionId: string;
  applicantName: string;
  createdBy: string;
  status: string;
  lastUpdatedAt: string;
  lastFiledAt: string | null;
  createdAt: string;
}

/** Payload sent to POST /api/Submissions/upsert. */
export interface UpsertSubmissionDto {
  publicId?: string;
  chefsSubmissionId: string;
  createdBy: string | undefined;
  applicantName: string;
  status: string | undefined;
  lastUpdatedAt: string;
  lastFiledAt: string | null;
  submissionData: string | null;
}

/** API response shape for step data (GET /api/submissions/{id}/steps/{formId}). */
export interface StepDataResponseDto {
  publicId: string;
  formId: string;
  data: string;
  formVersion: string | null;
  createdAt: string;
  updatedAt: string | null;
}

/** Payload sent to PUT /api/submissions/{id}/steps/{formId}. */
export interface UpsertStepDataDto {
  formId: string;
  data: string;
  formVersion?: string | null;
}

/** UI-mapped shape used in the Previous Activity table. */
export interface PreviousApplication {
  deceased_name: string;
  app_type: string;
  status: string;
  /** Displayed Package# column (equals chefsSubmissionId). */
  packageNum: string;
  /** Alias for publicId – kept for template compatibility. */
  id: string;
  publicId: string;
  chefsSubmissionId: string;
  /** Minutes since epoch (used for sorting). */
  lastUpdated: number;
  lastUpdatedDate: string;
  /** Minutes since epoch (used for sorting). */
  lastFiled: number;
  lastFiledDate: string;
  /** ISO string from the API response, used as fallback display date. */
  createdAt?: string;
}
