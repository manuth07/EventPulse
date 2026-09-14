import { getApiBaseUrl } from './apiConfig';

export async function addToCart(eventId, ticketTypeId, quantity, token) {
  const response = await fetch(`${getApiBaseUrl()}/api/cart/items`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
    body: JSON.stringify({ eventId, ticketTypeId, quantity }),
  });

  if (!response.ok) {
    let errorMsg = `Failed to add to cart (${response.status})`;
    try {
      const data = await response.json();
      if (data.message) errorMsg = data.message;
    } catch (e) { /* not JSON */ }
    const error = new Error(errorMsg);
    error.status = response.status;
    throw error;
  }

  return response.json();
}

export async function getCart(token) {
  const response = await fetch(`${getApiBaseUrl()}/api/cart`, {
    method: 'GET',
    headers: { 'Accept': 'application/json', 'Authorization': `Bearer ${token}` },
  });

  if (!response.ok) {
    const error = new Error(`Failed to load cart (${response.status})`);
    error.status = response.status;
    throw error;
  }

  return response.json();
}