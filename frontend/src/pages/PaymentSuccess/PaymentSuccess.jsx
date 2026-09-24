import React from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { CheckCircle2, ArrowRight } from 'lucide-react';

export function PaymentSuccess() {
  const [searchParams] = useSearchParams();
  const sessionId = searchParams.get('session_id');

  return (
    <div style={{
      minHeight: '100vh',
      backgroundColor: 'var(--ep-canvas)',
      display: 'flex',
      flexDirection: 'column',
    }}>
      <Header />
      <main style={{
        flex: 1,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '32px 16px',
      }}>
        <div style={{
          backgroundColor: '#ffffff',
          borderRadius: 'var(--ep-radius-card)',
          border: '1px solid var(--ep-border)',
          boxShadow: 'var(--ep-shadow-hover)',
          padding: '48px 32px',
          maxWidth: '500px',
          width: '100%',
          textAlign: 'center',
        }}>
          <div style={{
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: '64px',
            height: '64px',
            borderRadius: '50%',
            backgroundColor: '#F0FDF4',
            border: '1px solid #BBF7D0',
            marginBottom: '20px',
          }}>
            <CheckCircle2 size={32} color="#16A34A" />
          </div>

          <h1 style={{
            fontSize: '24px',
            fontWeight: 700,
            color: 'var(--ep-text-primary)',
            marginBottom: '8px',
            letterSpacing: '-0.005em',
          }}>
            Payment Initiated Successfully!
          </h1>

          <p style={{
            fontSize: '14px',
            color: 'var(--ep-text-secondary)',
            marginBottom: '24px',
            lineHeight: 1.5,
          }}>
            Your payment session has been confirmed. Your ticket reservations are being finalized.
          </p>

          {sessionId && (
            <div style={{
              backgroundColor: 'var(--ep-canvas)',
              borderRadius: '8px',
              padding: '12px 16px',
              marginBottom: '28px',
              fontSize: '12px',
              color: 'var(--ep-text-secondary)',
              wordBreak: 'break-all',
              textAlign: 'left',
              border: '1px solid var(--ep-border)',
            }}>
              <span style={{ fontWeight: 600, color: 'var(--ep-text-primary)', display: 'block', marginBottom: '4px' }}>
                Stripe Session Reference:
              </span>
              <code>{sessionId}</code>
            </div>
          )}

          <div style={{ display: 'flex', justifyContent: 'center', gap: '12px', flexWrap: 'wrap' }}>
            <Link
              to="/"
              className="ep-btn-primary"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '8px',
                padding: '12px 24px',
                fontSize: '14px',
                fontWeight: 600,
                textDecoration: 'none',
                borderRadius: 'var(--ep-radius-btn)',
              }}
            >
              <span>Explore More Events</span>
              <ArrowRight size={16} />
            </Link>
          </div>
        </div>
      </main>
    </div>
  );
}
