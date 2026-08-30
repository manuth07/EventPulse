import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { completeProfile } from '../../services/authService';
import { useAuth } from '../../context/AuthContext';
import { COUNTRIES, DIAL_CODES } from '../../data/countries';

export function CompleteProfile() {
  const navigate = useNavigate();
  const { currentUser, accessToken, login: setAuth } = useAuth();

  const [phoneNumber, setPhoneNumber] = useState('');
  const [countryCode, setCountryCode] = useState('LK');
  const [fieldErrors, setFieldErrors] = useState({});
  const [formError, setFormError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const dialCode = DIAL_CODES[countryCode] || '';

  function validate() {
    const errors = {};
    if (!phoneNumber.trim()) errors.phoneNumber = 'Contact number is required.';
    if (!countryCode) errors.countryCode = 'Country is required.';
    return errors;
  }

  async function handleSubmit(e) {
    e.preventDefault();
    setFormError('');

    const errors = validate();
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }

    const localDigits = phoneNumber.replace(/\D/g, '');
    const normalizedPhone = `${dialCode}${localDigits}`;

    setIsSubmitting(true);
    try {
      await completeProfile(
        { phoneNumber: normalizedPhone, countryCode: countryCode.trim().toUpperCase() },
        accessToken
      );

      // Update AuthContext so profileCompleted flag is reflected in Header/UI
      if (currentUser) {
        setAuth({
          accessToken,
          user: { ...currentUser, profileCompleted: true }
        });
      }

      navigate('/', { replace: true });
    } catch (err) {
      if (err.errors?.length > 0) {
        setFormError(err.errors.join(' '));
      } else {
        setFormError(err.message || 'Profile update failed. Please try again.');
      }
    } finally {
      setIsSubmitting(false);
    }
  }

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
      <Link to="/" style={{ textDecoration: 'none', marginBottom: '32px' }}>
        <span className="ep-brand">
          Event<span style={{ color: 'var(--ep-primary)' }}>Pulse</span>
        </span>
      </Link>

      <div style={{
        width: '100%',
        maxWidth: '460px',
        backgroundColor: '#ffffff',
        borderRadius: 'var(--ep-radius-card)',
        border: '1px solid var(--ep-border)',
        boxShadow: '0 2px 8px rgba(0,0,0,0.04)',
        padding: '36px 32px',
      }}>
        <div style={{ marginBottom: '24px' }}>
          <h1 className="ep-h2" style={{ marginBottom: '6px' }}>
            Complete your profile
          </h1>
          <p className="ep-body" style={{ margin: 0 }}>
            {currentUser?.firstName ? `Welcome, ${currentUser.firstName}. ` : ''}
            A few more details to get you started.
          </p>
        </div>

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
          {/* Country */}
          <Field label="Country" id="profile-country" error={fieldErrors.countryCode} style={{ marginBottom: '14px' }}>
            <select
              id="profile-country"
              className="ep-input"
              style={{ ...inputStyle(fieldErrors.countryCode), cursor: 'pointer' }}
              value={countryCode}
              onChange={(e) => {
                setCountryCode(e.target.value);
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
          <Field label="Contact number" id="profile-phone" error={fieldErrors.phoneNumber} style={{ marginBottom: '24px' }}>
            <div style={{ display: 'flex', gap: '8px' }}>
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
                id="profile-phone"
                type="tel"
                className="ep-input"
                style={{ ...inputStyle(fieldErrors.phoneNumber), flex: 1 }}
                value={phoneNumber}
                onChange={(e) => {
                  setPhoneNumber(e.target.value);
                  setFieldErrors((prev) => { const n = { ...prev }; delete n.phoneNumber; return n; });
                }}
                autoComplete="tel-national"
                disabled={isSubmitting}
                placeholder="77 123 4567"
              />
            </div>
          </Field>

          <button
            type="submit"
            className="ep-btn-primary"
            style={{ width: '100%', height: '46px', fontSize: '15px' }}
            disabled={isSubmitting}
          >
            {isSubmitting ? 'Saving…' : 'Save and Continue'}
          </button>
        </form>
      </div>
    </div>
  );
}

function Field({ label, id, error, children, style }) {
  return (
    <div style={style}>
      <label htmlFor={id} style={{
        display: 'block', fontSize: '13px', fontWeight: 500,
        color: 'var(--ep-text-primary)', marginBottom: '6px',
      }}>
        {label}
      </label>
      {children}
      {error && <p style={{ margin: '5px 0 0', fontSize: '12px', color: 'var(--ep-danger)' }}>{error}</p>}
    </div>
  );
}

function inputStyle(error) {
  return error ? { borderColor: 'var(--ep-danger)', boxShadow: '0 0 0 3px rgba(255,59,48,0.12)' } : {};
}
