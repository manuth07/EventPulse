import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import {
  checkPasswordRules,
  validateResetPasswordForm,
} from './resetPasswordValidation.js';

describe('Reset Password Form Validation (EP-352 Part 2)', () => {
  it('correctly tracks password rule checks', () => {
    const empty = checkPasswordRules('');
    assert.equal(empty.minLength, false);
    assert.equal(empty.uppercase, false);
    assert.equal(empty.lowercase, false);
    assert.equal(empty.number, false);
    assert.equal(empty.allMet, false);

    const partial = checkPasswordRules('Pass');
    assert.equal(partial.minLength, false);
    assert.equal(partial.uppercase, true);
    assert.equal(partial.lowercase, true);
    assert.equal(partial.number, false);
    assert.equal(partial.allMet, false);

    const valid = checkPasswordRules('Secure123');
    assert.equal(valid.minLength, true);
    assert.equal(valid.uppercase, true);
    assert.equal(valid.lowercase, true);
    assert.equal(valid.number, true);
    assert.equal(valid.allMet, true);
  });

  it('reports missing password error', () => {
    const { errors, isValid } = validateResetPasswordForm('', '');
    assert.equal(isValid, false);
    assert.equal(errors.password, 'Password is required.');
    assert.equal(errors.confirmPassword, 'Please confirm your password.');
  });

  it('reports minimum length error', () => {
    const { errors, isValid } = validateResetPasswordForm('Abc1', 'Abc1');
    assert.equal(isValid, false);
    assert.equal(errors.password, 'Password must be at least 8 characters.');
  });

  it('reports missing uppercase error', () => {
    const { errors, isValid } = validateResetPasswordForm('lowercase123', 'lowercase123');
    assert.equal(isValid, false);
    assert.equal(errors.password, 'Password must contain at least one uppercase letter.');
  });

  it('reports missing lowercase error', () => {
    const { errors, isValid } = validateResetPasswordForm('UPPERCASE123', 'UPPERCASE123');
    assert.equal(isValid, false);
    assert.equal(errors.password, 'Password must contain at least one lowercase letter.');
  });

  it('reports missing number error', () => {
    const { errors, isValid } = validateResetPasswordForm('NoDigitsHere!', 'NoDigitsHere!');
    assert.equal(isValid, false);
    assert.equal(errors.password, 'Password must contain at least one number.');
  });

  it('reports password mismatch error', () => {
    const { errors, isValid } = validateResetPasswordForm('Secure123!', 'Different123!');
    assert.equal(isValid, false);
    assert.equal(errors.password, undefined);
    assert.equal(errors.confirmPassword, 'Passwords do not match.');
  });

  it('validates a completely valid matching password pair', () => {
    const { errors, isValid } = validateResetPasswordForm('StrongPass123', 'StrongPass123');
    assert.equal(isValid, true);
    assert.deepEqual(errors, {});
  });
});
