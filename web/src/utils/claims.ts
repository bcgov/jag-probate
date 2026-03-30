import type { UserInfo } from '@/types';

/**
 * Flattens UserInfo claims array into a plain object suitable
 * for passing as the CHEFS form 'token' attribute.
 * https://developer.gov.bc.ca/docs/default/component/chefs-techdocs/Capabilities/Integrations/Embedding-Webcomponent/#advanced-configuration
 *
 *
 * Example output:
 * {
 *   sub: "abc123",
 *   email: "user@gov.bc.ca",
 *   name: "John Doe",
 *   roles: ["admin"]
 * }
 */
export function extractTokenPayload(userInfo: UserInfo): Record<string, any> {
  const payload: Record<string, any> = {};

  for (const claim of userInfo.claims) {
    const key = normalizeClaimKey(claim.type);

    // Handle repeated claims (e.g. multiple roles) as arrays
    if (key in payload) {
      if (!Array.isArray(payload[key])) {
        payload[key] = [payload[key]];
      }
      payload[key].push(claim.value);
    } else {
      payload[key] = claim.value;
    }
  }

  // Always include name from top-level UserInfo as fallback
  if (!payload.name && userInfo.name) {
    payload.name = userInfo.name;
  }

  return payload;
}

/**
 * Normalises claim type URIs to short keys.
 * e.g. "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" → "sub"
 */
function normalizeClaimKey(claimType: string): string {
  const map: Record<string, string> = {
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier':
      'sub',
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress':
      'email',
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name': 'name',
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname':
      'firstName',
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname': 'lastName',
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'roles',
  };

  return map[claimType] ?? claimType.split('/').pop() ?? claimType;
}
