import React from 'react';
import { Link, useLocation } from 'react-router-dom';
import { Mail } from 'lucide-react';

/**
 * EP-8 Phase 3 shell.
 * Shown immediately after a successful registration.
 * OTP input will be added in Phase 3 — this layout is structured to receive it.
 */
export function VerifyEmail() {
  const location = useLocation();
  const email = location.state?.email || 'your email address';

  return (
    <div style={{
      minHeight: '100vh',
      backgroundColor: 'var(--ep-canvas)',
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      justifyContent: 'flex-start',
      padding: '60px 16px 64px',
    }}>
      {/* Brand */}
      <Link to="/" style={{ textDecoration: 'none', marginBottom: '40px' }}>
        <span className="ep-brand">
          Event<span style={{ color: 'var(--ep-primary)' }}>Pulse</span>
        </span>
      </Link>

      {/* Card */}
      <div style={{
        width: '100%',
        maxWidth: '460px',
        backgroundColor: '#ffffff',
        borderRadius: 'var(--ep-radius-card)',
        border: '1px solid var(--ep-border)',
        boxShadow: '0 2px 8px rgba(0,0,0,0.04)',
        padding: '40px 32px',
        textAlign: 'center',
      }}>
        {/* Icon */}
        <div style={{
          display: 'inline-flex',
          alignItems: 'center',
          justifyContent: 'center',
          width: '56px',
          height: '56px',
          backgroundColor: 'var(--ep-soft-accent)',
          borderRadius: '14px',
          marginBottom: '20px',
        }}>
          <Mail size={26} color="var(--ep-primary)" />
        </div>

        <h1 className="ep-h2" style={{ marginBottom: '10px' }}>Verify your email</h1>

        <p className="ep-body" style={{ maxWidth: '340px', margin: '0 auto 8px' }}>
          A verification code will be sent to:
        </p>
        <p style={{ fontWeight: 600, color: 'var(--ep-text-primary)', fontSize: '14px', marginBottom: '24px' }}>
          {email}
        </p>

        {/*
          ----------------------------------------------------------------
          Phase 3 OTP input goes here.
          Replace this placeholder block with the 6-digit code input.
          ----------------------------------------------------------------
        */}
        <div style={{
          backgroundColor: 'var(--ep-canvas)',
          borderRadius: '12px',
          padding: '20px 16px',
          marginBottom: '24px',
          border: '1px dashed var(--ep-border)',
        }}>
          <p className="ep-caption" style={{ color: 'var(--ep-text-secondary)', margin: 0 }}>
            Email verification will be available once the verification service is live.
          </p>
        </div>

        <Link to="/" className="ep-btn-secondary" style={{ textDecoration: 'none', display: 'inline-block' }}>
          Return to homepage
        </Link>
      </div>
    </div>
  );
}
