/**
 * EventPulse Frontend - Centralized API Configuration
 *
 * Production behavior:
 * - If VITE_API_BASE_URL exists and is a valid non-local production URL, use it.
 * - Never silently fall back to localhost in a production build.
 * - If VITE_API_BASE_URL is missing in production, fail clearly rather than contacting localhost.
 *
 * Local development behavior:
 * - Falls back to http://localhost:7000 ONLY when import.meta.env.DEV is true (vite dev).
 * - Localhost is never included as a fallback in production builds.
 */

const rawApiBaseUrl = import.meta.env.VITE_API_BASE_URL;

function resolveApiBaseUrl() {
  // Local development fallback: only available in dev server (import.meta.env.DEV === true)
  if (import.meta.env.DEV) {
    if (rawApiBaseUrl && typeof rawApiBaseUrl === 'string' && rawApiBaseUrl.trim() !== '') {
      return rawApiBaseUrl.trim().replace(/\/+$/, '');
    }
    return 'http://localhost:7000';
  }

  // Production build:
  // Must use VITE_API_BASE_URL. Never allow localhost in a production build.
  if (rawApiBaseUrl && typeof rawApiBaseUrl === 'string' && rawApiBaseUrl.trim() !== '') {
    const trimmed = rawApiBaseUrl.trim().replace(/\/+$/, '');
    if (!trimmed.includes('localhost') && !trimmed.includes('127.0.0.1')) {
      return trimmed;
    }
  }

  // If missing or pointing to localhost in production, do not provide a fallback
  return undefined;
}

export const API_BASE_URL = resolveApiBaseUrl();

/**
 * Validates and returns the backend API base URL.
 * In a production build where VITE_API_BASE_URL is undefined (or invalid),
 * throws an explicit error so calls fail immediately and loudly instead of hitting localhost.
 *
 * @returns {string} The resolved API base URL.
 * @throws {Error} If VITE_API_BASE_URL is missing or invalid in production.
 */
export function getApiBaseUrl() {
  if (API_BASE_URL) {
    return API_BASE_URL;
  }

  throw new Error(
    '[EventPulse Configuration Error] VITE_API_BASE_URL is not configured for this production environment. ' +
    'API calls cannot be dispatched. Please ensure VITE_API_BASE_URL is set in GitHub repository variables.'
  );
}

/**
 * Helper to construct full API endpoint URLs.
 * @param {string} endpoint - Path such as '/api/events'
 * @returns {string} Full URL e.g. 'https://gateway-host/api/events'
 */
export function buildApiUrl(endpoint) {
  const base = getApiBaseUrl();
  const cleanEndpoint = endpoint.startsWith('/') ? endpoint : `/${endpoint}`;
  return `${base}${cleanEndpoint}`;
}
