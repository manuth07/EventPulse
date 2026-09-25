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

export default {
  createCheckoutSession,
};
