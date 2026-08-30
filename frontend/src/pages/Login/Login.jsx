import React, { useState, useEffect, useRef } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { Eye, EyeOff } from 'lucide-react';
import { login, googleLogin } from '../../services/authService';
import { useAuth } from '../../context/AuthContext';

const GOOGLE_CLIENT_ID = import.meta.env.VITE_GOOGLE_CLIENT_ID;
const isValidEmail = (v) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v.trim());

export function Login() {
  const navigate = useNavigate();
  const location = useLocation();
  const { login: setAuth } = useAuth();
  const googleButtonRef = useRef(null);

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);

  const [fieldErrors, setFieldErrors] = useState({});
  const [formError, setFormError] = useState('');
  const [unverifiedEmail, setUnverifiedEmail] = useState(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // State for ACCOUNT_LINK_REQUIRED flow — keep credential in memory only, never in storage
  const [linkRequired, setLinkRequired] = useState(false);
  const [pendingCredential, setPendingCredential] = useState(null);

  // -----------------------------------------------------------------------
  // Google Identity Services initialization
  // -----------------------------------------------------------------------
  useEffect(() => {
    if (!GOOGLE_CLIENT_ID || !googleButtonRef.current) return;

    const scriptId = 'google-gis-script';
    if (document.getElementById(scriptId)) {
      initGoogleButton();
      return;
    }

    const script = document.createElement('script');
    script.id = scriptId;
    script.src = 'https://accounts.google.com/gsi/client';
    script.async = true;
    script.defer = true;
    script.onload = initGoogleButton;
    document.head.appendChild(script);

    return () => {
      // Cleanup: revoke on unmount so credentials don't linger in GIS state
      window.google?.accounts?.id?.cancel?.();
    };
  }, []);

  function initGoogleButton() {
    if (!window.google?.accounts?.id) return;
    window.google.accounts.id.initialize({
      client_id: GOOGLE_CLIENT_ID,
      callback: handleGoogleCredentialResponse,
      auto_select: false,
      cancel_on_tap_outside: true,
    });
    window.google.accounts.id.renderButton(googleButtonRef.current, {
      theme: 'outline',
      size: 'large',
      type: 'standard',
      text: 'continue_with',
      width: 396,
      logo_alignment: 'left',
    });
  }

  async function handleGoogleCredentialResponse(response) {
    if (!response?.credential) return;
    setFormError('');
    setIsSubmitting(true);
    try {
      const result = await googleLogin(response.credential);
      handleAuthSuccess(result);
    } catch (err) {
      if (err.code === 'ACCOUNT_LINK_REQUIRED') {
        // Store credential only in memory — never in localStorage/sessionStorage
        setPendingCredential(response.credential);
        setLinkRequired(true);
        setFormError('');
      } else if (err.code === 'GOOGLE_EMAIL_NOT_VERIFIED') {
        setFormError('Your Google account email must be verified before signing in.');
      } else if (err.code === 'ACCOUNT_DISABLED') {
        setFormError('This account is currently unavailable. Please contact EventPulse support if you need help.');
      } else {
        setFormError(err.message || "We couldn't sign you in right now. Please try again.");
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  function handleAuthSuccess(result) {
    setAuth(result);
    // Navigate to profile completion if required, otherwise to intended destination
    if (result?.user?.profileCompleted === false) {
      navigate('/complete-profile', { replace: true });
    } else {
      const from = location.state?.from?.pathname || '/';
      navigate(from, { replace: true });
    }
  }

  // -----------------------------------------------------------------------
  // Email/Password form handlers
  // -----------------------------------------------------------------------

  const handleEmailChange = (e) => {
    setEmail(e.target.value);
    if (fieldErrors.email) setFieldErrors((prev) => { const n = { ...prev }; delete n.email; return n; });
    setFormError('');
    setUnverifiedEmail(null);
  };

  const handlePasswordChange = (e) => {
    setPassword(e.target.value);
    if (fieldErrors.password) setFieldErrors((prev) => { const n = { ...prev }; delete n.password; return n; });
    setFormError('');
  };

  function validate() {
    const errors = {};
    if (!email.trim()) errors.email = 'Email address is required.';
    else if (!isValidEmail(email)) errors.email = 'Enter a valid email address.';
    if (!password) errors.password = 'Password is required.';
    return errors;
  }

  async function handleSubmit(e) {
    e.preventDefault();
    setFormError('');
    setUnverifiedEmail(null);

    const errors = validate();
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }

    setIsSubmitting(true);
    try {
      const result = await login({ email: email.trim(), password });
      handleAuthSuccess(result);
    } catch (err) {
      if (err.status === 401) {
        setFormError('Invalid email or password.');
      } else if (err.status === 403 && err.code === 'EMAIL_NOT_VERIFIED') {
        setFormError("Your email hasn't been verified yet.");
        setUnverifiedEmail(email.trim());
      } else if (err.status === 403 && err.code === 'ACCOUNT_DISABLED') {
        setFormError('This account is currently unavailable. Please contact EventPulse support if you need help.');
      } else if (err.status === 400 && err.errors?.length > 0) {
        setFormError(err.errors.join(' '));
      } else {
        setFormError(err.message || "We couldn't sign you in right now. Please try again.");
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  const handleNavigateToVerify = () => {
    navigate('/verify-email', { state: { email: unverifiedEmail } });
  };

  const handleCancelLink = () => {
    setPendingCredential(null);
    setLinkRequired(false);
    setFormError('');
  };

  // -----------------------------------------------------------------------
  // ACCOUNT_LINK_REQUIRED inline state
  // -----------------------------------------------------------------------
  if (linkRequired && pendingCredential) {
    return (
      <AccountLinkView
        credential={pendingCredential}
        onSuccess={handleAuthSuccess}
        onCancel={handleCancelLink}
      />
    );
  }

  // -----------------------------------------------------------------------
  // Render main login form
  // -----------------------------------------------------------------------
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
          <h1 className="ep-h2" style={{ marginBottom: '6px' }}>Welcome back</h1>
          <p className="ep-body" style={{ margin: 0 }}>Sign in to continue to EventPulse.</p>
        </div>

        {/* Form-level Error Alert */}
        {formError && (
          <div style={{
            backgroundColor: '#FFF0EF',
            border: '1px solid #FFCDD2',
            borderRadius: '10px',
            padding: '12px 14px',
            marginBottom: '20px',
            fontSize: '13px',
            color: 'var(--ep-danger)',
            display: 'flex',
            flexDirection: 'column',
            gap: '8px',
          }}>
            <span>{formError}</span>
            {unverifiedEmail && (
              <button type="button" onClick={handleNavigateToVerify}
                className="ep-btn-secondary"
                style={{ fontSize: '12px', padding: '6px 12px', alignSelf: 'flex-start', borderColor: '#FFCDD2', backgroundColor: '#ffffff' }}>
                Verify email
              </button>
            )}
          </div>
        )}

        <form onSubmit={handleSubmit} noValidate>
          <Field label="Email" id="login-email" error={fieldErrors.email} style={{ marginBottom: '16px' }}>
            <input
              id="login-email"
              type="email"
              className="ep-input"
              style={inputStyle(fieldErrors.email)}
              value={email}
              onChange={handleEmailChange}
              autoComplete="email"
              disabled={isSubmitting}
              placeholder="Enter your email"
            />
          </Field>

          <Field label="Password" id="login-password" error={fieldErrors.password} style={{ marginBottom: '24px' }}>
            <div style={{ position: 'relative' }}>
              <input
                id="login-password"
                type={showPassword ? 'text' : 'password'}
                className="ep-input"
                style={{ ...inputStyle(fieldErrors.password), paddingRight: '44px' }}
                value={password}
                onChange={handlePasswordChange}
                autoComplete="current-password"
                disabled={isSubmitting}
                placeholder="••••••••"
              />
              <button type="button"
                aria-label={showPassword ? 'Hide password' : 'Show password'}
                onClick={() => setShowPassword((v) => !v)}
                disabled={isSubmitting}
                style={{
                  position: 'absolute', right: '12px', top: '50%', transform: 'translateY(-50%)',
                  background: 'none', border: 'none', cursor: 'pointer', padding: '4px',
                  color: 'var(--ep-text-secondary)', display: 'flex', alignItems: 'center',
                }}>
                {showPassword ? <EyeOff size={16} /> : <Eye size={16} />}
              </button>
            </div>
          </Field>

          <button type="submit" className="ep-btn-primary"
            style={{ width: '100%', height: '46px', fontSize: '15px' }}
            disabled={isSubmitting}>
            {isSubmitting ? 'Signing in…' : 'Sign In'}
          </button>
        </form>

        {/* Google Sign-In — only rendered when VITE_GOOGLE_CLIENT_ID is present */}
        {GOOGLE_CLIENT_ID ? (
          <div>
            <div style={{
              display: 'flex', alignItems: 'center', gap: '12px',
              margin: '20px 0',
            }}>
              <div style={{ flex: 1, height: '1px', backgroundColor: 'var(--ep-border)' }} />
              <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', whiteSpace: 'nowrap' }}>or</span>
              <div style={{ flex: 1, height: '1px', backgroundColor: 'var(--ep-border)' }} />
            </div>
            <div ref={googleButtonRef} style={{ display: 'flex', justifyContent: 'center' }} />
          </div>
        ) : null}

        <p style={{ textAlign: 'center', marginTop: '20px', marginBottom: 0, fontSize: '13px', color: 'var(--ep-text-secondary)' }}>
          Don't have an account?{' '}
          <Link to="/register" style={{ color: 'var(--ep-primary)', fontWeight: 500, textDecoration: 'none' }}>
            Create account
          </Link>
        </p>
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Account Link View — shown when 409 ACCOUNT_LINK_REQUIRED
// Keeps the Google credential in component memory only, never in storage.
// ---------------------------------------------------------------------------
function AccountLinkView({ credential, onSuccess, onCancel }) {
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');
  const [isLinking, setIsLinking] = useState(false);

  // Import inline to avoid circular dependency via authService
  const handleLink = async (e) => {
    e.preventDefault();
    if (!password.trim()) {
      setError('Password is required.');
      return;
    }
    setError('');
    setIsLinking(true);
    try {
      const { googleLinkExisting } = await import('../../services/authService');
      const result = await googleLinkExisting({ credential, password });
      onSuccess(result);
    } catch (err) {
      if (err.code === 'INVALID_CREDENTIALS') {
        setError('Incorrect password. Please try again.');
      } else {
        setError(err.message || 'Linking failed. Please try again.');
      }
    } finally {
      setIsLinking(false);
    }
  };

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
      <div style={{ textDecoration: 'none', marginBottom: '32px' }}>
        <span className="ep-brand">
          Event<span style={{ color: 'var(--ep-primary)' }}>Pulse</span>
        </span>
      </div>

      <div style={{
        width: '100%',
        maxWidth: '460px',
        backgroundColor: '#ffffff',
        borderRadius: 'var(--ep-radius-card)',
        border: '1px solid var(--ep-border)',
        boxShadow: '0 2px 8px rgba(0,0,0,0.04)',
        padding: '36px 32px',
      }}>
        <h1 className="ep-h2" style={{ marginBottom: '8px' }}>Connect Google</h1>
        <p className="ep-body" style={{ marginBottom: '24px' }}>
          This email already belongs to an EventPulse account.
          Enter your EventPulse password once to securely connect Google to your existing account.
        </p>

        {error && (
          <div style={{
            backgroundColor: '#FFF0EF', border: '1px solid #FFCDD2', borderRadius: '10px',
            padding: '12px 14px', marginBottom: '20px', fontSize: '13px', color: 'var(--ep-danger)',
          }}>
            {error}
          </div>
        )}

        <form onSubmit={handleLink} noValidate>
          <Field label="EventPulse Password" id="link-password" style={{ marginBottom: '20px' }}>
            <div style={{ position: 'relative' }}>
              <input
                id="link-password"
                type={showPassword ? 'text' : 'password'}
                className="ep-input"
                style={{ paddingRight: '44px' }}
                value={password}
                onChange={(e) => { setPassword(e.target.value); setError(''); }}
                autoComplete="current-password"
                disabled={isLinking}
                placeholder="••••••••"
              />
              <button type="button"
                aria-label={showPassword ? 'Hide password' : 'Show password'}
                onClick={() => setShowPassword((v) => !v)}
                disabled={isLinking}
                style={{
                  position: 'absolute', right: '12px', top: '50%', transform: 'translateY(-50%)',
                  background: 'none', border: 'none', cursor: 'pointer', padding: '4px',
                  color: 'var(--ep-text-secondary)', display: 'flex', alignItems: 'center',
                }}>
                {showPassword ? <EyeOff size={16} /> : <Eye size={16} />}
              </button>
            </div>
          </Field>

          <button type="submit" className="ep-btn-primary"
            style={{ width: '100%', height: '46px', fontSize: '15px', marginBottom: '12px' }}
            disabled={isLinking}>
            {isLinking ? 'Connecting…' : 'Link Google Account'}
          </button>

          <button type="button" onClick={onCancel}
            className="ep-btn-secondary"
            style={{ width: '100%', height: '44px', fontSize: '14px' }}
            disabled={isLinking}>
            Cancel
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
