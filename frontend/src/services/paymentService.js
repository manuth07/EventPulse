import { apiClient, getStoredToken } from './apiClient';

/**
 * Creates a Stripe Checkout Session for a pending booking.
 * 
 * @param {string} bookingId - The GUID of the booking awaiting payment.
 * @param {string} [token] - Optional JWT authentication token.
 * @returns {Promise<{ paymentId: string, bookingId: string, bookingReference: string, sessionId: string, checkoutUrl: string }>}
 */
export async function createCheckoutSession(bookingId, token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};

  return apiClient.post(
    '/api/payments/checkout-session',
    { bookingId },
    { headers }
  );
}

/**
 * Retrieves the authenticated customer's paginated payment history (EP-358 / EP-54).
 * 
 * @param {object} [params] - Optional query parameters: { page, pageSize, status }
 * @param {string} [token] - Optional JWT authentication token.
 * @returns {Promise<{ items: Array, totalCount: number, page: number, pageSize: number, totalPages: number, hasNextPage: boolean, hasPreviousPage: boolean }>}
 */
export async function getMyPayments(params = {}, token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};

  const queryParams = new URLSearchParams();
  if (params.page) queryParams.set('page', String(params.page));
  if (params.pageSize) queryParams.set('pageSize', String(params.pageSize));
  if (params.status && params.status !== 'All') queryParams.set('status', params.status);

  const queryString = queryParams.toString();
  const endpoint = `/api/payments/my-payments${queryString ? `?${queryString}` : ''}`;

  return apiClient.get(endpoint, { headers });
}

/**
 * Retrieves a single payment detail record for the authenticated owner (EP-356 / EP-54).
 * 
 * @param {string} paymentId - GUID of the payment transaction.
 * @param {string} [token] - Optional JWT authentication token.
 * @returns {Promise<object>}
 */
export async function getPaymentById(paymentId, token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};

  return apiClient.get(`/api/payments/${paymentId}`, { headers });
}

export default {
  createCheckoutSession,
  getMyPayments,
  getPaymentById,
};
