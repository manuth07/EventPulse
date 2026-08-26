import React, { useState, useEffect } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { Mail } from 'lucide-react';
import { verifyEmail, resendVerification } from '../../services/authService';

export function VerifyEmail() {
  const location = useLocation();
  const navigate = useNavigate();
  const email = location.state?.email;

  const [code, setCode] = useState('');
  const [isVerifying, setIsVerifying] = useState(false);
  const [isResending, setIsResending] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);
  
  const [cooldown, setCooldown] = useState(60);

  useEffect(() => {
    if (!email) {
      navigate('/register', { replace: true });
    }
  }, [email, navigate]);

  useEffect(() => {
    let timer;
    if (cooldown > 0) {
      timer = setInterval(() => setCooldown((c) => c - 1), 1000);
    }
    return () => clearInterval(timer);
  }, [cooldown]);

  const handleVerify = async (e) => {
    e.preventDefault();
    setError('');

    if (code.length !== 6 || !/^\d+$/.test(code)) {
      setError('Please enter a valid 6-digit code.');
      return;
    }

    setIsVerifying(true);
    try {
      await verifyEmail(email, code);
      setSuccess(true);
    } catch (err) {
      setError(err.message || 'Verification failed. Please try again.');
    } finally {
      setIsVerifying(false);
    }
  };

  const handleResend = async () => {
    if (cooldown > 0 || isResending) return;
    
    setError('');
    setIsResending(true);
    try {
      await resendVerification(email);
      setCooldown(60);
      setCode('');
    } catch (err) {
      setError(err.message || 'Failed to resend code.');
    } finally {
      setIsResending(false);
    }
  };

  const formatCooldown = (seconds) => {
    const m = Math.floor(seconds / 60).toString().padStart(2, '0');
    const s = (seconds % 60).toString().padStart(2, '0');
    return `${m}:${s}`;
  };

  if (!email) return null;

  if (success) {
    return (
      <div style={containerStyle}>
        <Link to="/" style={{ textDecoration: 'none', marginBottom: '40px' }}>
          <span className="ep-brand">
            Event<span style={{ color: 'var(--ep-primary)' }}>Pulse</span>
          </span>
        </Link>
        <div style={cardStyle}>
          <div style={iconWrapperStyle}>
            <Mail size={26} color="var(--ep-primary)" />
          </div>
          <h1 className="ep-h2" style={{ marginBottom: '10px' }}>Email verified</h1>
          <p className="ep-body" style={{ marginBottom: '24px' }}>
            Your EventPulse account is ready.
          </p>
          <Link to="/" className="ep-btn-primary" style={{ textDecoration: 'none', display: 'inline-block', width: '100%', textAlign: 'center', padding: '12px 0' }}>
            Continue to Sign In
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div style={containerStyle}>
      <Link to="/" style={{ textDecoration: 'none', marginBottom: '40px' }}>
        <span className="ep-brand">
          Event<span style={{ color: 'var(--ep-primary)' }}>Pulse</span>
        </span>
      </Link>

      <div style={cardStyle}>
        <div style={iconWrapperStyle}>
          <Mail size={26} color="var(--ep-primary)" />
        </div>

        <h1 className="ep-h2" style={{ marginBottom: '10px' }}>Verify your email</h1>

        <p className="ep-body" style={{ maxWidth: '340px', margin: '0 auto 8px' }}>
          We've sent a 6-digit code to
        </p>
        <p style={{ fontWeight: 600, color: 'var(--ep-text-primary)', fontSize: '14px', marginBottom: '24px' }}>
          {email}
        </p>

        {error && (
          <div style={{
            backgroundColor: '#FFF0EF',
            border: '1px solid #FFCDD2',
            borderRadius: '10px',
            padding: '12px 14px',
            marginBottom: '20px',
            fontSize: '13px',
            color: 'var(--ep-danger)',
            textAlign: 'left'
          }}>
            {error}
          </div>
        )}

        <form onSubmit={handleVerify}>
          <input
            type="text"
            inputMode="numeric"
            maxLength={6}
            value={code}
            onChange={(e) => {
              const val = e.target.value.replace(/\D/g, '');
              setCode(val);
              setError('');
            }}
            placeholder="000000"
            className="ep-input"
            style={{
              fontSize: '24px',
              letterSpacing: '8px',
              textAlign: 'center',
              padding: '16px',
              marginBottom: '20px',
              fontWeight: '600'
            }}
            autoComplete="one-time-code"
          />

          <button
            type="submit"
            className="ep-btn-primary"
            style={{ width: '100%', height: '46px', fontSize: '15px', marginBottom: '24px' }}
            disabled={isVerifying || code.length !== 6}
          >
            {isVerifying ? 'Verifying...' : 'Verify Email'}
          </button>
        </form>

        <div style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', marginBottom: '16px' }}>
          Didn't receive the code?{' '}
          {cooldown > 0 ? (
            <span>Resend in {formatCooldown(cooldown)}</span>
          ) : (
            <button
              type="button"
              onClick={handleResend}
              disabled={isResending}
              style={{
                background: 'none',
                border: 'none',
                color: 'var(--ep-primary)',
                fontWeight: 500,
                cursor: 'pointer',
                padding: 0
              }}
            >
              {isResending ? 'Sending...' : 'Resend code'}
            </button>
          )}
        </div>

        <Link to="/register" style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', textDecoration: 'none' }}>
          Back to registration
        </Link>
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
  padding: '60px 16px 64px',
};

const cardStyle = {
  width: '100%',
  maxWidth: '460px',
  backgroundColor: '#ffffff',
  borderRadius: 'var(--ep-radius-card)',
  border: '1px solid var(--ep-border)',
  boxShadow: '0 2px 8px rgba(0,0,0,0.04)',
  padding: '40px 32px',
  textAlign: 'center',
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
