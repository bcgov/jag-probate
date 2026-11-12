/** API response shape returned by GET /api/Submissions and GET /api/Submissions/{id}. */
export interface SubmissionResponseDto {
  publicId: string; // Guid
  applicantName: string;
  createdBy: string;
  /** Status code stored by the API; see `SUBMISSION_STATUS_LABELS`. */
  status: string;
  lastUpdatedAt: string;
  /** Set only once the package has been eFiled (locks delete). */
  lastFiledAt: string | null;
  createdAt: string;
}

/** Human-readable labels for the status codes stored by the API. */
export const SUBMISSION_STATUS_LABELS: Record<string, string> = {
  draft: 'Draft',
  ready_to_print: 'Ready to Print',
  // Legacy value from before the "Save Completed Application" rename.
  submitted: 'Ready to Print',
};

/** Maps an API status code to its display label (falls back to the raw code). */
export function formatSubmissionStatus(
  status: string | null | undefined
): string {
  if (!status) return '';
  return SUBMISSION_STATUS_LABELS[status] ?? status;
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
  /** Display label for the application status (e.g. "Ready to Print"). */
  status: string;
  /** Displayed Package# column (populated by future efiling integration). */
  packageNum: string;
  /** Alias for publicId – kept for template compatibility. */
  id: string;
  publicId: string;
  /** Minutes since epoch (used for sorting). */
  lastUpdated: number;
  lastUpdatedDate: string;
  /** Minutes since epoch (used for sorting). */
  lastFiled: number;
  lastFiledDate: string;
  /** ISO string from the API response, used as fallback display date. */
  createdAt?: string;
}
