import { getApiBaseUrl } from './apiConfig';

/**
 * Fetch the customer's current active cart.
 */
export async function getCart(token) {
  const response = await fetch(`${getApiBaseUrl()}/api/cart`, {
    method: 'GET',
    headers: {
      Accept: 'application/json',
      Authorization: `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    const error = new Error(`Failed to load cart (${response.status})`);
    error.status = response.status;
    throw error;
  }

  return response.json();
}

/**
 * Add or update a ticket type in the customer's active cart.
 * By default, quantity sets the desired absolute quantity.
 */
export async function addToCart(eventId, ticketTypeId, quantity, token, clearExisting = false, isDelta = false) {
  const response = await fetch(`${getApiBaseUrl()}/api/cart/items`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Accept: 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ eventId, ticketTypeId, quantity, clearExisting, isDelta }),
  });

  if (!response.ok) {
    let errorMsg = `Failed to add to cart (${response.status})`;
    let conflictData = null;

    try {
      const data = await response.json();
      if (data.message) errorMsg = data.message;
      if (response.status === 409) conflictData = data;
    } catch (e) {
      /* not JSON */
    }

    const error = new Error(errorMsg);
    error.status = response.status;
    error.conflictData = conflictData;
    throw error;
  }

  return response.json();
}

/**
 * Set the exact quantity of a ticket type in the active cart.
 * If quantity <= 0, the item is removed.
 */
export async function updateCartItemQuantity(ticketTypeId, quantity, token) {
  const response = await fetch(`${getApiBaseUrl()}/api/cart/items/${ticketTypeId}`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Accept: 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ quantity }),
  });

  if (!response.ok) {
    let errorMsg = `Failed to update quantity (${response.status})`;
    try {
      const data = await response.json();
      if (data.message) errorMsg = data.message;
    } catch (e) {
      /* not JSON */
    }

    const error = new Error(errorMsg);
    error.status = response.status;
    throw error;
  }

  return response.json();
}

/**
 * Remove an item from the customer's active cart.
 */
export async function removeCartItem(ticketTypeId, token) {
  const response = await fetch(`${getApiBaseUrl()}/api/cart/items/${ticketTypeId}`, {
    method: 'DELETE',
    headers: {
      Accept: 'application/json',
      Authorization: `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    let errorMsg = `Failed to remove item (${response.status})`;
    try {
      const data = await response.json();
      if (data.message) errorMsg = data.message;
    } catch (e) {
      /* not JSON */
    }

    const error = new Error(errorMsg);
    error.status = response.status;
    throw error;
  }

  return response.json();
}

/**
 * Clear the customer's active cart.
 */
export async function clearCart(token) {
  const response = await fetch(`${getApiBaseUrl()}/api/cart`, {
    method: 'DELETE',
    headers: {
      Accept: 'application/json',
      Authorization: `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    const error = new Error(`Failed to clear cart (${response.status})`);
    error.status = response.status;
    throw error;
  }

  return response.json();
}

/**
 * Mark the customer's active cart as completed (called post-checkout).
 */
export async function completeCart(token) {
  const response = await fetch(`${getApiBaseUrl()}/api/cart/complete`, {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      Authorization: `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    const error = new Error(`Failed to complete cart (${response.status})`);
    error.status = response.status;
    throw error;
  }

  return response.json();
}