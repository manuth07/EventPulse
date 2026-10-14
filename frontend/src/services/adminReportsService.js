import { apiClient } from './apiClient.js';

/**
 * Dynamic Reports API Service (EP-378 / EP-379)
 * 
 * Provides client methods to interact with backend administrative reporting endpoints
 * with secure token injection, role validation, and sanitized error handling.
 */

/**
 * Normalizes API errors to human-readable user messages.
 * 
 * @param {Error|Object} error - Error thrown by apiClient
 * @returns {string} Sanitized user-facing error message
 */
export function formatReportApiError(error) {
  const status = error?.status;
  const errorData = error?.data;

  if (status === 401) {
    return 'Authentication required or your session has expired. Please log in again.';
  }

  if (status === 403) {
    return 'Access denied. You must be a platform Administrator to access dynamic reports.';
  }

  if (errorData?.code === 'INVALID_DATE_RANGE' || error?.message?.includes('earlier than or equal to')) {
    return errorData?.message || 'The start date must be earlier than or equal to the end date.';
  }

  if (errorData?.code === 'DATE_RANGE_EXCEEDED') {
    return errorData?.message || 'The selected date range exceeds the maximum allowed period of 730 days (2 years).';
  }

  if (errorData?.code === 'UNSUPPORTED_REPORT_TYPE') {
    return errorData?.message || 'The selected report type is not supported.';
  }

  if (errorData?.code === 'INVALID_BOOKING_STATUS') {
    return errorData?.message || 'Invalid booking status filter provided.';
  }

  if (errorData?.code === 'INVALID_CURRENCY') {
    return errorData?.message || 'Invalid currency filter provided.';
  }

  if (errorData?.message) {
    return errorData.message;
  }

  if (error?.message) {
    return error.message;
  }

  return 'An error occurred while generating the report. Please verify your filters and try again.';
}

/**
 * Generates dynamic Bookings Report.
 * GET /api/admin/reports/bookings
 * 
 * @param {Object} [params]
 * @param {string} [params.from] - ISO date string start
 * @param {string} [params.to] - ISO date string end
 * @param {string} [params.eventId] - Optional event UUID
 * @param {string} [params.organizerId] - Optional organizer UUID
 * @param {string} [params.bookingStatus] - Optional booking status
 * @returns {Promise<Object>} BookingsReportDataDto
 */
export async function getBookingsReport(params = {}) {
  const query = new URLSearchParams();
  if (params.from) query.append('from', params.from);
  if (params.to) query.append('to', params.to);
  if (params.eventId) query.append('eventId', params.eventId);
  if (params.organizerId) query.append('organizerId', params.organizerId);
  if (params.bookingStatus && params.bookingStatus !== 'All') query.append('bookingStatus', params.bookingStatus);

  const queryString = query.toString();
  const endpoint = `/api/admin/reports/bookings${queryString ? `?${queryString}` : ''}`;
  return apiClient.get(endpoint);
}

/**
 * Generates dynamic Revenue Report.
 * GET /api/admin/reports/revenue
 * 
 * @param {Object} [params]
 * @param {string} [params.from] - ISO date string start
 * @param {string} [params.to] - ISO date string end
 * @param {string} [params.eventId] - Optional event UUID
 * @param {string} [params.organizerId] - Optional organizer UUID
 * @param {string} [params.currency] - Optional currency (USD, LKR)
 * @returns {Promise<Object>} RevenueReportDataDto
 */
export async function getRevenueReport(params = {}) {
  const query = new URLSearchParams();
  if (params.from) query.append('from', params.from);
  if (params.to) query.append('to', params.to);
  if (params.eventId) query.append('eventId', params.eventId);
  if (params.organizerId) query.append('organizerId', params.organizerId);
  if (params.currency && params.currency !== 'ALL') query.append('currency', params.currency);

  const queryString = query.toString();
  const endpoint = `/api/admin/reports/revenue${queryString ? `?${queryString}` : ''}`;
  return apiClient.get(endpoint);
}

/**
 * Generates Platform Activity Summary Report.
 * GET /api/admin/reports/platform-summary
 * 
 * @param {Object} [params]
 * @param {string} [params.from] - ISO date string start
 * @param {string} [params.to] - ISO date string end
 * @param {string} [params.eventId] - Optional event UUID
 * @param {string} [params.organizerId] - Optional organizer UUID
 * @returns {Promise<Object>} PlatformActivityReportDataDto
 */
export async function getPlatformSummaryReport(params = {}) {
  const query = new URLSearchParams();
  if (params.from) query.append('from', params.from);
  if (params.to) query.append('to', params.to);
  if (params.eventId) query.append('eventId', params.eventId);
  if (params.organizerId) query.append('organizerId', params.organizerId);

  const queryString = query.toString();
  const endpoint = `/api/admin/reports/platform-summary${queryString ? `?${queryString}` : ''}`;
  return apiClient.get(endpoint);
}

/**
 * Generic dynamic report generation dispatcher.
 * POST /api/admin/reports/generate
 * 
 * @param {Object} request
 * @param {string} request.reportType - 'platform-summary' | 'bookings' | 'revenue'
 * @param {string} [request.from]
 * @param {string} [request.to]
 * @param {string} [request.eventId]
 * @param {string} [request.organizerId]
 * @param {string} [request.bookingStatus]
 * @param {string} [request.currency]
 * @returns {Promise<Object>} GeneratedReportEnvelopeDto
 */
export async function generateDynamicReport(request) {
  return apiClient.post('/api/admin/reports/generate', request);
}
