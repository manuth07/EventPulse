const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:7000';

/**
 * Register a new customer account via the API Gateway.
 * @param {Object} data - { firstName, lastName, phoneNumber, countryCode, email, password }
 * @returns {Promise<{id, email, emailConfirmed, verificationRequired}>}
 * @throws Error with .status and .errors properties on failure
 */
export async function registerCustomer(data) {
  const response = await fetch(`${API_BASE_URL}/api/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
    body: JSON.stringify(data),
  });

  if (response.status === 409) {
    const err = new Error('An account with this email already exists.');
    err.status = 409;
    throw err;
  }

  if (response.status === 400) {
    const body = await response.json().catch(() => ({}));
    const err = new Error(body.message || 'Validation failed.');
    err.status = 400;
    err.errors = body.errors || [];
    throw err;
  }

  if (!response.ok) {
    const err = new Error('We couldn\'t create your account right now. Please try again.');
    err.status = response.status;
    throw err;
  }

  return response.json();
}

/**
 * Authenticates a user with email and password via API Gateway.
 * @param {Object} credentials - { email, password }
 * @returns {Promise<{accessToken, tokenType, expiresIn, user}>}
 */
export async function login(credentials) {
  const response = await fetch(`${API_BASE_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
    body: JSON.stringify(credentials),
  });

  if (response.status === 401) {
    const err = new Error('Invalid email or password.');
    err.status = 401;
    err.code = 'INVALID_CREDENTIALS';
    throw err;
  }

  if (response.status === 403) {
    const body = await response.json().catch(() => ({}));
    const err = new Error(body.message || 'Access forbidden.');
    err.status = 403;
    err.code = body.code || 'FORBIDDEN';
    throw err;
  }

  if (response.status === 400) {
    const body = await response.json().catch(() => ({}));
    const err = new Error(body.message || 'Validation failed.');
    err.status = 400;
    err.code = 'INVALID_REQUEST';
    err.errors = body.errors || [];
    throw err;
  }

  if (!response.ok) {
    const err = new Error('We couldn\'t sign you in right now. Please try again.');
    err.status = response.status;
    throw err;
  }

  return response.json();
}

/**
 * Authenticates a user via Google Identity Services ID token.
 * On 409 ACCOUNT_LINK_REQUIRED, throws with code for the link flow.
 * @param {string} credential - Google ID token (credential) from GIS
 * @returns {Promise<{accessToken, tokenType, expiresIn, user}>}
 */
export async function googleLogin(credential) {
  const response = await fetch(`${API_BASE_URL}/api/auth/google`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
    body: JSON.stringify({ credential }),
  });

  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    const err = new Error(body.message || 'Google sign-in failed. Please try again.');
    err.status = response.status;
    err.code = body.code || 'GOOGLE_AUTH_FAILED';
    throw err;
  }

  return response.json();
}

/**
 * Links an existing EventPulse password account to Google.
 * Requires both the Google credential AND the existing EventPulse password.
 * @param {Object} data - { credential, password }
 * @returns {Promise<{accessToken, tokenType, expiresIn, user}>}
 */
export async function googleLinkExisting(data) {
  const response = await fetch(`${API_BASE_URL}/api/auth/google/link-existing`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    const err = new Error(body.message || 'Account linking failed. Please try again.');
    err.status = response.status;
    err.code = body.code || 'LINK_FAILED';
    throw err;
  }

  return response.json();
}

/**
 * Completes the profile for a Google-created user (phone + country).
 * Requires a valid Bearer token.
 * @param {Object} data - { phoneNumber, countryCode }
 * @param {string} accessToken
 * @returns {Promise<Object>}
 */
export async function completeProfile(data, accessToken) {
  const response = await fetch(`${API_BASE_URL}/api/users/me/profile`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'Authorization': `Bearer ${accessToken}`,
    },
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    const err = new Error(body.message || 'Profile update failed. Please try again.');
    err.status = response.status;
    err.code = body.code || 'PROFILE_ERROR';
    err.errors = body.errors || [];
    throw err;
  }

  return response.json();
}

/**
 * Verifies a customer's email using a 6-digit OTP.
 * @param {string} email 
 * @param {string} code 
 * @returns {Promise<Object>}
 */
export async function verifyEmail(email, code) {
  const response = await fetch(`${API_BASE_URL}/api/auth/verify-email`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
    body: JSON.stringify({ email, code }),
  });

  if (response.status === 400 || response.status === 429) {
    const body = await response.json().catch(() => ({}));
    const err = new Error(body.message || 'Verification failed.');
    err.status = response.status;
    err.errors = body.errors || [];
    throw err;
  }

  if (!response.ok) {
    const err = new Error('We couldn\'t verify your email right now. Please try again.');
    err.status = response.status;
    throw err;
  }

  return response.json();
}

/**
 * Resends the verification email.
 * @param {string} email 
 * @returns {Promise<Object>}
 */
export async function resendVerification(email) {
  const response = await fetch(`${API_BASE_URL}/api/auth/resend-verification`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
    body: JSON.stringify({ email }),
  });

  if (response.status === 400 || response.status === 429) {
    const body = await response.json().catch(() => ({}));
    const err = new Error(body.message || 'Resend failed.');
    err.status = response.status;
    err.errors = body.errors || [];
    throw err;
  }

  if (!response.ok) {
    const err = new Error('We couldn\'t resend your email right now. Please try again.');
    err.status = response.status;
    throw err;
  }

  return response.json();
}
