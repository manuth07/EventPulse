import { apiClient, getStoredToken } from './apiClient';

/**
 * Fetch the customer's current active cart.
 */
export async function getCart(token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};
  return apiClient.get('/api/cart', { headers });
}

/**
 * Add or update a ticket type in the customer's active cart.
 * By default, quantity sets the desired absolute quantity.
 */
export async function addToCart(eventId, ticketTypeId, quantity, token, clearExisting = false, isDelta = false) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};
  return apiClient.post('/api/cart/items', { eventId, ticketTypeId, quantity, clearExisting, isDelta }, { headers });
}

/**
 * Set the exact quantity of a ticket type in the active cart.
 * If quantity <= 0, the item is removed.
 */
export async function updateCartItemQuantity(ticketTypeId, quantity, token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};
  return apiClient.put(`/api/cart/items/${ticketTypeId}`, { quantity }, { headers });
}

/**
 * Remove an item from the customer's active cart.
 */
export async function removeCartItem(ticketTypeId, token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};
  return apiClient.delete(`/api/cart/items/${ticketTypeId}`, { headers });
}

/**
 * Clear the customer's active cart.
 */
export async function clearCart(token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};
  return apiClient.delete('/api/cart', { headers });
}

/**
 * Mark the customer's active cart as completed (called post-checkout).
 */
export async function completeCart(token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};
  return apiClient.post('/api/cart/complete', {}, { headers });
}

/**
 * Create a new booking from the cart items.
 */
export async function createBooking(eventId, items, token) {
  const authToken = token || getStoredToken();
  const headers = authToken ? { Authorization: `Bearer ${authToken}` } : {};
  const payload = {
    eventId,
    items: items.map(item => ({
      ticketTypeId: item.ticketTypeId,
      quantity: item.quantity
    }))
  };

  return apiClient.post('/api/bookings', payload, { headers });
}