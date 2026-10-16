import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { KeyRound, Mail, ArrowLeft } from 'lucide-react';
import { requestPasswordReset } from '../../services/authService';
import { validateForgotPasswordEmail } from '../../utils/forgotPasswordValidation';

export function ForgotPassword() {
  const [email, setEmail] = useState('');
  const [fieldError, setFieldError] = useState('');
  const [formError, setFormError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSubmitted, setIsSubmitted] = useState(false);
  const [submittedEmail, setSubmittedEmail] = useState('');

  const handleEmailChange = (e) => {
    setEmail(e.target.value);
    if (fieldError) setFieldError('');
    if (formError) setFormError('');
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (isSubmitting) return;

    setFormError('');

    const error = validateForgotPasswordEmail(email);
    if (error) {
      setFieldError(error);
      return;
    }

    const cleanEmail = email.trim();
    setIsSubmitting(true);

    try {
      await requestPasswordReset(cleanEmail);
      setSubmittedEmail(cleanEmail);
      setIsSubmitted(true);
    } catch (err) {
      if (err.status === 400 && err.errors && err.errors.length > 0) {
        setFormError(err.errors.join(' '));
      } else {
        setFormError(err.message || "We couldn't process your request right now. Please try again.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleTryAnotherEmail = () => {
    setIsSubmitted(false);
    setEmail('');
    setFieldError('');
    setFormError('');
  };

  return (
    <div style={containerStyle}>
      <Link to="/" style={{ textDecoration: 'none', marginBottom: '32px' }} data-testid="forgot-password-brand-link">
        <span className="ep-brand">
          Event<span style={{ color: 'var(--ep-primary)' }}>Pulse</span>
        </span>
      </Link>

      <div
        data-testid="forgot-password-card"
        style={cardStyle}
      >
        {isSubmitted ? (
          /* Confirmation State — Uniform generic feedback preventing account enumeration */
          <div data-testid="forgot-password-confirmation" style={{ textAlign: 'center' }}>
            <div style={iconWrapperStyle}>
              <Mail size={26} color="var(--ep-primary)" />
            </div>

            <h1 className="ep-h2" style={{ marginBottom: '10px' }} data-testid="forgot-password-confirmation-title">
              Check your email
            </h1>

            <p
              className="ep-body"
              data-testid="forgot-password-confirmation-message"
              style={{ maxWidth: '380px', margin: '0 auto 16px', lineHeight: 1.5 }}
            >
              If an account exists for that email, we've sent password reset instructions to your inbox.
            </p>

            {submittedEmail && (
              <p
                style={{
                  fontWeight: 600,
                  fontSize: '14px',
                  color: 'var(--ep-text-primary)',
                  marginBottom: '20px',
                  wordBreak: 'break-all',
                }}
                data-testid="forgot-password-submitted-email"
              >
                {submittedEmail}
              </p>
            )}

            <div
              style={{
                backgroundColor: '#F5F5F7',
                borderRadius: '10px',
                padding: '14px 16px',
                marginBottom: '24px',
                fontSize: '13px',
                color: 'var(--ep-text-secondary)',
                lineHeight: 1.5,
                textAlign: 'left',
              }}
            >
              Please check your inbox (and spam folder) within the next 30 minutes. If you don't receive an email, verify that the address was typed correctly.
            </div>

            <Link
              to="/login"
              data-testid="confirmation-login-btn"
              className="ep-btn-primary"
              style={{
                textDecoration: 'none',
                display: 'inline-flex',
                alignItems: 'center',
                justifyContent: 'center',
                width: '100%',
                height: '46px',
                fontSize: '15px',
                marginBottom: '16px',
              }}
            >
              Return to Sign In
            </Link>

            <div>
              <button
                type="button"
                data-testid="try-another-email-btn"
                onClick={handleTryAnotherEmail}
                style={{
                  background: 'none',
                  border: 'none',
                  padding: 0,
                  fontSize: '13px',
                  color: 'var(--ep-primary)',
                  cursor: 'pointer',
                  fontWeight: 500,
                }}
              >
                Try a different email address
              </button>
            </div>
          </div>
        ) : (
          /* Request Reset Input Form */
          <>
            <div style={iconWrapperStyle}>
              <KeyRound size={26} color="var(--ep-primary)" />
            </div>

            <div style={{ marginBottom: '24px' }}>
              <h1 className="ep-h2" style={{ marginBottom: '6px' }} data-testid="forgot-password-title">
                Forgot password?
              </h1>
              <p className="ep-body" style={{ margin: 0 }}>
                Enter your registered email address and we'll send you instructions to reset your password.
              </p>
            </div>

            {/* Controlled Form-level Error Alert */}
            {formError && (
              <div
                data-testid="forgot-password-alert-error"
                style={{
                  backgroundColor: '#FFF0EF',
                  border: '1px solid #FFCDD2',
                  borderRadius: '10px',
                  padding: '12px 14px',
                  marginBottom: '20px',
                  fontSize: '13px',
                  color: 'var(--ep-danger)',
                }}
              >
                {formError}
              </div>
            )}

            <form onSubmit={handleSubmit} noValidate data-testid="forgot-password-form">
              <div style={{ marginBottom: '24px' }}>
                <label
                  htmlFor="forgot-email"
                  style={{
                    display: 'block',
                    fontSize: '13px',
                    fontWeight: 500,
                    color: 'var(--ep-text-primary)',
                    marginBottom: '6px',
                  }}
                >
                  Email <span style={{ color: 'var(--ep-danger)' }}>*</span>
                </label>
                <input
                  id="forgot-email"
                  data-testid="forgot-password-email-input"
                  type="email"
                  className="ep-input"
                  style={fieldError ? { borderColor: 'var(--ep-danger)', boxShadow: '0 0 0 3px rgba(255,59,48,0.12)' } : {}}
                  value={email}
                  onChange={handleEmailChange}
                  autoComplete="email"
                  disabled={isSubmitting}
                  placeholder="Enter your email"
                  autoFocus
                />
                {fieldError && (
                  <p
                    data-testid="forgot-password-email-error"
                    style={{ margin: '5px 0 0', fontSize: '12px', color: 'var(--ep-danger)' }}
                  >
                    {fieldError}
                  </p>
                )}
              </div>

              <button
                type="submit"
                className="ep-btn-primary"
                data-testid="forgot-password-submit-btn"
                style={{
                  width: '100%',
                  height: '46px',
                  fontSize: '15px',
                  marginBottom: '20px',
                }}
                disabled={isSubmitting}
              >
                {isSubmitting ? 'Sending instructions…' : 'Send Reset Link'}
              </button>
            </form>

            <div style={{ textAlign: 'center' }}>
              <Link
                to="/login"
                data-testid="back-to-login-link"
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '6px',
                  fontSize: '13px',
                  color: 'var(--ep-text-secondary)',
                  textDecoration: 'none',
                  fontWeight: 500,
                }}
              >
                <ArrowLeft size={14} /> Back to sign in
              </Link>
            </div>
          </>
        )}
      </div>
    </div>
  );
}

const containerStyle = {
  minHeight: '100vh',
  backgroundColor: 'var(--ep-canvas)',
  display: 'flex',
  flexDirection: 'column',
  alignItems: 'center',
  justifyContent: 'flex-start',
  padding: '40px 16px 64px',
};

const cardStyle = {
  width: '100%',
  maxWidth: '460px',
  backgroundColor: '#ffffff',
  borderRadius: 'var(--ep-radius-card)',
  border: '1px solid var(--ep-border)',
  boxShadow: '0 2px 8px rgba(0,0,0,0.04)',
  padding: '36px 32px',
};

const iconWrapperStyle = {
  display: 'inline-flex',
  alignItems: 'center',
  justifyContent: 'center',
  width: '56px',
  height: '56px',
  backgroundColor: 'var(--ep-soft-accent)',
  borderRadius: '14px',
  marginBottom: '20px',
};

export default ForgotPassword;
