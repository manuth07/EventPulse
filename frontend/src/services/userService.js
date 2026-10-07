import { apiClient, getStoredToken } from './apiClient';

/**
 * Fetch the authenticated user's authoritative profile from Identity Service (EP-26).
 *
 * @param {string} [token] - Optional JWT token override.
 * @returns {Promise<{ id: string, firstName: string, lastName: string, email: string, phoneNumber?: string, countryCode?: string, role: string, roles: string[], profileCompleted: boolean, hasPassword: boolean, profilePictureUrl?: string, createdAt: string }>}
 */
export async function getCurrentUserProfile(token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};

  return apiClient.get('/api/users/me', { headers });
}

export default {
  getCurrentUserProfile,
};
