import React, { useState, useRef, useEffect } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { MapPin, User, ChevronDown, LogOut, LayoutDashboard, Plus, ShieldCheck, FileCheck } from 'lucide-react';
import { useAuth } from '../../context/AuthContext';

export function Header({ location = 'Colombo, LK' }) {
  const navigate = useNavigate();
  const routerLocation = useLocation();
  const { isAuthenticated, currentUser, logout, hasRole } = useAuth();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef(null);

  const displayName = currentUser?.firstName
    ? `${currentUser.firstName} ${currentUser.lastName ? currentUser.lastName.charAt(0) + '.' : ''}`
    : 'Account';

  // Extract roles for display & navigation checks
  const roles = Array.isArray(currentUser?.roles) ? currentUser.roles : [];
  const isOrganizer = hasRole('Organizer');
  const isAdmin = hasRole('Administrator');

  // Primary display role label for dropdown
  const primaryRoleLabel = isAdmin
    ? (isOrganizer ? 'Administrator & Organizer' : 'Administrator')
    : isOrganizer
    ? 'Organizer'
    : isAuthenticated
    ? 'Customer'
    : null;

  // Close menu when clicking outside
  useEffect(() => {
    const handleClickOutside = (e) => {
      if (menuRef.current && !menuRef.current.contains(e.target)) {
        setMenuOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const handleLogout = () => {
    setMenuOpen(false);
    logout();
    navigate('/');
  };

  const navLinkStyle = (path) => ({
    fontSize: '13px',
    fontWeight: routerLocation.pathname === path ? 600 : 500,
    color: routerLocation.pathname === path ? 'var(--ep-primary)' : 'var(--ep-text-primary)',
    textDecoration: 'none',
    display: 'inline-flex',
    alignItems: 'center',
    gap: '6px',
    transition: 'var(--ep-transition)',
  });

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
        {/* Left: Brand & Role-Aware Navigation Links */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '32px' }}>
          <Link to="/" style={{ textDecoration: 'none' }}>
            <span className="ep-brand">
              Event<span style={{ color: 'var(--ep-primary)' }}>Pulse</span>
            </span>
          </Link>

          {/* Nav links */}
          {(isOrganizer || isAdmin) && (
            <nav style={{ display: 'flex', alignItems: 'center', gap: '20px' }}>
              {/* Organizer navigation */}
              {isOrganizer && (
                <Link to="/organizer" style={navLinkStyle('/organizer')}>
                  <LayoutDashboard size={14} />
                  <span>Organizer Dashboard</span>
                </Link>
              )}

              {/* Administrator navigation */}
              {isAdmin && (
                <>
                  <Link to="/admin" style={navLinkStyle('/admin')}>
                    <ShieldCheck size={14} />
                    <span>Admin Dashboard</span>
                  </Link>
                  <Link to="/admin/events/pending" style={navLinkStyle('/admin/events/pending')}>
                    <FileCheck size={14} />
                    <span>Pending Events</span>
                  </Link>
                </>
              )}
            </nav>
          )}
        </div>

        {/* Right: Location & Account Dropdown / Sign In */}
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
            /* ---- Account menu (all roles) ---- */
            <div ref={menuRef} style={{ position: 'relative' }}>
              <button
                type="button"
                id="account-menu-trigger"
                aria-haspopup="true"
                aria-expanded={menuOpen}
                aria-label="Account menu"
                onClick={() => setMenuOpen((o) => !o)}
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '6px',
                  backgroundColor: 'var(--ep-canvas)',
                  padding: '6px 14px',
                  borderRadius: 'var(--ep-radius-pill)',
                  fontSize: '13px',
                  fontWeight: 600,
                  color: 'var(--ep-text-primary)',
                  border: '1px solid var(--ep-border)',
                  cursor: 'pointer',
                  transition: 'var(--ep-transition)',
                }}
                onKeyDown={(e) => {
                  if (e.key === 'Escape') setMenuOpen(false);
                }}
              >
                <User size={15} color="var(--ep-text-secondary)" />
                <span>{displayName}</span>
                <ChevronDown
                  size={13}
                  color="var(--ep-text-secondary)"
                  style={{
                    transition: 'transform 150ms ease-out',
                    transform: menuOpen ? 'rotate(180deg)' : 'rotate(0deg)',
                  }}
                />
              </button>

              {/* Dropdown Menu */}
              {menuOpen && (
                <div
                  role="menu"
                  aria-labelledby="account-menu-trigger"
                  style={{
                    position: 'absolute',
                    top: 'calc(100% + 8px)',
                    right: 0,
                    minWidth: '200px',
                    backgroundColor: '#ffffff',
                    border: '1px solid var(--ep-border)',
                    borderRadius: 'var(--ep-radius-card)',
                    boxShadow: 'var(--ep-shadow-hover)',
                    overflow: 'hidden',
                    zIndex: 200,
                  }}
                >
                  {/* Identity Header */}
                  <div style={{
                    padding: '10px 14px',
                    borderBottom: '1px solid var(--ep-border)',
                  }}>
                    <div style={{
                      fontSize: '13px',
                      fontWeight: 600,
                      color: 'var(--ep-text-primary)',
                      whiteSpace: 'nowrap',
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                    }}>
                      {currentUser?.email ?? displayName}
                    </div>
                    {primaryRoleLabel && (
                      <div style={{
                        fontSize: '11px',
                        fontWeight: 500,
                        color: 'var(--ep-text-secondary)',
                        marginTop: '2px',
                      }}>
                        {primaryRoleLabel}
                      </div>
                    )}
                  </div>

                  {/* Log out */}
                  <button
                    type="button"
                    role="menuitem"
                    onClick={handleLogout}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: '8px',
                      width: '100%',
                      padding: '10px 14px',
                      background: 'none',
                      border: 'none',
                      fontSize: '13px',
                      fontWeight: 500,
                      color: 'var(--ep-text-primary)',
                      cursor: 'pointer',
                      transition: 'var(--ep-transition)',
                      fontFamily: 'var(--ep-font-body)',
                      textAlign: 'left',
                    }}
                    onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'var(--ep-canvas)'}
                    onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                  >
                    <LogOut size={14} color="var(--ep-text-secondary)" />
                    <span>Log out</span>
                  </button>
                </div>
              )}
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
