import React from 'react';
import { Link } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { ShieldAlert } from 'lucide-react';

export function Forbidden() {
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
            width: '56px',
            height: '56px',
            borderRadius: '50%',
            backgroundColor: 'var(--ep-canvas)',
            marginBottom: '20px',
          }}>
            <ShieldAlert size={28} color="var(--ep-primary)" />
          </div>

          <h1 style={{
            fontSize: '24px',
            fontWeight: 700,
            color: 'var(--ep-text-primary)',
            marginBottom: '8px',
            letterSpacing: '-0.005em',
          }}>
            Access denied
          </h1>

          <p style={{
            fontSize: '14px',
            color: 'var(--ep-text-secondary)',
            marginBottom: '28px',
            lineHeight: 1.5,
          }}>
            You don't have permission to access this page.
          </p>

          <Link
            to="/"
            className="ep-btn-primary"
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              padding: '12px 24px',
              fontSize: '14px',
              fontWeight: 600,
              textDecoration: 'none',
              borderRadius: 'var(--ep-radius-btn)',
            }}
          >
            Back to home
          </Link>
        </div>
      </main>
    </div>
  );
}
