import type { ChefsAuthToken, SubmissionResponseDto } from '@/types';
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

  async getSubmissions(): Promise<SubmissionResponseDto[]> {
    return await this.httpService.get('/api/submissions');
  }

  async createLocalSubmission(data: {
    chefsSubmissionId: string;
    createdBy: string;
    applicantName: string;
    status: string;
    lastUpdatedAt: string;
    lastFiledAt: string;
  }): Promise<void> {
    await this.httpService.post('/api/submissions', {
      chefsSubmissionId: data.chefsSubmissionId,
      applicantName: data.applicantName,
      createdBy: data.createdBy,
      status: data.status,
      lastUpdatedAt: data.lastUpdatedAt,
      lastFiledAt: data.lastFiledAt,
    });
  }
}

export default ChefsService;
