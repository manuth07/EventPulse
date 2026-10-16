import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import {
  isValidEmail,
  validateForgotPasswordEmail,
} from './forgotPasswordValidation.js';

describe('Forgot Password Email Validation (EP-352)', () => {
  it('returns required error when email is null, undefined, or empty', () => {
    assert.equal(validateForgotPasswordEmail(null), 'Email address is required.');
    assert.equal(validateForgotPasswordEmail(undefined), 'Email address is required.');
    assert.equal(validateForgotPasswordEmail(''), 'Email address is required.');
    assert.equal(validateForgotPasswordEmail('   '), 'Email address is required.');
  });

  it('returns format error when email does not contain @ or domain', () => {
    assert.equal(validateForgotPasswordEmail('notanemail'), 'Enter a valid email address.');
    assert.equal(validateForgotPasswordEmail('user@'), 'Enter a valid email address.');
    assert.equal(validateForgotPasswordEmail('@example.com'), 'Enter a valid email address.');
    assert.equal(validateForgotPasswordEmail('user@domain'), 'Enter a valid email address.');
    assert.equal(validateForgotPasswordEmail('user @domain.com'), 'Enter a valid email address.');
    assert.equal(validateForgotPasswordEmail('user@domain .com'), 'Enter a valid email address.');
  });

  it('returns null when email format is valid', () => {
    assert.equal(validateForgotPasswordEmail('john.doe@example.com'), null);
    assert.equal(validateForgotPasswordEmail('user+tag@domain.co.uk'), null);
    assert.equal(validateForgotPasswordEmail('test.123@sub.domain.org'), null);
    assert.equal(validateForgotPasswordEmail('  user@example.com  '), null);
  });

  it('isValidEmail correctly identifies valid and invalid email strings', () => {
    assert.equal(isValidEmail('valid@example.com'), true);
    assert.equal(isValidEmail('valid.user+reset@domain.lk'), true);
    assert.equal(isValidEmail('invalid-plain-text'), false);
    assert.equal(isValidEmail('invalid@'), false);
    assert.equal(isValidEmail(''), false);
    assert.equal(isValidEmail(null), false);
  });
});
