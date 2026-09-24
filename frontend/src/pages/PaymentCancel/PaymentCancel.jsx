import React from 'react';
import { Link } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { XCircle, ShoppingCart, ArrowRight } from 'lucide-react';

export function PaymentCancel() {
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
          maxWidth: '480px',
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
            backgroundColor: '#FFF5F5',
            border: '1px solid #FED7D7',
            marginBottom: '20px',
          }}>
            <XCircle size={32} color="var(--ep-danger)" />
          </div>

          <h1 style={{
            fontSize: '24px',
            fontWeight: 700,
            color: 'var(--ep-text-primary)',
            marginBottom: '8px',
            letterSpacing: '-0.005em',
          }}>
            Payment Cancelled
          </h1>

          <p style={{
            fontSize: '14px',
            color: 'var(--ep-text-secondary)',
            marginBottom: '28px',
            lineHeight: 1.5,
          }}>
            You have cancelled your payment session. No funds were debited. You can review your cart or return to browse events whenever you're ready.
          </p>

          <div style={{ display: 'flex', justifyContent: 'center', gap: '12px', flexWrap: 'wrap' }}>
            <Link
              to="/cart"
              className="ep-btn-secondary"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '8px',
                padding: '10px 20px',
                fontSize: '14px',
                fontWeight: 600,
                textDecoration: 'none',
              }}
            >
              <ShoppingCart size={15} />
              <span>Return to Cart</span>
            </Link>

            <Link
              to="/"
              className="ep-btn-primary"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '8px',
                padding: '10px 20px',
                fontSize: '14px',
                fontWeight: 600,
                textDecoration: 'none',
                borderRadius: 'var(--ep-radius-btn)',
              }}
            >
              <span>Browse Events</span>
              <ArrowRight size={15} />
            </Link>
          </div>
        </div>
      </main>
    </div>
  );
}
