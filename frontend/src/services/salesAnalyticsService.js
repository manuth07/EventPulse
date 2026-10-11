import { apiClient } from './apiClient.js';

/**
 * Sales Analytics API Service (EP-384 / EP-386)
 * 
 * Provides client methods to interact with backend organizer sales analytics endpoints
 * with secure token injection, strict role validation, and sanitized error handling.
 */

/**
 * Maps API errors to safe, user-friendly messages without disclosing
 * internal system state or another organizer's event information (EP-386).
 * 
 * @param {Error|Object} error - Error thrown by apiClient
 * @returns {string} Sanitized user-facing error message
 */
export function formatSalesAnalyticsError(error) {
  const status = error?.status;

  if (status === 401) {
    return 'Authentication required or your session has expired. Please log in again.';
  }

  if (status === 403) {
    return 'Access denied. You do not have permission to view sales analytics.';
  }

  if (status === 404) {
    return 'The requested event was not found or is inaccessible.';
  }

  if (status === 400) {
    return error?.message || 'Invalid query parameters supplied for sales analytics.';
  }

  return 'An error occurred while retrieving sales analytics. Please try again.';
}

/**
 * Retrieves overall sales metrics for the authenticated organizer.
 * GET /api/organizer/analytics/sales
 * 
 * @param {Object} [params]
 * @param {string} [params.from] - Optional ISO date filter start
 * @param {string} [params.to] - Optional ISO date filter end
 * @returns {Promise<Object>}
 */
export async function getOrganizerSalesSummary(params = {}) {
  const query = new URLSearchParams();
  if (params.from) query.append('from', params.from);
  if (params.to) query.append('to', params.to);

  const queryString = query.toString();
  const endpoint = `/api/organizer/analytics/sales${queryString ? `?${queryString}` : ''}`;
  return apiClient.get(endpoint);
}

/**
 * Retrieves paginated event-level sales summaries for all events owned by the organizer.
 * GET /api/organizer/analytics/sales/events
 * 
 * @param {Object} [params]
 * @param {string} [params.search] - Event title search filter
 * @param {string} [params.from] - ISO date filter start
 * @param {string} [params.to] - ISO date filter end
 * @param {number} [params.page=1] - 1-based page index
 * @param {number} [params.pageSize=20] - Page size (1-100)
 * @returns {Promise<Object>}
 */
export async function getOrganizerEventsSalesList(params = {}) {
  const query = new URLSearchParams();
  if (params.search) query.append('search', params.search);
  if (params.from) query.append('from', params.from);
  if (params.to) query.append('to', params.to);
  if (params.page) query.append('page', params.page.toString());
  if (params.pageSize) query.append('pageSize', params.pageSize.toString());

  const queryString = query.toString();
  const endpoint = `/api/organizer/analytics/sales/events${queryString ? `?${queryString}` : ''}`;
  return apiClient.get(endpoint);
}

/**
 * Retrieves sales analytics for a specific event owned by the authenticated organizer.
 * GET /api/organizer/analytics/sales/events/{eventId}
 * 
 * Enforces server-side ownership. Returns 404 if event is foreign or nonexistent.
 * 
 * @param {string} eventId - Target event UUID
 * @param {Object} [params]
 * @param {string} [params.from] - Optional ISO date filter start
 * @param {string} [params.to] - Optional ISO date filter end
 * @returns {Promise<Object>}
 */
export async function getEventSalesAnalytics(eventId, params = {}) {
  if (!eventId) {
    throw new Error('Event ID is required.');
  }

  const query = new URLSearchParams();
  if (params.from) query.append('from', params.from);
  if (params.to) query.append('to', params.to);

  const queryString = query.toString();
  const endpoint = `/api/organizer/analytics/sales/events/${eventId}${queryString ? `?${queryString}` : ''}`;
  return apiClient.get(endpoint);
}
