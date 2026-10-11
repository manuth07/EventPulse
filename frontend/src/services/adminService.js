/**
 * EventPulse Frontend - Administrative Management Service (EP-57 / EP-370 / EP-371 / EP-372)
 *
 * Provides typed, secure client dispatchers for administrative management:
 * - Platform overview & metrics
 * - User directory pagination, search & filtering
 * - User detail inspection
 * - Account activation & deactivation
 * - Role promotion & demotion
 */

import { apiClient, getStoredToken } from './apiClient';

/**
 * Retrieves the high-level platform overview metrics approved in EP-368.
 * Microservice-governed: Identity metrics, Event metrics, Booking metrics.
 * 
 * @param {string} [token] - Optional explicit admin JWT token.
 * @returns {Promise<AdminPlatformOverviewDto>}
 */
export async function getPlatformOverview(token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};
  return apiClient.get('/api/admin/overview', { headers });
}

/**
 * Retrieves a paginated list of registered users with search, role, status filtering, and sorting.
 * 
 * @param {Object} [params={}]
 * @param {string} [params.q] - Search query matching name or email.
 * @param {string} [params.role] - Role filter ('All', 'Customer', 'Organizer', 'Administrator').
 * @param {string} [params.status] - Status filter ('All', 'Active', 'Inactive', 'Unverified').
 * @param {number} [params.page=1] - 1-based page index.
 * @param {number} [params.pageSize=20] - Number of items per page.
 * @param {string} [params.sortBy='CreatedAt'] - Field to sort by.
 * @param {boolean} [params.sortDesc=true] - Sort descending.
 * @param {string} [token] - Optional explicit admin JWT token.
 * @returns {Promise<PagedResult<AdminUserSummaryDto>>}
 */
export async function getAdminUsers(params = {}, token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};

  const queryParams = new URLSearchParams();
  if (params.q && typeof params.q === 'string' && params.q.trim()) {
    queryParams.set('q', params.q.trim());
  }
  if (params.role && params.role !== 'All') {
    queryParams.set('role', params.role);
  }
  if (params.status && params.status !== 'All') {
    queryParams.set('status', params.status);
  }
  if (params.page && Number(params.page) > 0) {
    queryParams.set('page', String(params.page));
  }
  if (params.pageSize && Number(params.pageSize) > 0) {
    queryParams.set('pageSize', String(params.pageSize));
  }
  if (params.sortBy) {
    queryParams.set('sortBy', params.sortBy);
  }
  if (typeof params.sortDesc === 'boolean') {
    queryParams.set('sortDesc', String(params.sortDesc));
  }

  const queryStr = queryParams.toString();
  const endpoint = queryStr ? `/api/admin/users?${queryStr}` : '/api/admin/users';
  return apiClient.get(endpoint, { headers });
}

/**
 * Retrieves safe operational details of an individual user account.
 * 
 * @param {string} userId - Target user Guid.
 * @param {string} [token] - Optional explicit admin JWT token.
 * @returns {Promise<AdminUserDetailsDto>}
 */
export async function getAdminUserDetails(userId, token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};
  return apiClient.get(`/api/admin/users/${userId}`, { headers });
}

/**
 * Activates a previously deactivated user account.
 * 
 * @param {string} userId - Target user Guid.
 * @param {string} [reason] - Reason for activation.
 * @param {string} [token] - Optional explicit admin JWT token.
 * @returns {Promise<{ message: string, user: AdminUserDetailsDto }>}
 */
export async function activateUser(userId, reason = '', token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};
  return apiClient.post(`/api/admin/users/${userId}/activate`, { reason: reason || null }, { headers });
}

/**
 * Deactivates an active user account.
 * Enforces self-deactivation and last active admin safety guards.
 * 
 * @param {string} userId - Target user Guid.
 * @param {string} [reason] - Reason for deactivation.
 * @param {string} [token] - Optional explicit admin JWT token.
 * @returns {Promise<{ message: string, user: AdminUserDetailsDto }>}
 */
export async function deactivateUser(userId, reason = '', token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};
  return apiClient.post(`/api/admin/users/${userId}/deactivate`, { reason: reason || null }, { headers });
}

/**
 * Updates an account's assigned system role (Customer, Organizer, Administrator).
 * Enforces self-demotion and last active admin safety guards.
 * 
 * @param {string} userId - Target user Guid.
 * @param {string} role - New canonical role name.
 * @param {string} [reason] - Reason for role transition.
 * @param {string} [token] - Optional explicit admin JWT token.
 * @returns {Promise<{ message: string, user: AdminUserDetailsDto }>}
 */
export async function changeUserRole(userId, role, reason = '', token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};
  return apiClient.post(`/api/admin/users/${userId}/role`, { role, reason: reason || null }, { headers });
}
