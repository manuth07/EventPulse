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

/**
 * Update the authenticated user's email address (EP-26 Phase 2).
 *
 * @param {string} newEmail - New email address.
 * @param {string} [token] - Optional JWT token override.
 * @returns {Promise<{ email: string, message: string }>}
 */
export async function updateUserEmail(newEmail, token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};

  return apiClient.put('/api/users/me/email', { newEmail }, { headers });
}

/**
 * Update the authenticated user's phone number (EP-26 Phase 2).
 *
 * @param {string} newPhoneNumber - New phone number.
 * @param {string} [token] - Optional JWT token override.
 * @returns {Promise<{ phoneNumber: string, message: string }>}
 */
export async function updateUserPhone(newPhoneNumber, token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};

  return apiClient.put('/api/users/me/phone', { newPhoneNumber }, { headers });
}

/**
 * Change or add password for authenticated user (EP-26 Phase 4).
 *
 * @param {{ currentPassword?: string, newPassword: string, confirmPassword: string }} payload
 * @param {string} [token] - Optional JWT token override.
 * @returns {Promise<{ message: string }>}
 */
export async function changeUserPassword(payload, token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};

  return apiClient.post('/api/users/me/change-password', payload, { headers });
}

/**
 * Upload profile avatar image (EP-26 Phase 3).
 *
 * @param {File} file - Image file (JPEG, PNG, WebP, max 2MB).
 * @param {string} [token] - Optional JWT token override.
 * @returns {Promise<{ profilePictureUrl: string, message: string }>}
 */
export async function uploadUserAvatar(file, token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};

  const formData = new FormData();
  formData.append('avatar', file);

  return apiClient.post('/api/users/me/avatar', formData, { headers });
}

/**
 * Remove profile avatar image (EP-26 Phase 3).
 *
 * @param {string} [token] - Optional JWT token override.
 * @returns {Promise<{ message: string }>}
 */
export async function removeUserAvatar(token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};

  return apiClient.delete('/api/users/me/avatar', { headers });
}

export default {
  getCurrentUserProfile,
  updateUserEmail,
  updateUserPhone,
  changeUserPassword,
  uploadUserAvatar,
  removeUserAvatar,
};

