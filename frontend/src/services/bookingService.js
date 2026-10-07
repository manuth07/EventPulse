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
 * Fetch full booking detail for customer (EP-353 / EP-354).
 * 
 * @param {string} bookingId - The GUID of the booking.
 * @param {string} [token] - Optional JWT authentication token.
 * @returns {Promise<{ id: string, bookingReference: string, customerId: string, eventId: string, eventName?: string, eventDate?: string, eventVenue?: string, status: string, totalAmount: number, createdAt: string, confirmedAt: string|null, expiresAt: string, totalTickets: number, items: Array, tickets: Array }>}
 */
export async function getBookingDetail(bookingId, token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};

  return apiClient.get(`/api/bookings/${bookingId}`, { headers });
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

/**
 * Retrieves the authenticated customer's paginated booking history (US-27).
 * 
 * @param {number} [page=1] - The page number.
 * @param {number} [pageSize=10] - Number of items per page.
 * @param {string} [status] - Optional status filter ('Confirmed', 'PendingPayment', 'Cancelled').
 * @param {string} [token] - Optional JWT authentication token.
 * @returns {Promise<{ items: Array, page: number, pageSize: number, totalCount: number, totalPages: number, hasNextPage: boolean, hasPreviousPage: boolean }>}
 */
export async function getMyBookings(page = 1, pageSize = 10, status, token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};

  const params = new URLSearchParams();
  if (page) params.append('page', page.toString());
  if (pageSize) params.append('pageSize', pageSize.toString());
  if (status && status !== 'All') params.append('status', status);

  const queryString = params.toString() ? `?${params.toString()}` : '';
  return apiClient.get(`/api/bookings/my-bookings${queryString}`, { headers });
}

/**
 * Cancels a booking for the customer (US-28 / EP-308).
 * 
 * @param {string} bookingId - The GUID of the booking.
 * @param {string} [reason] - Optional cancellation reason.
 * @param {string} [token] - Optional JWT authentication token.
 * @returns {Promise<{ bookingId: string, bookingReference: string, previousStatus: string, newStatus: string, cancelledAt: string, success: boolean, message?: string }>}
 */
export async function cancelBooking(bookingId, reason, token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};

  return apiClient.post(`/api/bookings/${bookingId}/cancel`, { reason }, { headers });
}

/**
 * Cancels an individual customer ticket (EP-partial cancellation).
 * 
 * @param {string} ticketId - The GUID of the ticket.
 * @param {string} [reason] - Optional cancellation reason.
 * @param {string} [token] - Optional JWT authentication token.
 * @returns {Promise<{ ticketId: string, ticketCode: string, status: string, parentBookingStatus: string, success: boolean, message?: string }>}
 */
export async function cancelTicket(ticketId, reason, token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};

  return apiClient.post(`/api/tickets/${ticketId}/cancel`, { reason }, { headers });
}

export default {
  getBookingSummary,
  getBookingDetail,
  getBookingTickets,
  getTicketById,
  getMyBookings,
  cancelBooking,
  cancelTicket,
};

