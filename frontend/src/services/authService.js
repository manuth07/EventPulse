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
