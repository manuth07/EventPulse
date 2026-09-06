const API_BASE_URL = import.meta.env?.VITE_API_BASE_URL || 'http://localhost:7000';

/**
 * Retrieves the current authenticated user's organizer application.
 * Returns null if the user has no application (HTTP 404 APPLICATION_NOT_FOUND).
 * Throws on actual API, authentication, or network failures so they are never
 * misidentified as "no application".
 *
 * @param {string} token - EventPulse JWT
 * @returns {Promise<Object|null>} - OrganizerApplicationDto or null
 */
export async function getMyOrganizerApplication(token) {
  if (!token) {
    const err = new Error('Authentication token is required.');
    err.status = 401;
    err.code = 'UNAUTHORIZED';
    throw err;
  }

  const response = await fetch(`${API_BASE_URL}/api/organizer-applications/me`, {
    method: 'GET',
    headers: {
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
  });

  if (response.status === 404) {
    // 404 is the definitive signal that user has not applied yet
    return null;
  }

  if (response.status === 401) {
    const err = new Error('Your session has expired. Please sign in again.');
    err.status = 401;
    err.code = 'UNAUTHORIZED';
    throw err;
  }

  if (response.status === 403) {
    const err = new Error('You do not have permission to view organizer applications.');
    err.status = 403;
    err.code = 'FORBIDDEN';
    throw err;
  }

  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    const err = new Error(body.message || `Failed to retrieve application status (${response.status})`);
    err.status = response.status;
    err.code = body.code || 'API_ERROR';
    throw err;
  }

  return response.json();
}

/**
 * Submits a new organizer application for the current user.
 *
 * @param {Object} payload - { organizerName, organizerType, contactNumber, description, website }
 * @param {string} token - EventPulse JWT
 * @returns {Promise<Object>} - Newly created OrganizerApplicationDto
 */
export async function submitOrganizerApplication(payload, token) {
  if (!token) {
    const err = new Error('Authentication token is required.');
    err.status = 401;
    err.code = 'UNAUTHORIZED';
    throw err;
  }

  const response = await fetch(`${API_BASE_URL}/api/organizer-applications`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
    body: JSON.stringify(payload),
  });

  if (response.status === 201) {
    return response.json();
  }

  const body = await response.json().catch(() => ({}));

  if (response.status === 400) {
    const err = new Error(body.message || 'Validation failed. Please check your inputs.');
    err.status = 400;
    err.code = body.code || 'VALIDATION_ERROR';
    err.errors = body.errors || [];
    throw err;
  }

  if (response.status === 401) {
    const err = new Error('Your session has expired. Please sign in again.');
    err.status = 401;
    err.code = 'UNAUTHORIZED';
    throw err;
  }

  if (response.status === 403) {
    const err = new Error('You do not have permission to submit an organizer application.');
    err.status = 403;
    err.code = 'FORBIDDEN';
    throw err;
  }

  if (response.status === 409) {
    const err = new Error(body.message || 'An application conflict occurred.');
    err.status = 409;
    err.code = body.code || 'APPLICATION_CONFLICT';
    throw err;
  }

  const err = new Error(body.message || 'Failed to submit organizer application. Please try again later.');
  err.status = response.status;
  err.code = body.code || 'SERVER_ERROR';
  throw err;
}
