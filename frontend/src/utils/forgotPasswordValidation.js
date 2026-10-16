/**
 * Validation utilities for Forgot Password / Password Reset request flow.
 */

export const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/**
 * Checks whether an email string matches the valid email format.
 * @param {string} value
 * @returns {boolean}
 */
export function isValidEmail(value) {
  if (!value || typeof value !== 'string') return false;
  return EMAIL_REGEX.test(value.trim());
}

/**
 * Validates the email input for the forgot password form.
 * Returns an error string if invalid, or null if valid.
 * @param {string} email
 * @returns {string|null}
 */
export function validateForgotPasswordEmail(email) {
  if (!email || typeof email !== 'string' || !email.trim()) {
    return 'Email address is required.';
  }

  if (!isValidEmail(email)) {
    return 'Enter a valid email address.';
  }

  return null;
}
