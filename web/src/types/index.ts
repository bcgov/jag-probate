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
