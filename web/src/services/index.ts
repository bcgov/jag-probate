import type { App } from 'vue';
import AuthService from './AuthService';
import ChefsService from './ChefsService';
import HttpService from './HttpService';

/**
 * Registers services as Vue provide/inject singletons.
 * Follows jasper's service registration pattern.
 */
export function registerServices(app: App) {
  const httpService = new HttpService(import.meta.env.BASE_URL);
  const authService = new AuthService(httpService);
  const chefsService = new ChefsService(httpService);

  app.provide('httpService', httpService);
  app.provide('authService', authService);
  app.provide('chefsService', chefsService);
}

export { AuthService, ChefsService, HttpService };
