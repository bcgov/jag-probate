import type { UserInfo } from '@/types';
import HttpService from './HttpService';

/**
 * Service for authentication-related API calls.
 * Mirrors jasper's AuthService pattern.
 */
class AuthService {
  private httpService: HttpService;

  constructor(httpService: HttpService) {
    this.httpService = httpService;
  }

  /**
   * Fetches the current user's info from the backend.
   * Returns user info if authenticated, or triggers a 401 redirect via HttpService.
   */
  async getUserInfo(): Promise<UserInfo> {
    return await this.httpService.get<UserInfo>('api/auth/user');
  }

  /**
   * Checks authentication status without triggering a redirect.
   * Used for non-protected pages (e.g., landing page) to show/hide UI elements.
   */
  async getAuthStatus(): Promise<{ isAuthenticated: boolean; name: string | null }> {
    return await this.httpService.get('api/auth/status');
  }
}

export default AuthService;
