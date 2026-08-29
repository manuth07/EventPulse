import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Eye, EyeOff } from 'lucide-react';
import { registerCustomer } from '../../services/authService';
import { COUNTRIES, DIAL_CODES } from '../../data/countries';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const isValidEmail = (v) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v.trim());

const INITIAL = {
  firstName: '',
  lastName: '',
  phoneNumber: '',
  countryCode: 'LK',
  email: '',
  password: '',
  confirmPassword: '',
};

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function Register() {
  const navigate = useNavigate();

  const [form, setForm] = useState(INITIAL);
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [fieldErrors, setFieldErrors] = useState({});
  const [formError, setFormError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  // -------------------------------------------------------------------------
  // Field helpers
  // -------------------------------------------------------------------------

  const set = (field) => (e) => {
    setForm((prev) => ({ ...prev, [field]: e.target.value }));
    // Clear field error on change
    if (fieldErrors[field]) {
      setFieldErrors((prev) => { const next = { ...prev }; delete next[field]; return next; });
    }
    setFormError('');
  };

  const dialCode = DIAL_CODES[form.countryCode] || '';

  // -------------------------------------------------------------------------
  // Client-side validation
  // -------------------------------------------------------------------------

  function validate() {
    const errors = {};
    if (!form.firstName.trim()) errors.firstName = 'First name is required.';
    else if (form.firstName.trim().length > 100) errors.firstName = 'First name cannot exceed 100 characters.';

    if (!form.lastName.trim()) errors.lastName = 'Last name is required.';
    else if (form.lastName.trim().length > 100) errors.lastName = 'Last name cannot exceed 100 characters.';

    if (!form.phoneNumber.trim()) errors.phoneNumber = 'Contact number is required.';

    if (!form.countryCode) errors.countryCode = 'Country is required.';

    if (!form.email.trim()) errors.email = 'Email address is required.';
    else if (!isValidEmail(form.email)) errors.email = 'Enter a valid email address.';

    if (!form.password) errors.password = 'Password is required.';
    else if (form.password.length < 8) errors.password = 'Password must be at least 8 characters.';
    else if (!/[A-Z]/.test(form.password)) errors.password = 'Password must contain at least one uppercase letter.';
    else if (!/[a-z]/.test(form.password)) errors.password = 'Password must contain at least one lowercase letter.';
    else if (!/[0-9]/.test(form.password)) errors.password = 'Password must contain at least one digit.';

    if (!form.confirmPassword) errors.confirmPassword = 'Please confirm your password.';
    else if (form.password !== form.confirmPassword) errors.confirmPassword = 'Passwords do not match.';

    return errors;
  }

  // -------------------------------------------------------------------------
  // Submit
  // -------------------------------------------------------------------------

  async function handleSubmit(e) {
    e.preventDefault();
    setFormError('');

    const errors = validate();
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }

    // Build normalized phone: dialCode + local number (digits only)
    const localDigits = form.phoneNumber.replace(/\D/g, '');
    const normalizedPhone = `${dialCode}${localDigits}`;

    setIsSubmitting(true);
    try {
      const result = await registerCustomer({
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        phoneNumber: normalizedPhone,
        countryCode: form.countryCode,
        email: form.email.trim(),
        password: form.password,
      });

      // Phase 3 readiness: navigate to verify-email with registered email in state
      navigate('/verify-email', { state: { email: result.email }, replace: true });
    } catch (err) {
      if (err.status === 409) {
        setFieldErrors((prev) => ({ ...prev, email: 'An account with this email already exists.' }));
      } else if (err.status === 400 && err.errors?.length > 0) {
        setFormError(err.errors.join(' '));
      } else {
        setFormError(err.message || "We couldn't create your account right now. Please try again.");
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  // -------------------------------------------------------------------------
  // Render
  // -------------------------------------------------------------------------

  return (
    <div style={{
      minHeight: '100vh',
      backgroundColor: 'var(--ep-canvas)',
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      justifyContent: 'flex-start',
      padding: '40px 16px 64px',
    }}>
      {/* Brand */}
      <Link to="/" style={{ textDecoration: 'none', marginBottom: '32px' }}>
        <span className="ep-brand">
          Event<span style={{ color: 'var(--ep-primary)' }}>Pulse</span>
        </span>
      </Link>

      {/* Registration Card */}
      <div style={{
        width: '100%',
        maxWidth: '500px',
        backgroundColor: '#ffffff',
        borderRadius: 'var(--ep-radius-card)',
        border: '1px solid var(--ep-border)',
        boxShadow: '0 2px 8px rgba(0,0,0,0.04)',
        padding: '36px 32px',
      }}>
        {/* Heading */}
        <div style={{ marginBottom: '24px' }}>
          <h1 className="ep-h2" style={{ marginBottom: '6px' }}>Create your account</h1>
          <p className="ep-body" style={{ margin: 0 }}>
            Discover events worth remembering.
          </p>
        </div>

        {/* Form-level error */}
        {formError && (
          <div style={{
            backgroundColor: '#FFF0EF',
            border: '1px solid #FFCDD2',
            borderRadius: '10px',
            padding: '12px 14px',
            marginBottom: '20px',
            fontSize: '13px',
            color: 'var(--ep-danger)',
          }}>
            {formError}
          </div>
        )}

        <form onSubmit={handleSubmit} noValidate>
          {/* Row: First name / Last name */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '14px' }}>
            <Field label="First name" id="reg-firstName" error={fieldErrors.firstName}>
              <input
                id="reg-firstName"
                type="text"
                className="ep-input"
                style={inputStyle(fieldErrors.firstName)}
                value={form.firstName}
                onChange={set('firstName')}
                autoComplete="given-name"
                maxLength={100}
                placeholder="John"
              />
            </Field>

            <Field label="Last name" id="reg-lastName" error={fieldErrors.lastName}>
              <input
                id="reg-lastName"
                type="text"
                className="ep-input"
                style={inputStyle(fieldErrors.lastName)}
                value={form.lastName}
                onChange={set('lastName')}
                autoComplete="family-name"
                maxLength={100}
                placeholder="Silva"
              />
            </Field>
          </div>

          {/* Country */}
          <Field label="Country" id="reg-country" error={fieldErrors.countryCode} style={{ marginBottom: '14px' }}>
            <select
              id="reg-country"
              className="ep-input"
              style={{ ...inputStyle(fieldErrors.countryCode), cursor: 'pointer' }}
              value={form.countryCode}
              onChange={(e) => {
                setForm((prev) => ({ ...prev, countryCode: e.target.value }));
                setFieldErrors((prev) => { const n = { ...prev }; delete n.countryCode; return n; });
              }}
            >
              <option value="">Select country</option>
              {COUNTRIES.map((c) => (
                <option key={c.code} value={c.code}>{c.name}</option>
              ))}
            </select>
          </Field>

          {/* Contact number */}
          <Field label="Contact number" id="reg-phone" error={fieldErrors.phoneNumber} style={{ marginBottom: '14px' }}>
            <div style={{ display: 'flex', gap: '8px' }}>
              {/* Dial code badge */}
              <div style={{
                display: 'flex',
                alignItems: 'center',
                padding: '0 14px',
                borderRadius: '10px',
                border: `1px solid ${fieldErrors.phoneNumber ? 'var(--ep-danger)' : 'var(--ep-border)'}`,
                backgroundColor: 'var(--ep-canvas)',
                fontSize: '14px',
                fontWeight: 500,
                color: 'var(--ep-text-primary)',
                whiteSpace: 'nowrap',
                minWidth: '56px',
                justifyContent: 'center',
              }}>
                {dialCode || '—'}
              </div>
              <input
                id="reg-phone"
                type="tel"
                className="ep-input"
                style={{ ...inputStyle(fieldErrors.phoneNumber), flex: 1 }}
                value={form.phoneNumber}
                onChange={set('phoneNumber')}
                autoComplete="tel-national"
                placeholder="77 123 4567"
              />
            </div>
          </Field>

          {/* Email */}
          <Field label="Email address" id="reg-email" error={fieldErrors.email} style={{ marginBottom: '14px' }}>
            <input
              id="reg-email"
              type="email"
              className="ep-input"
              style={inputStyle(fieldErrors.email)}
              value={form.email}
              onChange={set('email')}
              autoComplete="email"
              placeholder="john@example.com"
            />
          </Field>

          {/* Password */}
          <Field label="Password" id="reg-password" error={fieldErrors.password} style={{ marginBottom: '14px' }}>
            <PasswordInput
              id="reg-password"
              value={form.password}
              onChange={set('password')}
              show={showPassword}
              onToggle={() => setShowPassword((v) => !v)}
              error={fieldErrors.password}
              autoComplete="new-password"
              placeholder="At least 8 characters"
            />
          </Field>

          {/* Confirm password */}
          <Field label="Confirm password" id="reg-confirm" error={fieldErrors.confirmPassword} style={{ marginBottom: '24px' }}>
            <PasswordInput
              id="reg-confirm"
              value={form.confirmPassword}
              onChange={set('confirmPassword')}
              show={showConfirm}
              onToggle={() => setShowConfirm((v) => !v)}
              error={fieldErrors.confirmPassword}
              autoComplete="new-password"
              placeholder="Repeat your password"
            />
          </Field>

          {/* Submit */}
          <button
            type="submit"
            className="ep-btn-primary"
            style={{ width: '100%', height: '46px', fontSize: '15px' }}
            disabled={isSubmitting}
          >
            {isSubmitting ? 'Creating account…' : 'Create Account'}
          </button>
        </form>

        {/* Sign in link */}
        <p style={{ textAlign: 'center', marginTop: '20px', marginBottom: 0, fontSize: '13px', color: 'var(--ep-text-secondary)' }}>
          Already have an account?{' '}
          <Link to="/login" style={{ color: 'var(--ep-primary)', fontWeight: 500, textDecoration: 'none' }}>
            Sign in
          </Link>
        </p>
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Sub-components
// ---------------------------------------------------------------------------

function Field({ label, id, error, children, style }) {
  return (
    <div style={style}>
      <label
        htmlFor={id}
        style={{
          display: 'block',
          fontSize: '13px',
          fontWeight: 500,
          color: 'var(--ep-text-primary)',
          marginBottom: '6px',
        }}
      >
        {label}
      </label>
      {children}
      {error && (
        <p style={{ margin: '5px 0 0', fontSize: '12px', color: 'var(--ep-danger)' }}>
          {error}
        </p>
      )}
    </div>
  );
}

function PasswordInput({ id, value, onChange, show, onToggle, error, autoComplete, placeholder }) {
  return (
    <div style={{ position: 'relative' }}>
      <input
        id={id}
        type={show ? 'text' : 'password'}
        className="ep-input"
        style={{ ...inputStyle(error), paddingRight: '44px' }}
        value={value}
        onChange={onChange}
        autoComplete={autoComplete}
        placeholder={placeholder}
      />
      <button
        type="button"
        aria-label={show ? 'Hide password' : 'Show password'}
        onClick={onToggle}
        style={{
          position: 'absolute',
          right: '12px',
          top: '50%',
          transform: 'translateY(-50%)',
          background: 'none',
          border: 'none',
          cursor: 'pointer',
          padding: '4px',
          color: 'var(--ep-text-secondary)',
          display: 'flex',
          alignItems: 'center',
        }}
      >
        {show ? <EyeOff size={16} /> : <Eye size={16} />}
      </button>
    </div>
  );
}

function inputStyle(error) {
  return error ? { borderColor: 'var(--ep-danger)', boxShadow: '0 0 0 3px rgba(255,59,48,0.12)' } : {};
}
