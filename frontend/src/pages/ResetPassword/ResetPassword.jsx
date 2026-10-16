import React, { useState, useEffect } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { KeyRound, Eye, EyeOff, Check, CheckCircle2, AlertCircle, ArrowLeft } from 'lucide-react';
import { validateResetToken, resetPassword } from '../../services/authService';
import { checkPasswordRules, validateResetPasswordForm } from '../../utils/resetPasswordValidation';

export function ResetPassword() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') || '';

  // Token validation state (on initial page load)
  const [isValidatingToken, setIsValidatingToken] = useState(true);
  const [tokenError, setTokenError] = useState(null); // { title, message, code }

  // Form state
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);

  const [fieldErrors, setFieldErrors] = useState({});
  const [formError, setFormError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSuccess, setIsSuccess] = useState(false);

  // Validate token from URL on mount
  useEffect(() => {
    let isMounted = true;

    async function checkToken() {
      if (!token.trim()) {
        if (isMounted) {
          setTokenError({
            code: 'MISSING_TOKEN',
            title: 'Missing reset link',
            message: 'No reset token was found in the link. Please check that you clicked or pasted the full URL from your email.',
          });
          setIsValidatingToken(false);
        }
        return;
      }

      try {
        await validateResetToken(token.trim());
        if (isMounted) {
          setTokenError(null);
          setIsValidatingToken(false);
        }
      } catch (err) {
        if (!isMounted) return;

        if (err.code === 'TOKEN_EXPIRED') {
          setTokenError({
            code: 'TOKEN_EXPIRED',
            title: 'Reset link expired',
            message: 'This password reset link has expired. Password reset links are valid for 30 minutes for security reasons.',
          });
        } else if (err.code === 'TOKEN_ALREADY_USED') {
          setTokenError({
            code: 'TOKEN_ALREADY_USED',
            title: 'Reset link already used',
            message: 'This password reset link has already been used. Each reset link can only be used once.',
          });
        } else {
          setTokenError({
            code: 'INVALID_TOKEN',
            title: 'Invalid reset link',
            message: err.message || 'This password reset link is invalid or malformed. Please request a new reset link.',
          });
        }
        setIsValidatingToken(false);
      }
    }

    checkToken();

    return () => {
      isMounted = false;
    };
  }, [token]);

  const rules = checkPasswordRules(newPassword);
  const passwordsMatch = Boolean(newPassword && confirmPassword && newPassword === confirmPassword);

  const handleNewPasswordChange = (e) => {
    setNewPassword(e.target.value);
    if (fieldErrors.password) {
      setFieldErrors((prev) => {
        const next = { ...prev };
        delete next.password;
        return next;
      });
    }
    setFormError('');
  };

  const handleConfirmPasswordChange = (e) => {
    setConfirmPassword(e.target.value);
    if (fieldErrors.confirmPassword) {
      setFieldErrors((prev) => {
        const next = { ...prev };
        delete next.confirmPassword;
        return next;
      });
    }
    setFormError('');
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (isSubmitting) return;

    setFormError('');

    const { errors, isValid } = validateResetPasswordForm(newPassword, confirmPassword);
    if (!isValid) {
      setFieldErrors(errors);
      return;
    }

    setIsSubmitting(true);

    try {
      await resetPassword({
        token: token.trim(),
        newPassword,
        confirmPassword,
      });

      setIsSuccess(true);
    } catch (err) {
      if (err.code === 'TOKEN_EXPIRED') {
        setTokenError({
          code: 'TOKEN_EXPIRED',
          title: 'Reset link expired',
          message: 'Your reset link expired while you were filling out the form. Please request a new link.',
        });
      } else if (err.code === 'TOKEN_ALREADY_USED') {
        setTokenError({
          code: 'TOKEN_ALREADY_USED',
          title: 'Reset link already used',
          message: 'This reset token has already been consumed. If you need to reset again, please request a new link.',
        });
      } else if (err.code === 'INVALID_TOKEN') {
        setTokenError({
          code: 'INVALID_TOKEN',
          title: 'Invalid reset link',
          message: 'This reset token is no longer valid. Please request a new link.',
        });
      } else if (err.errors && err.errors.length > 0) {
        setFormError(err.errors.join(' '));
      } else {
        setFormError(err.message || 'Failed to reset password. Please try again.');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div style={containerStyle}>
      <Link to="/" style={{ textDecoration: 'none', marginBottom: '32px' }} data-testid="reset-password-brand-link">
        <span className="ep-brand">
          Event<span style={{ color: 'var(--ep-primary)' }}>Pulse</span>
        </span>
      </Link>

      <div data-testid="reset-password-card" style={cardStyle}>
        {/* 1. Loading / Verifying Token State */}
        {isValidatingToken && (
          <div data-testid="reset-password-loading" style={{ textAlign: 'center', padding: '24px 0' }}>
            <div style={iconWrapperStyle}>
              <KeyRound size={26} color="var(--ep-primary)" />
            </div>
            <h1 className="ep-h2" style={{ marginBottom: '8px' }}>
              Verifying link…
            </h1>
            <p className="ep-body" style={{ margin: 0, color: 'var(--ep-text-secondary)' }}>
              Checking the validity of your password reset link.
            </p>
          </div>
        )}

        {/* 2. Unusable Token State (Expired / Already Used / Invalid / Missing) */}
        {!isValidatingToken && tokenError && (
          <div data-testid="reset-password-token-error" style={{ textAlign: 'center' }}>
            <div style={{ ...iconWrapperStyle, backgroundColor: '#FFF0EF' }}>
              <AlertCircle size={26} color="var(--ep-danger)" />
            </div>

            <h1
              className="ep-h2"
              data-testid="reset-password-token-error-title"
              style={{ marginBottom: '10px' }}
            >
              {tokenError.title}
            </h1>

            <p
              className="ep-body"
              data-testid="reset-password-token-error-message"
              style={{ maxWidth: '380px', margin: '0 auto 24px', lineHeight: 1.5 }}
            >
              {tokenError.message}
            </p>

            <Link
              to="/forgot-password"
              data-testid="request-new-link-btn"
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
              Request a new reset link
            </Link>

            <div>
              <Link
                to="/login"
                data-testid="token-error-back-login-link"
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
          </div>
        )}

        {/* 3. Successful Reset State */}
        {!isValidatingToken && !tokenError && isSuccess && (
          <div data-testid="reset-password-success" style={{ textAlign: 'center' }}>
            <div style={{ ...iconWrapperStyle, backgroundColor: '#E8F8EE' }}>
              <CheckCircle2 size={30} color="#34C759" />
            </div>

            <h1
              className="ep-h2"
              data-testid="reset-password-success-title"
              style={{ marginBottom: '10px' }}
            >
              Password reset successful
            </h1>

            <p
              className="ep-body"
              data-testid="reset-password-success-message"
              style={{ maxWidth: '380px', margin: '0 auto 24px', lineHeight: 1.5 }}
            >
              Your password has been updated. You can now sign in to your EventPulse account with your new password.
            </p>

            <Link
              to="/login"
              data-testid="reset-success-login-btn"
              className="ep-btn-primary"
              style={{
                textDecoration: 'none',
                display: 'inline-flex',
                alignItems: 'center',
                justifyContent: 'center',
                width: '100%',
                height: '46px',
                fontSize: '15px',
              }}
            >
              Continue to Sign In
            </Link>
          </div>
        )}

        {/* 4. Active Reset Form */}
        {!isValidatingToken && !tokenError && !isSuccess && (
          <>
            <div style={iconWrapperStyle}>
              <KeyRound size={26} color="var(--ep-primary)" />
            </div>

            <div style={{ marginBottom: '24px' }}>
              <h1 className="ep-h2" style={{ marginBottom: '6px' }} data-testid="reset-password-title">
                Set new password
              </h1>
              <p className="ep-body" style={{ margin: 0 }}>
                Choose a strong password to secure your EventPulse account.
              </p>
            </div>

            {/* Controlled Form-level Error Alert */}
            {formError && (
              <div
                data-testid="reset-password-alert-error"
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

            <form onSubmit={handleSubmit} noValidate data-testid="reset-password-form">
              {/* New Password */}
              <div style={{ marginBottom: '16px' }}>
                <label
                  htmlFor="reset-new-password"
                  style={{
                    display: 'block',
                    fontSize: '13px',
                    fontWeight: 500,
                    color: 'var(--ep-text-primary)',
                    marginBottom: '6px',
                  }}
                >
                  New password <span style={{ color: 'var(--ep-danger)' }}>*</span>
                </label>
                <div style={{ position: 'relative' }}>
                  <input
                    id="reset-new-password"
                    data-testid="new-password-input"
                    type={showNewPassword ? 'text' : 'password'}
                    className="ep-input"
                    style={{
                      ...(fieldErrors.password ? errorInputStyle : {}),
                      paddingRight: '44px',
                    }}
                    value={newPassword}
                    onChange={handleNewPasswordChange}
                    autoComplete="new-password"
                    disabled={isSubmitting}
                    placeholder="Enter new password"
                    autoFocus
                  />
                  <button
                    type="button"
                    data-testid="toggle-new-password-btn"
                    aria-label={showNewPassword ? 'Hide password' : 'Show password'}
                    onClick={() => setShowNewPassword((v) => !v)}
                    disabled={isSubmitting}
                    style={passwordToggleStyle}
                  >
                    {showNewPassword ? <EyeOff size={16} /> : <Eye size={16} />}
                  </button>
                </div>
                {fieldErrors.password && (
                  <p
                    data-testid="new-password-error"
                    style={{ margin: '5px 0 0', fontSize: '12px', color: 'var(--ep-danger)' }}
                  >
                    {fieldErrors.password}
                  </p>
                )}
              </div>

              {/* Password Requirements Checklist */}
              <div
                style={{
                  backgroundColor: '#FAFAFA',
                  border: '1px solid var(--ep-border)',
                  borderRadius: '10px',
                  padding: '12px 14px',
                  marginBottom: '18px',
                  fontSize: '12px',
                }}
              >
                <div style={{ fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '8px' }}>
                  Password requirements:
                </div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
                  <RuleItem met={rules.minLength} label="At least 8 characters" testId="rule-min-length" />
                  <RuleItem met={rules.uppercase} label="At least one uppercase letter (A-Z)" testId="rule-uppercase" />
                  <RuleItem met={rules.lowercase} label="At least one lowercase letter (a-z)" testId="rule-lowercase" />
                  <RuleItem met={rules.number} label="At least one number (0-9)" testId="rule-number" />
                </div>
              </div>

              {/* Confirm Password */}
              <div style={{ marginBottom: '24px' }}>
                <label
                  htmlFor="reset-confirm-password"
                  style={{
                    display: 'block',
                    fontSize: '13px',
                    fontWeight: 500,
                    color: 'var(--ep-text-primary)',
                    marginBottom: '6px',
                  }}
                >
                  Confirm new password <span style={{ color: 'var(--ep-danger)' }}>*</span>
                </label>
                <div style={{ position: 'relative' }}>
                  <input
                    id="reset-confirm-password"
                    data-testid="confirm-password-input"
                    type={showConfirmPassword ? 'text' : 'password'}
                    className="ep-input"
                    style={{
                      ...(fieldErrors.confirmPassword ? errorInputStyle : {}),
                      paddingRight: '44px',
                    }}
                    value={confirmPassword}
                    onChange={handleConfirmPasswordChange}
                    autoComplete="new-password"
                    disabled={isSubmitting}
                    placeholder="Repeat new password"
                  />
                  <button
                    type="button"
                    data-testid="toggle-confirm-password-btn"
                    aria-label={showConfirmPassword ? 'Hide password' : 'Show password'}
                    onClick={() => setShowConfirmPassword((v) => !v)}
                    disabled={isSubmitting}
                    style={passwordToggleStyle}
                  >
                    {showConfirmPassword ? <EyeOff size={16} /> : <Eye size={16} />}
                  </button>
                </div>
                {fieldErrors.confirmPassword && (
                  <p
                    data-testid="confirm-password-error"
                    style={{ margin: '5px 0 0', fontSize: '12px', color: 'var(--ep-danger)' }}
                  >
                    {fieldErrors.confirmPassword}
                  </p>
                )}
                {confirmPassword && !fieldErrors.confirmPassword && (
                  <div style={{ marginTop: '6px' }}>
                    <RuleItem met={passwordsMatch} label="Passwords match" testId="rule-match" />
                  </div>
                )}
              </div>

              <button
                type="submit"
                className="ep-btn-primary"
                data-testid="reset-password-submit-btn"
                style={{
                  width: '100%',
                  height: '46px',
                  fontSize: '15px',
                  marginBottom: '20px',
                }}
                disabled={isSubmitting}
              >
                {isSubmitting ? 'Resetting password…' : 'Reset Password'}
              </button>
            </form>

            <div style={{ textAlign: 'center' }}>
              <Link
                to="/login"
                data-testid="reset-form-back-login-link"
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

function RuleItem({ met, label, testId }) {
  return (
    <div
      data-testid={testId}
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        color: met ? '#2E7D32' : 'var(--ep-text-secondary)',
        fontSize: '12px',
        fontWeight: met ? 500 : 400,
        transition: 'color 150ms ease',
      }}
    >
      <div
        style={{
          width: '16px',
          height: '16px',
          borderRadius: '50%',
          backgroundColor: met ? '#E8F8EE' : '#F0F0F2',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          flexShrink: 0,
        }}
      >
        {met ? (
          <Check size={11} color="#2E7D32" strokeWidth={3} />
        ) : (
          <div style={{ width: '4px', height: '4px', borderRadius: '50%', backgroundColor: '#8E8E93' }} />
        )}
      </div>
      <span>{label}</span>
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

const passwordToggleStyle = {
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
};

const errorInputStyle = {
  borderColor: 'var(--ep-danger)',
  boxShadow: '0 0 0 3px rgba(255,59,48,0.12)',
};

export default ResetPassword;
