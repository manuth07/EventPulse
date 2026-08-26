import React from 'react';
import { Link } from 'react-router-dom';
import { MapPin } from 'lucide-react';

export function Header({ location = 'Colombo, LK' }) {
  return (
    <header style={{
      backgroundColor: '#ffffff',
      borderBottom: '1px solid var(--ep-border)',
      position: 'sticky',
      top: 0,
      zIndex: 100,
    }}>
      <div className="container" style={{
        height: '72px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        paddingLeft: '16px',
        paddingRight: '16px',
      }}>
        {/* Brand Wordmark */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <Link to="/" style={{ textDecoration: 'none' }}>
            <span className="ep-brand">
              Event<span style={{ color: 'var(--ep-primary)' }}>Pulse</span>
            </span>
          </Link>
        </div>

        {/* Location & Navigation */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '24px' }}>
          <div className="d-none d-md-flex" style={{
            alignItems: 'center',
            gap: '6px',
            backgroundColor: 'var(--ep-canvas)',
            padding: '6px 12px',
            borderRadius: 'var(--ep-radius-pill)',
            fontSize: '13px',
            color: 'var(--ep-text-primary)',
            fontWeight: 500
          }}>
            <MapPin size={15} color="var(--ep-text-secondary)" />
            <span>{location}</span>
          </div>

          <Link
            to="/register"
            className="ep-btn-secondary"
            style={{ fontSize: '13px', padding: '8px 16px', textDecoration: 'none' }}
          >
            Sign In
          </Link>
        </div>
      </div>
    </header>
  );
}
