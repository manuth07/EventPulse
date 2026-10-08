import React, { useState, useRef, useEffect, useCallback } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { MapPin, User, ChevronDown, LogOut, LayoutDashboard, ShieldCheck, FileCheck, UserCheck, Ticket, ShoppingCart, Calendar, Bell, CheckCircle2, Clock } from 'lucide-react';
import { useAuth } from '../../context/AuthContext';
import { useCart } from '../../context/CartContext';
import { buildApiUrl } from '../../services/apiConfig';
import { getAdminNotifications, getAdminNotificationsUnreadCount, markAdminNotificationRead } from '../../services/eventService';

export function Header({ location = 'Colombo, LK' }) {
  const navigate = useNavigate();
  const routerLocation = useLocation();
  const { isAuthenticated, currentUser, logout, hasRole } = useAuth();
  const { cartCount } = useCart();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef(null);

  // Administrator Notifications State (EP-151 / EP-32)
  const [notifOpen, setNotifOpen] = useState(false);
  const [notifications, setNotifications] = useState([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [notifLoading, setNotifLoading] = useState(false);
  const [notifError, setNotifError] = useState(null);
  const notifRef = useRef(null);

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

  const { accessToken } = useAuth();
  const getEffectiveToken = useCallback(() => {
    return accessToken || sessionStorage.getItem('ep_access_token');
  }, [accessToken]);

  const fetchAdminNotifications = useCallback(async () => {
    if (!isAdmin) return;
    const token = getEffectiveToken();
    if (!token) return;

    try {
      const [list, count] = await Promise.all([
        getAdminNotifications(token).catch(() => []),
        getAdminNotificationsUnreadCount(token).catch(() => 0),
      ]);
      setNotifications(Array.isArray(list) ? list : []);
      setUnreadCount(typeof count === 'number' ? count : 0);
      setNotifError(null);
    } catch (err) {
      setNotifError(err.message || 'Failed to load notifications');
    }
  }, [isAdmin, getEffectiveToken]);

  // Load notifications for admin on mount and on route changes
  useEffect(() => {
    if (isAdmin) {
      fetchAdminNotifications();
    }
  }, [isAdmin, routerLocation.pathname, fetchAdminNotifications]);

  // Close menus when clicking outside
  useEffect(() => {
    const handleClickOutside = (e) => {
      if (menuRef.current && !menuRef.current.contains(e.target)) {
        setMenuOpen(false);
      }
      if (notifRef.current && !notifRef.current.contains(e.target)) {
        setNotifOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const handleNotificationClick = async (notif) => {
    setNotifOpen(false);
    const token = getEffectiveToken();
    if (token && !notif.IsRead && !notif.isRead) {
      try {
        await markAdminNotificationRead(notif.id || notif.Id, token);
        setNotifications((prev) =>
          prev.map((n) => (n.id === notif.id ? { ...n, isRead: true, IsRead: true } : n))
        );
        setUnreadCount((prev) => Math.max(0, prev - 1));
      } catch (err) {
        console.warn('Failed to mark notification read:', err);
      }
    }
    // Navigate to pending events page with reviewId parameter
    navigate(`/admin/events/pending?reviewId=${notif.eventId || notif.EventId}`);
  };

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
                  <Link to="/admin/organizer-applications" style={navLinkStyle('/admin/organizer-applications')}>
                    <UserCheck size={14} />
                    <span>Organizer Applications</span>
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

          {/* Cart Icon & Live Ticket Count Badge */}
          <Link
            to="/cart"
            id="global-cart-button"
            aria-label={`Shopping cart with ${cartCount} tickets`}
            style={{
              position: 'relative',
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              padding: '8px',
              borderRadius: 'var(--ep-radius-pill)',
              color: 'var(--ep-text-primary)',
              textDecoration: 'none',
              backgroundColor: routerLocation.pathname === '/cart' ? 'var(--ep-canvas)' : 'transparent',
              transition: 'var(--ep-transition)',
            }}
            onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = 'var(--ep-canvas)')}
            onMouseLeave={(e) => {
              if (routerLocation.pathname !== '/cart') {
                e.currentTarget.style.backgroundColor = 'transparent';
              }
            }}
          >
            <ShoppingCart size={20} color="var(--ep-text-primary)" />
            {cartCount > 0 && (
              <span
                id="global-cart-badge"
                style={{
                  position: 'absolute',
                  top: '-2px',
                  right: '-4px',
                  backgroundColor: 'var(--ep-primary)',
                  color: '#ffffff',
                  fontSize: '11px',
                  fontWeight: 700,
                  minWidth: '18px',
                  height: '18px',
                  borderRadius: '9px',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  padding: '0 4px',
                  lineHeight: 1,
                  boxShadow: '0 1px 3px rgba(0,0,0,0.2)',
                }}
              >
                {cartCount}
              </span>
            )}
          </Link>

          {/* Administrator Notifications Bell Indicator (EP-151 / EP-32) */}
          {isAdmin && (
            <div ref={notifRef} style={{ position: 'relative' }}>
              <button
                type="button"
                id="admin-notification-bell"
                data-testid="admin-notification-bell"
                aria-haspopup="true"
                aria-expanded={notifOpen}
                aria-label={`Admin notifications. ${unreadCount} unread.`}
                onClick={() => setNotifOpen((o) => !o)}
                style={{
                  position: 'relative',
                  display: 'inline-flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  padding: '8px',
                  borderRadius: 'var(--ep-radius-pill)',
                  color: 'var(--ep-text-primary)',
                  backgroundColor: notifOpen ? 'var(--ep-canvas)' : 'transparent',
                  border: 'none',
                  cursor: 'pointer',
                  transition: 'var(--ep-transition)',
                }}
                onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = 'var(--ep-canvas)')}
                onMouseLeave={(e) => {
                  if (!notifOpen) e.currentTarget.style.backgroundColor = 'transparent';
                }}
              >
                <Bell size={20} color="var(--ep-text-primary)" />
                {unreadCount > 0 && (
                  <span
                    id="admin-notification-badge"
                    data-testid="admin-notification-badge"
                    style={{
                      position: 'absolute',
                      top: '-2px',
                      right: '-4px',
                      backgroundColor: 'var(--ep-primary)',
                      color: '#ffffff',
                      fontSize: '11px',
                      fontWeight: 700,
                      minWidth: '18px',
                      height: '18px',
                      borderRadius: '9px',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      padding: '0 4px',
                      lineHeight: 1,
                      boxShadow: '0 1px 3px rgba(0,0,0,0.2)',
                    }}
                  >
                    {unreadCount}
                  </span>
                )}
              </button>

              {/* Notification Dropdown Drawer */}
              {notifOpen && (
                <div
                  role="region"
                  aria-labelledby="admin-notification-bell"
                  data-testid="admin-notification-dropdown"
                  style={{
                    position: 'absolute',
                    top: 'calc(100% + 8px)',
                    right: 0,
                    width: '340px',
                    backgroundColor: '#ffffff',
                    border: '1px solid var(--ep-border)',
                    borderRadius: 'var(--ep-radius-card)',
                    boxShadow: 'var(--ep-shadow-hover)',
                    overflow: 'hidden',
                    zIndex: 200,
                  }}
                >
                  <div style={{
                    padding: '12px 16px',
                    borderBottom: '1px solid var(--ep-border)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    backgroundColor: 'var(--ep-canvas)',
                  }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <Bell size={15} color="var(--ep-primary)" />
                      <span style={{ fontSize: '13px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                        Event Review Notifications
                      </span>
                    </div>
                    {unreadCount > 0 && (
                      <span style={{
                        fontSize: '11px',
                        fontWeight: 600,
                        color: 'var(--ep-primary)',
                        backgroundColor: '#FFF0E6',
                        padding: '2px 8px',
                        borderRadius: 'var(--ep-radius-pill)',
                      }}>
                        {unreadCount} new
                      </span>
                    )}
                  </div>

                  <div style={{ maxHeight: '360px', overflowY: 'auto' }}>
                    {notifications.length === 0 ? (
                      <div
                        data-testid="admin-notification-empty"
                        style={{
                          padding: '32px 16px',
                          textAlign: 'center',
                          color: 'var(--ep-text-secondary)',
                          fontSize: '13px',
                        }}
                      >
                        <CheckCircle2 size={24} color="#34C759" style={{ margin: '0 auto 8px auto' }} />
                        <p style={{ margin: 0, fontWeight: 500 }}>No event submissions waiting for review.</p>
                      </div>
                    ) : (
                      notifications.slice(0, 6).map((item) => {
                        const isPending = (item.reviewStatus || item.ReviewStatus || '').toLowerCase() === 'pending';
                        return (
                          <div
                            key={item.id || item.Id}
                            data-testid={`admin-notification-item-${item.id || item.Id}`}
                            onClick={() => handleNotificationClick(item)}
                            style={{
                              padding: '12px 16px',
                              borderBottom: '1px solid var(--ep-border)',
                              cursor: 'pointer',
                              backgroundColor: item.isRead || item.IsRead ? '#ffffff' : '#FFF9F5',
                              transition: 'var(--ep-transition)',
                            }}
                            onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = 'var(--ep-canvas)')}
                            onMouseLeave={(e) => {
                              e.currentTarget.style.backgroundColor = item.isRead || item.IsRead ? '#ffffff' : '#FFF9F5';
                            }}
                          >
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '4px' }}>
                              <span style={{
                                fontSize: '13px',
                                fontWeight: item.isRead || item.IsRead ? 600 : 700,
                                color: 'var(--ep-text-primary)',
                              }}>
                                {item.eventTitle || item.EventTitle || 'New Event'}
                              </span>
                              <span style={{
                                fontSize: '10px',
                                fontWeight: 700,
                                padding: '2px 6px',
                                borderRadius: 'var(--ep-radius-pill)',
                                textTransform: 'uppercase',
                                backgroundColor: isPending ? '#FFF0E6' : '#E8F5E9',
                                color: isPending ? 'var(--ep-primary)' : '#2E7D32',
                              }}>
                                {item.reviewStatus || item.ReviewStatus || 'Pending'}
                              </span>
                            </div>
                            <p style={{
                              fontSize: '12px',
                              color: 'var(--ep-text-secondary)',
                              margin: '0 0 6px 0',
                              lineHeight: 1.4,
                            }}>
                              {item.message || item.Message || 'New event submitted for review.'}
                            </p>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '4px', fontSize: '11px', color: 'var(--ep-text-secondary)' }}>
                              <Clock size={12} />
                              <span>
                                {item.submittedAtUtc || item.SubmittedAtUtc
                                  ? new Date(item.submittedAtUtc || item.SubmittedAtUtc).toLocaleDateString('en-US', {
                                      month: 'short',
                                      day: 'numeric',
                                      hour: '2-digit',
                                      minute: '2-digit',
                                    })
                                  : 'Recently'}
                              </span>
                            </div>
                          </div>
                        );
                      })
                    )}
                  </div>

                  <div style={{
                    padding: '8px 16px',
                    textAlign: 'center',
                    backgroundColor: 'var(--ep-canvas)',
                    borderTop: '1px solid var(--ep-border)',
                  }}>
                    <Link
                      to="/admin/events/pending"
                      onClick={() => setNotifOpen(false)}
                      style={{
                        fontSize: '12px',
                        fontWeight: 600,
                        color: 'var(--ep-primary)',
                        textDecoration: 'none',
                      }}
                    >
                      View all pending events →
                    </Link>
                  </div>
                </div>
              )}
            </div>
          )}

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
                {currentUser?.profilePictureUrl ? (
                  <img
                    src={
                      currentUser.profilePictureUrl.startsWith('http://') || currentUser.profilePictureUrl.startsWith('https://')
                        ? currentUser.profilePictureUrl
                        : buildApiUrl(currentUser.profilePictureUrl)
                    }
                    alt={displayName}
                    style={{
                      width: '20px',
                      height: '20px',
                      borderRadius: '50%',
                      objectFit: 'cover',
                    }}
                    onError={(e) => {
                      e.target.style.display = 'none';
                    }}
                  />
                ) : (
                  <User size={15} color="var(--ep-text-secondary)" />
                )}
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

                  {/* My Profile Link (EP-26) */}
                  <Link
                    to="/profile"
                    id="account-menu-profile"
                    data-testid="account-menu-profile"
                    role="menuitem"
                    onClick={() => setMenuOpen(false)}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: '8px',
                      width: '100%',
                      padding: '10px 14px',
                      background: 'none',
                      borderBottom: '1px solid var(--ep-border)',
                      fontSize: '13px',
                      fontWeight: 600,
                      color: 'var(--ep-text-primary)',
                      textDecoration: 'none',
                      cursor: 'pointer',
                      transition: 'var(--ep-transition)',
                      fontFamily: 'var(--ep-font-body)',
                      textAlign: 'left',
                      boxSizing: 'border-box',
                    }}
                    onMouseEnter={(e) => {
                      e.currentTarget.style.backgroundColor = 'var(--ep-soft-accent)';
                      e.currentTarget.style.color = 'var(--ep-primary)';
                      const svg = e.currentTarget.querySelector('svg');
                      if (svg) svg.style.color = 'var(--ep-primary)';
                    }}
                    onMouseLeave={(e) => {
                      e.currentTarget.style.backgroundColor = 'transparent';
                      e.currentTarget.style.color = 'var(--ep-text-primary)';
                      const svg = e.currentTarget.querySelector('svg');
                      if (svg) svg.style.color = 'var(--ep-text-secondary)';
                    }}
                  >
                    <User size={14} color="var(--ep-text-secondary)" />
                    <span>My Profile</span>
                  </Link>

                  {isOrganizer && (
                    <Link
                      to="/organizer"
                      role="menuitem"
                      onClick={() => setMenuOpen(false)}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '8px',
                        width: '100%',
                        padding: '10px 14px',
                        background: 'none',
                        borderBottom: '1px solid var(--ep-border)',
                        fontSize: '13px',
                        fontWeight: 500,
                        color: 'var(--ep-text-primary)',
                        textDecoration: 'none',
                        cursor: 'pointer',
                        transition: 'var(--ep-transition)',
                        fontFamily: 'var(--ep-font-body)',
                        textAlign: 'left',
                        boxSizing: 'border-box',
                      }}
                      onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'var(--ep-canvas)'}
                      onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                    >
                      <LayoutDashboard size={14} color="var(--ep-text-secondary)" />
                      <span>Organizer Dashboard</span>
                    </Link>
                  )}

                  {isAdmin && (
                    <>
                      <Link
                        to="/admin"
                        role="menuitem"
                        onClick={() => setMenuOpen(false)}
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          gap: '8px',
                          width: '100%',
                          padding: '10px 14px',
                          background: 'none',
                          borderBottom: '1px solid var(--ep-border)',
                          fontSize: '13px',
                          fontWeight: 500,
                          color: 'var(--ep-text-primary)',
                          textDecoration: 'none',
                          cursor: 'pointer',
                          transition: 'var(--ep-transition)',
                          fontFamily: 'var(--ep-font-body)',
                          textAlign: 'left',
                          boxSizing: 'border-box',
                        }}
                        onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'var(--ep-canvas)'}
                        onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                      >
                        <ShieldCheck size={14} color="var(--ep-text-secondary)" />
                        <span>Admin Dashboard</span>
                      </Link>
                      <Link
                        to="/admin/organizer-applications"
                        role="menuitem"
                        onClick={() => setMenuOpen(false)}
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          gap: '8px',
                          width: '100%',
                          padding: '10px 14px',
                          background: 'none',
                          borderBottom: '1px solid var(--ep-border)',
                          fontSize: '13px',
                          fontWeight: 500,
                          color: 'var(--ep-text-primary)',
                          textDecoration: 'none',
                          cursor: 'pointer',
                          transition: 'var(--ep-transition)',
                          fontFamily: 'var(--ep-font-body)',
                          textAlign: 'left',
                          boxSizing: 'border-box',
                        }}
                        onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'var(--ep-canvas)'}
                        onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                      >
                        <UserCheck size={14} color="var(--ep-text-secondary)" />
                        <span>Organizer Applications</span>
                      </Link>
                    </>
                  )}

                  {/* Role-Specific Navigation Item in Dropdown */}
                  {!isOrganizer && !isAdmin && (
                    <Link
                      to="/list-your-event"
                      role="menuitem"
                      onClick={() => setMenuOpen(false)}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '8px',
                        width: '100%',
                        padding: '10px 14px',
                        background: 'none',
                        borderBottom: '1px solid var(--ep-border)',
                        fontSize: '13px',
                        fontWeight: 500,
                        color: 'var(--ep-text-primary)',
                        textDecoration: 'none',
                        cursor: 'pointer',
                        transition: 'var(--ep-transition)',
                        fontFamily: 'var(--ep-font-body)',
                        textAlign: 'left',
                        boxSizing: 'border-box',
                      }}
                      onMouseEnter={(e) => {
                        e.currentTarget.style.backgroundColor = 'var(--ep-soft-accent)';
                        e.currentTarget.style.color = 'var(--ep-primary)';
                        const svg = e.currentTarget.querySelector('svg');
                        if (svg) svg.style.color = 'var(--ep-primary)';
                      }}
                      onMouseLeave={(e) => {
                        e.currentTarget.style.backgroundColor = 'transparent';
                        e.currentTarget.style.color = 'var(--ep-text-primary)';
                        const svg = e.currentTarget.querySelector('svg');
                        if (svg) svg.style.color = 'var(--ep-text-secondary)';
                      }}
                    >
                      <Ticket
                        size={14}
                        color="var(--ep-text-secondary)"
                        style={{ transition: 'var(--ep-transition)' }}
                      />
                      <span>List Your Event</span>
                    </Link>
                  )}

                  {/* My Bookings / Booked Tickets (available to all authenticated users: Customer, Organizer, Admin) */}
                  <Link
                    to="/my-bookings"
                    id="account-menu-my-bookings"
                    data-testid="account-menu-my-bookings"
                    role="menuitem"
                    onClick={() => setMenuOpen(false)}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: '8px',
                      width: '100%',
                      padding: '10px 14px',
                      background: 'none',
                      borderBottom: '1px solid var(--ep-border)',
                      fontSize: '13px',
                      fontWeight: 500,
                      color: 'var(--ep-text-primary)',
                      textDecoration: 'none',
                      cursor: 'pointer',
                      transition: 'var(--ep-transition)',
                      fontFamily: 'var(--ep-font-body)',
                      textAlign: 'left',
                      boxSizing: 'border-box',
                    }}
                    onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'var(--ep-canvas)'}
                    onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                  >
                    <Ticket size={14} color="var(--ep-text-secondary)" />
                    <span>My Booked Tickets</span>
                  </Link>

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
            <div style={{ display: 'flex', alignItems: 'center' }}>
              <Link
                to="/login"
                className="ep-btn-secondary"
                style={{ fontSize: '13px', padding: '8px 16px', textDecoration: 'none' }}
              >
                Sign In
              </Link>
            </div>
          )}
        </div>
      </div>
    </header>
  );
}
