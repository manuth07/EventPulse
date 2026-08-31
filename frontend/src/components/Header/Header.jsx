import React from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { MapPin, User, LogOut } from 'lucide-react';
import { useAuth } from '../../context/AuthContext';

export function Header({ location = 'Colombo, LK' }) {
  const navigate = useNavigate();
  const { isAuthenticated, currentUser, logout } = useAuth();

  const handleLogout = () => {
    logout();
    navigate('/');
  };

  const displayName = currentUser?.firstName
    ? `${currentUser.firstName} ${currentUser.lastName ? currentUser.lastName.charAt(0) + '.' : ''}`
    : 'Account';

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

          {isAuthenticated ? (
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <div style={{
                display: 'flex',
                alignItems: 'center',
                gap: '8px',
                backgroundColor: 'var(--ep-canvas)',
                padding: '6px 14px',
                borderRadius: 'var(--ep-radius-pill)',
                fontSize: '13px',
                fontWeight: 600,
                color: 'var(--ep-text-primary)',
                border: '1px solid var(--ep-border)',
              }}>
                <User size={15} color="var(--ep-text-secondary)" />
                <span>{displayName}</span>
              </div>
              <button
                type="button"
                onClick={handleLogout}
                className="ep-btn-secondary"
                aria-label="Log out"
                style={{
                  fontSize: '13px',
                  padding: '6px 14px',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '6px',
                  cursor: 'pointer',
                  borderRadius: 'var(--ep-radius-pill)',
                }}
              >
                <LogOut size={14} color="var(--ep-text-secondary)" />
                <span>Log out</span>
              </button>
            </div>
          ) : (
            <Link
              to="/login"
              className="ep-btn-secondary"
              style={{ fontSize: '13px', padding: '8px 16px', textDecoration: 'none' }}
            >
              Sign In
            </Link>
          )}
        </div>
      </div>
    </header>
  );
}
