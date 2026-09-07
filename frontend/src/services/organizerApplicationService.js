import { API_BASE_URL, getApiBaseUrl } from './apiConfig';

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

  const response = await fetch(`${getApiBaseUrl()}/api/organizer-applications/me`, {
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

  const response = await fetch(`${getApiBaseUrl()}/api/organizer-applications`, {
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

/**
 * Resubmits a previously rejected organizer application.
 *
 * @param {Object} payload - { organizerName, organizerType, contactNumber, description, website }
 * @param {string} token - EventPulse JWT
 * @returns {Promise<Object>} - Updated OrganizerApplicationDto
 */
export async function resubmitOrganizerApplication(payload, token) {
  if (!token) {
    const err = new Error('Authentication token is required.');
    err.status = 401;
    err.code = 'UNAUTHORIZED';
    throw err;
  }

  const response = await fetch(`${getApiBaseUrl()}/api/organizer-applications/me/resubmit`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
    body: JSON.stringify(payload),
  });

  if (response.ok) {
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
    const err = new Error('You do not have permission to resubmit an application.');
    err.status = 403;
    err.code = 'FORBIDDEN';
    throw err;
  }

  if (response.status === 409) {
    const err = new Error(body.message || 'Only rejected applications can be resubmitted.');
    err.status = 409;
    err.code = body.code || 'APPLICATION_CONFLICT';
    throw err;
  }

  const err = new Error(body.message || 'Failed to resubmit application. Please try again later.');
  err.status = response.status;
  err.code = body.code || 'SERVER_ERROR';
  throw err;
}

/**
 * Retrieves organizer applications for review by an Administrator.
 *
 * @param {string|null} status - 'Pending' | 'Approved' | 'Rejected' | null
 * @param {string} token - EventPulse Administrator JWT
 * @returns {Promise<Array<Object>>} - List of AdminOrganizerApplicationDto
 */
export async function getAdminOrganizerApplications(status, token) {
  if (!token) {
    const err = new Error('Authentication token is required.');
    err.status = 401;
    err.code = 'UNAUTHORIZED';
    throw err;
  }

  const query = status ? `?status=${encodeURIComponent(status)}` : '';
  const response = await fetch(`${getApiBaseUrl()}/api/admin/organizer-applications${query}`, {
    method: 'GET',
    headers: {
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
  });

  if (response.status === 401) {
    const err = new Error('Your session has expired. Please sign in again.');
    err.status = 401;
    err.code = 'UNAUTHORIZED';
    throw err;
  }

  if (response.status === 403) {
    const err = new Error('Administrator privileges required.');
    err.status = 403;
    err.code = 'FORBIDDEN';
    throw err;
  }

  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    const err = new Error(body.message || `Failed to retrieve organizer applications (${response.status})`);
    err.status = response.status;
    err.code = body.code || 'API_ERROR';
    throw err;
  }

  return response.json();
}

/**
 * Retrieves a single organizer application for review by an Administrator.
 *
 * @param {string} id - Application GUID
 * @param {string} token - EventPulse Administrator JWT
 * @returns {Promise<Object>} - AdminOrganizerApplicationDto
 */
export async function getAdminOrganizerApplicationById(id, token) {
  if (!token) {
    const err = new Error('Authentication token is required.');
    err.status = 401;
    err.code = 'UNAUTHORIZED';
    throw err;
  }

  const response = await fetch(`${getApiBaseUrl()}/api/admin/organizer-applications/${id}`, {
    method: 'GET',
    headers: {
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
  });

  if (response.status === 404) {
    const err = new Error('Organizer application not found.');
    err.status = 404;
    err.code = 'APPLICATION_NOT_FOUND';
    throw err;
  }

  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    const err = new Error(body.message || `Failed to retrieve application details (${response.status})`);
    err.status = response.status;
    throw err;
  }

  return response.json();
}

/**
 * Approves a pending organizer application and grants the Organizer role.
 *
 * @param {string} id - Application GUID
 * @param {Object} payload - { reviewComment }
 * @param {string} token - EventPulse Administrator JWT
 * @returns {Promise<Object>} - AdminOrganizerApplicationDto
 */
export async function approveOrganizerApplication(id, payload, token) {
  if (!token) {
    const err = new Error('Authentication token is required.');
    err.status = 401;
    err.code = 'UNAUTHORIZED';
    throw err;
  }

  const response = await fetch(`${getApiBaseUrl()}/api/admin/organizer-applications/${id}/approve`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
    body: JSON.stringify(payload || {}),
  });

  if (response.ok) {
    return response.json();
  }

  const body = await response.json().catch(() => ({}));
  const err = new Error(body.message || `Failed to approve application (${response.status})`);
  err.status = response.status;
  err.code = body.code || 'APPROVAL_ERROR';
  throw err;
}

/**
 * Rejects a pending organizer application with mandatory review feedback.
 *
 * @param {string} id - Application GUID
 * @param {Object} payload - { reviewComment }
 * @param {string} token - EventPulse Administrator JWT
 * @returns {Promise<Object>} - AdminOrganizerApplicationDto
 */
export async function rejectOrganizerApplication(id, payload, token) {
  if (!token) {
    const err = new Error('Authentication token is required.');
    err.status = 401;
    err.code = 'UNAUTHORIZED';
    throw err;
  }

  const response = await fetch(`${getApiBaseUrl()}/api/admin/organizer-applications/${id}/reject`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
    body: JSON.stringify(payload),
  });

  if (response.ok) {
    return response.json();
  }

  const body = await response.json().catch(() => ({}));
  const err = new Error(body.message || `Failed to reject application (${response.status})`);
  err.status = response.status;
  err.code = body.code || 'REJECTION_ERROR';
  err.errors = body.errors || [];
  throw err;
}
