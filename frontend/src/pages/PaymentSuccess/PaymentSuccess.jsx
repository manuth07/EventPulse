import React, { useEffect } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { CheckCircle2, ArrowRight } from 'lucide-react';

export function PaymentSuccess() {
  const [searchParams] = useSearchParams();
  const sessionId = searchParams.get('session_id');

  useEffect(() => {
    try {
      sessionStorage.removeItem('ep_pending_booking');
    } catch (e) {
      console.warn('Failed to clear pending booking from sessionStorage:', e);
    }
  }, []);

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
        padding: '40px 16px',
      }}>
        <div style={{
          backgroundColor: '#ffffff',
          borderRadius: 'var(--ep-radius-card)',
          border: '1px solid var(--ep-border)',
          boxShadow: 'var(--ep-shadow-hover)',
          padding: '48px 32px',
          maxWidth: '520px',
          width: '100%',
          textAlign: 'center',
        }}>
          {/* Success Icon */}
          <div style={{
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: '64px',
            height: '64px',
            borderRadius: '50%',
            backgroundColor: '#F0FDF4',
            border: '2px solid #BBF7D0',
            marginBottom: '20px',
          }}>
            <CheckCircle2 size={36} color="#16A34A" />
          </div>

          <h1 style={{
            fontSize: '24px',
            fontWeight: 800,
            color: 'var(--ep-text-primary)',
            marginBottom: '8px',
            letterSpacing: '-0.01em',
          }}>
            Payment Submitted
          </h1>

          <p style={{
            fontSize: '15px',
            fontWeight: 600,
            color: '#15803D',
            marginBottom: '16px',
          }}>
            Your booking is being confirmed!
          </p>

          <p style={{
            fontSize: '14px',
            color: 'var(--ep-text-secondary)',
            marginBottom: '24px',
            lineHeight: 1.6,
          }}>
            We have received your payment request. Your tickets and QR codes will be generated and delivered as soon as the transaction verification is completed.
          </p>

          {sessionId && (
            <div style={{
              backgroundColor: 'var(--ep-canvas)',
              borderRadius: '8px',
              padding: '12px',
              marginBottom: '28px',
              fontSize: '12px',
              color: 'var(--ep-text-secondary)',
              wordBreak: 'break-all',
              fontFamily: 'monospace',
            }}>
              <span style={{ fontWeight: 600, color: 'var(--ep-text-primary)' }}>Session ID: </span>
              {sessionId}
            </div>
          )}

          <div style={{
            display: 'flex',
            flexDirection: 'column',
            gap: '12px',
          }}>
            <Link
              to="/"
              className="ep-btn-primary"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: '8px',
                padding: '12px 24px',
                fontSize: '14px',
                fontWeight: 600,
                textDecoration: 'none',
                borderRadius: 'var(--ep-radius-btn)',
              }}
            >
              <span>Browse More Events</span>
              <ArrowRight size={16} />
            </Link>
          </div>
        </div>
      </main>
    </div>
  );
}

export default PaymentSuccess;
