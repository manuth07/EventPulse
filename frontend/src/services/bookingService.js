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

/**
 * Fetch customer tickets for an authoritative booking.
 * 
 * @param {string} bookingId - The GUID of the booking.
 * @param {string} [token] - Optional JWT authentication token.
 * @returns {Promise<{ bookingId: string, bookingReference: string, eventId: string, bookingStatus: string, totalAmount: number, createdAt: string, confirmedAt: string|null, tickets: Array }>}
 */
export async function getBookingTickets(bookingId, token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};

  return apiClient.get(`/api/bookings/${bookingId}/tickets`, { headers });
}

/**
 * Fetch an individual customer ticket by ticketId.
 * 
 * @param {string} ticketId - The GUID of the ticket.
 * @param {string} [token] - Optional JWT authentication token.
 */
export async function getTicketById(ticketId, token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};

  return apiClient.get(`/api/tickets/${ticketId}`, { headers });
}

export default {
  getBookingSummary,
  getBookingTickets,
  getTicketById,
};
