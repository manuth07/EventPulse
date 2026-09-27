import { apiClient, getStoredToken } from './apiClient';

/**
 * Fetch authoritative booking summary and status for customer.
 * 
 * @param {string} bookingId - The GUID of the booking.
 * @param {string} [token] - Optional JWT authentication token.
 * @returns {Promise<{ id: string, bookingReference: string, customerId: string, totalAmount: number, status: string }>}
 */
export async function getBookingSummary(bookingId, token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};

  return apiClient.get(`/api/bookings/${bookingId}/summary`, { headers });
}

export default {
  getBookingSummary,
};
