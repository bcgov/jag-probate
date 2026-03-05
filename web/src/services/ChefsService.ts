import type { ChefsAuthToken } from '@/types';
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
}

export default ChefsService;
