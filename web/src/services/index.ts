import type { App } from 'vue';
import AuthService from './AuthService';
import HttpService from './HttpService';

/**
 * Registers services as Vue provide/inject singletons.
 * Follows jasper's service registration pattern.
 */
export function registerServices(app: App) {
  const httpService = new HttpService(import.meta.env.BASE_URL);
  const authService = new AuthService(httpService);

  app.provide('httpService', httpService);
  app.provide('authService', authService);
}

export { AuthService, HttpService };
