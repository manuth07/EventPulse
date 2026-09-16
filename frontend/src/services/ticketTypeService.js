import { getApiBaseUrl } from './apiConfig';

export async function getTicketTypes(eventId, token) {
  const response = await fetch(`${getApiBaseUrl()}/api/events/${eventId}/ticket-types`, {
    method: 'GET',
    headers: {
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    let errorMsg = `Failed to load ticket types (${response.status})`;
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

export async function createTicketType(eventId, payload, token) {
  const response = await fetch(`${getApiBaseUrl()}/api/events/${eventId}/ticket-types`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
    body: JSON.stringify(payload),
  });

  if (!response.ok) {
    let errorMsg = `Failed to create ticket type (${response.status})`;
    try {
      const data = await response.json();
      if (data.message) errorMsg = data.message;
      if (data.errors?.length) errorMsg += ': ' + data.errors.join(', ');
    } catch (e) { /* not JSON */ }
    const error = new Error(errorMsg);
    error.status = response.status;
    throw error;
  }

  return response.json();
}
export async function updateTicketType(eventId, ticketTypeId, payload, token) {
  const response = await fetch(`${getApiBaseUrl()}/api/events/${eventId}/ticket-types/${ticketTypeId}`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
    body: JSON.stringify(payload),
  });

  if (!response.ok) {
    let errorMsg = `Failed to update ticket type (${response.status})`;
    try {
      const data = await response.json();
      if (data.message) errorMsg = data.message;
      if (data.errors?.length) errorMsg += ': ' + data.errors.join(', ');
    } catch (e) { /* not JSON */ }
    const error = new Error(errorMsg);
    error.status = response.status;
    throw error;
  }

  return response.json();
}
export async function getPublicTicketTypes(eventId) {
  const response = await fetch(`${getApiBaseUrl()}/api/events/${eventId}/ticket-types/public`, {
    method: 'GET',
    headers: { 'Accept': 'application/json' },
  });

  if (response.status === 404) {
    // Event not found or not published — treat as no ticket info available
    return [];
  }

  if (!response.ok) {
    let errorMsg = `Failed to load ticket information (${response.status})`;
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
export async function deleteTicketType(eventId, ticketTypeId, token) {
  const response = await fetch(`${getApiBaseUrl()}/api/events/${eventId}/ticket-types/${ticketTypeId}`, {
    method: 'DELETE',
    headers: { 'Authorization': `Bearer ${token}` },
  });

  if (!response.ok) {
    let errorMsg = `Failed to delete ticket type (${response.status})`;
    try {
      const data = await response.json();
      if (data.message) errorMsg = data.message;
    } catch (e) { /* not JSON */ }
    const error = new Error(errorMsg);
    error.status = response.status;
    throw error;
  }
}
export function getStartingPrice(ticketTypes) {
  if (!Array.isArray(ticketTypes) || ticketTypes.length === 0) return null;
  const prices = ticketTypes.map((t) => t.price).filter((p) => typeof p === 'number');
  if (prices.length === 0) return null;
  return Math.min(...prices);
}