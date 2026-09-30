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
        padding: '40px 16px',
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
          {/* Cancel Icon */}
          <div style={{
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: '64px',
            height: '64px',
            borderRadius: '50%',
            backgroundColor: '#FEF2F2',
            border: '2px solid #FECACA',
            marginBottom: '20px',
          }}>
            <XCircle size={36} color="var(--ep-danger)" />
          </div>

          <h1 style={{
            fontSize: '24px',
            fontWeight: 800,
            color: 'var(--ep-text-primary)',
            marginBottom: '8px',
            letterSpacing: '-0.01em',
          }}>
            Payment Cancelled
          </h1>

          <p style={{
            fontSize: '15px',
            fontWeight: 600,
            color: 'var(--ep-danger)',
            marginBottom: '16px',
          }}>
            Your transaction was not completed.
          </p>

          <p style={{
            fontSize: '14px',
            color: 'var(--ep-text-secondary)',
            marginBottom: '28px',
            lineHeight: 1.6,
          }}>
            Payment was cancelled. Your reservation is still pending in your cart. You can return to checkout whenever you're ready.
          </p>

          <div style={{
            display: 'flex',
            flexDirection: 'column',
            gap: '12px',
          }}>
            <Link
              to="/cart"
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
              <ShoppingCart size={16} />
              <span>Return to Cart</span>
            </Link>

            <Link
              to="/"
              className="ep-btn-secondary"
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
              <span>Browse Other Events</span>
              <ArrowRight size={16} />
            </Link>
          </div>
        </div>
      </main>
    </div>
  );
}

export default PaymentCancel;
