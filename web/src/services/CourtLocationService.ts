import type { CourtLocationResult } from '@/types';
import HttpService from './HttpService';

/**
 * Service for court location API calls.
 * Fetches court locations from eFiling Hub API via backend.
 */
class CourtLocationService {
  private httpService: HttpService;

  constructor(httpService: HttpService) {
    this.httpService = httpService;
  }

  /**
   * Fetches all available court locations.
   * Results are cached on the backend for 24 hours.
   *
   * @returns Court locations result with list of courts
   */
  async getCourtLocations(): Promise<CourtLocationResult> {
    return await this.httpService.get<CourtLocationResult>(
      'api/courtlocations'
    );
  }
}

export default CourtLocationService;
