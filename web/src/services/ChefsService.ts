import type {
  ChefsAuthToken,
  SidebarStepDto,
  StepDataResponseDto,
  SubmissionResponseDto,
  UpsertStepDataDto,
  UpsertSubmissionDto,
} from '@/types';
import HttpService from './HttpService';

/**
 * Service for CHEFS-related API calls.
 * Handles fetching auth tokens for the CHEFS web component embedding.
 */
class ChefsService {
  private httpService: HttpService;

  constructor(httpService: HttpService) {
    this.httpService = httpService;
  }

  /**
   * Fetches a short-lived auth token and form ID for the given logical form key.
   * The backend resolves the key to the actual CHEFS form GUID, keeping it
   * server-side and never exposing it to the client directly.
   *
   * @param formKey Logical form key (e.g. "legal", "nonlegal").
   */
  async getAuthToken(formKey: string): Promise<ChefsAuthToken> {
    return await this.httpService.get<ChefsAuthToken>(
      `api/chefs/auth?formKey=${encodeURIComponent(formKey)}`
    );
  }

  /**
   * Fetches all submitted applications for the given logical form key.
   * Calls GET /api/Chefs/Applications?formKey=...
   *
   * @param formKey Logical form key (e.g. "probate").
   */
  async getApplications(formKey: string): Promise<any[]> {
    return await this.httpService.get<any[]>(
      `api/Chefs/Applications?formKey=${encodeURIComponent(formKey)}`
    );
  }

  /**
   * Fetches the sidebar steps and substeps.
   */
  async getSidebarStructure(): Promise<SidebarStepDto[]> {
    return await this.httpService.get<SidebarStepDto[]>('api/Chefs/Sidebar');
  }

  async getSubmissions(): Promise<SubmissionResponseDto[]> {
    return await this.httpService.get('/api/submissions');
  }

  async getSubmission(id: string): Promise<SubmissionResponseDto> {
    return await this.httpService.get<SubmissionResponseDto>(
      `api/Submissions/${id}`
    );
  }

  async upsertSubmission(
    data: UpsertSubmissionDto
  ): Promise<SubmissionResponseDto> {
    return await this.httpService.post<SubmissionResponseDto>(
      'api/Submissions/upsert',
      data
    );
  }

  async deleteSubmission(id: string): Promise<void> {
    await this.httpService.delete<void>(`api/Submissions/${id}`);
  }

  // ── Step Data endpoints ─────────────────────────────────────────────

  async upsertStepData(
    submissionPublicId: string,
    formId: string,
    data: UpsertStepDataDto
  ): Promise<StepDataResponseDto> {
    return await this.httpService.put<StepDataResponseDto>(
      `api/submissions/${submissionPublicId}/steps/${encodeURIComponent(formId)}`,
      data
    );
  }

  async getStepData(
    submissionPublicId: string,
    formId: string
  ): Promise<StepDataResponseDto> {
    return await this.httpService.get<StepDataResponseDto>(
      `api/submissions/${submissionPublicId}/steps/${encodeURIComponent(formId)}`
    );
  }

  async getAllStepData(
    submissionPublicId: string
  ): Promise<StepDataResponseDto[]> {
    return await this.httpService.get<StepDataResponseDto[]>(
      `api/submissions/${submissionPublicId}/steps`
    );
  }

  async getCompiledData(submissionPublicId: string): Promise<string> {
    return await this.httpService.get<string>(
      `api/submissions/${submissionPublicId}/steps/compiled`
    );
  }

  async submitApplication(
    submissionPublicId: string
  ): Promise<SubmissionResponseDto> {
    return await this.httpService.post<SubmissionResponseDto>(
      `api/submissions/${submissionPublicId}/submit`
    );
  }
}

export default ChefsService;
