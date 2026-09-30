/**
 * Centralized API Client with automatic JWT Bearer token injection.
 * 
 * Intercepts all outgoing HTTP requests to the YARP Gateway (:7000):
 * - Automatically attaches Authorization: Bearer <token> from sessionStorage
 * - Resolves base URL via apiConfig.js
 * - Handles JSON serialization and parsing
 * - Normalizes error responses for consistent UI handling
 */

import { getApiBaseUrl } from './apiConfig';

const TOKEN_KEY = 'ep_access_token';

/**
 * Retrieves the current authentication token from storage.
 * @returns {string|null}
 */
export function getStoredToken() {
  return sessionStorage.getItem(TOKEN_KEY) || localStorage.getItem(TOKEN_KEY) || null;
}

/**
 * Core request dispatcher.
 * @param {string} endpoint - Relative API endpoint (e.g., '/api/cart')
 * @param {RequestInit} [options={}] - Standard fetch RequestInit options
 * @returns {Promise<any>}
 */
export async function apiRequest(endpoint, options = {}) {
  const baseUrl = getApiBaseUrl();
  const cleanEndpoint = endpoint.startsWith('/') ? endpoint : `/${endpoint}`;
  const url = `${baseUrl}${cleanEndpoint}`;

  const headers = new Headers(options.headers || {});

  // Set default Accept header
  if (!headers.has('Accept')) {
    headers.set('Accept', 'application/json');
  }

  // Set default Content-Type for requests with body (unless already set or FormData)
  if (options.body && !(options.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }

  // Attach JWT Bearer token if present and not explicitly overridden
  if (!headers.has('Authorization')) {
    const token = getStoredToken();
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }
  }

  const fetchOptions = {
    ...options,
    headers,
  };

  const response = await fetch(url, fetchOptions);

  if (!response.ok) {
    let errorData = null;
    let errorMessage = `Request failed with status ${response.status}`;

    try {
      errorData = await response.json();
      if (errorData?.message) {
        errorMessage = errorData.message;
      } else if (errorData?.error) {
        errorMessage = errorData.error;
      }
    } catch {
      // Non-JSON response body
    }

    const error = new Error(errorMessage);
    error.status = response.status;
    error.data = errorData;

    if (response.status === 401) {
      console.warn(`[apiClient] 401 Unauthorized encountered on ${url}. Session may be expired.`);
    }

    throw error;
  }

  // If response is 204 No Content or empty
  if (response.status === 204) {
    return null;
  }

  const contentType = response.headers.get('content-type');
  if (contentType && contentType.includes('application/json')) {
    return response.json();
  }

  return response.text();
}

/**
 * Standardized HTTP Methods
 */
export const apiClient = {
  get: (endpoint, options = {}) => apiRequest(endpoint, { ...options, method: 'GET' }),
  post: (endpoint, body, options = {}) =>
    apiRequest(endpoint, {
      ...options,
      method: 'POST',
      body: body instanceof FormData ? body : JSON.stringify(body),
    }),
  put: (endpoint, body, options = {}) =>
    apiRequest(endpoint, {
      ...options,
      method: 'PUT',
      body: body instanceof FormData ? body : JSON.stringify(body),
    }),
  delete: (endpoint, options = {}) => apiRequest(endpoint, { ...options, method: 'DELETE' }),
};

export default apiClient;
