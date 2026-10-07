/**
 * Validation utilities for Reset Password form (EP-352 Part 2).
 * Aligns client-side UX checks with Identity Service password policy.
 */

export function checkPasswordRules(password = '') {
  const p = typeof password === 'string' ? password : '';
  const minLength = p.length >= 8;
  const uppercase = /[A-Z]/.test(p);
  const lowercase = /[a-z]/.test(p);
  const number = /[0-9]/.test(p);

  return {
    minLength,
    uppercase,
    lowercase,
    number,
    allMet: minLength && uppercase && lowercase && number,
  };
}

export function validateResetPasswordForm(password = '', confirmPassword = '') {
  const errors = {};

  if (!password) {
    errors.password = 'Password is required.';
  } else if (password.length < 8) {
    errors.password = 'Password must be at least 8 characters.';
  } else if (!/[A-Z]/.test(password)) {
    errors.password = 'Password must contain at least one uppercase letter.';
  } else if (!/[a-z]/.test(password)) {
    errors.password = 'Password must contain at least one lowercase letter.';
  } else if (!/[0-9]/.test(password)) {
    errors.password = 'Password must contain at least one number.';
  }

  if (!confirmPassword) {
    errors.confirmPassword = 'Please confirm your password.';
  } else if (password && password !== confirmPassword) {
    errors.confirmPassword = 'Passwords do not match.';
  }

  return {
    errors,
    isValid: Object.keys(errors).length === 0,
  };
}
