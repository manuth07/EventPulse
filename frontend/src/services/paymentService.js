import { getApiBaseUrl } from './apiConfig';

/**
 * Initiate a Stripe checkout session for a given booking.
 * @param {string} bookingId - The GUID of the pending booking.
 * @param {string} token - The user's JWT access token.
 * @returns {Promise<{ sessionId: string, checkoutUrl: string }>}
 */
export async function createCheckoutSession(bookingId, token) {
  const response = await fetch(`${getApiBaseUrl()}/api/payments/checkout-session`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Accept: 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ bookingId }),
  });

  if (!response.ok) {
    let errorMsg = `Failed to initiate checkout (${response.status})`;
    try {
      const data = await response.json();
      if (data.message) errorMsg = data.message;
    } catch (e) {
      /* non-json response */
    }

    const error = new Error(errorMsg);
    error.status = response.status;
    throw error;
  }

  return response.json();
}
