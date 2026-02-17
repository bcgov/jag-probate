export interface UserInfo {
  isAuthenticated: boolean;
  name: string | null;
  authenticationType: string | null;
  claims: { type: string; value: string }[];
}
