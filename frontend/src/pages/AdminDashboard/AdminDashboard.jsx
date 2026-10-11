import React, { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { useAuth } from '../../context/AuthContext';
import {
  ShieldCheck,
  FileCheck,
  Users,
  UserCheck,
  Bell,
  Clock,
  CheckCircle2,
  AlertCircle,
  RefreshCw,
  ChevronRight,
  Ticket,
  UserX,
} from 'lucide-react';
import { getAdminNotifications, getAdminNotificationsUnreadCount } from '../../services/eventService';
import { getPlatformOverview } from '../../services/adminService';
import { formatPlatformMetric } from '../../utils/adminDashboardUtils';
import { UserManagementSection } from '../../components/Admin/UserManagementSection';

export function AdminDashboard() {
  const { accessToken, currentUser } = useAuth();
  const currentAdminId = currentUser?.id || currentUser?.userId;

  // Platform Metrics State
  const [overview, setOverview] = useState(null);
  const [overviewLoading, setOverviewLoading] = useState(true);
  const [overviewError, setOverviewError] = useState(null);

  // Review Notifications State
  const [notifications, setNotifications] = useState([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [notificationsLoading, setNotificationsLoading] = useState(true);

  // Overall refresh state
  const [refreshing, setRefreshing] = useState(false);

  const getEffectiveToken = useCallback(() => {
    return accessToken || sessionStorage.getItem('ep_access_token');
  }, [accessToken]);

  // Load platform overview metrics from backend
  const loadOverview = useCallback(async () => {
    const token = getEffectiveToken();
    if (!token) return;

    try {
      const data = await getPlatformOverview(token);
      setOverview(data);
      setOverviewError(null);
    } catch (err) {
      setOverviewError(err.message || 'Unable to load platform overview metrics.');
    } finally {
      setOverviewLoading(false);
    }
  }, [getEffectiveToken]);

  // Load event review notifications
  const loadNotifications = useCallback(async () => {
    const token = getEffectiveToken();
    if (!token) return;

    try {
      const [list, count] = await Promise.all([
        getAdminNotifications(token).catch(() => []),
        getAdminNotificationsUnreadCount(token).catch(() => 0),
      ]);
      setNotifications(Array.isArray(list) ? list : []);
      setUnreadCount(typeof count === 'number' ? count : 0);
    } catch {
      // Notifications fallback gracefully
      setNotifications([]);
      setUnreadCount(0);
    } finally {
      setNotificationsLoading(false);
    }
  }, [getEffectiveToken]);

  // Unified reload
  const handleRefreshAll = async () => {
    setRefreshing(true);
    await Promise.all([loadOverview(), loadNotifications()]);
    setRefreshing(false);
  };

  useEffect(() => {
    loadOverview();
    loadNotifications();
  }, [loadOverview, loadNotifications]);

  const eventsMetric = formatPlatformMetric(overview?.events);
  const bookingsMetric = formatPlatformMetric(overview?.bookings);
  const userMetrics = overview?.users || {};

  return (
    <div
      style={{
        minHeight: '100vh',
        backgroundColor: 'var(--ep-canvas)',
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      <Header />
      <main
        className="container"
        style={{
          flex: 1,
          paddingTop: '36px',
          paddingBottom: '64px',
        }}
      >
        {/* Header Section */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            marginBottom: '28px',
            flexWrap: 'wrap',
            gap: '16px',
          }}
        >
          <div>
            <div
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                backgroundColor: '#ffffff',
                color: 'var(--ep-text-primary)',
                padding: '4px 10px',
                borderRadius: 'var(--ep-radius-pill)',
                fontSize: '12px',
                fontWeight: 600,
                border: '1px solid var(--ep-border)',
                marginBottom: '8px',
              }}
            >
              <ShieldCheck size={14} color="var(--ep-primary)" />
              <span>Platform Administration</span>
            </div>
            <h1
              style={{
                fontSize: '28px',
                fontWeight: 700,
                color: 'var(--ep-text-primary)',
                margin: 0,
                letterSpacing: '-0.01em',
              }}
            >
              Admin Dashboard
            </h1>
            <p
              style={{
                fontSize: '14px',
                color: 'var(--ep-text-secondary)',
                marginTop: '4px',
                margin: 0,
              }}
            >
              Manage users, review platform metrics, and evaluate pending event submissions.
            </p>
          </div>

          <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap', alignItems: 'center' }}>
            <button
              type="button"
              data-testid="admin-dashboard-refresh-all-btn"
              onClick={handleRefreshAll}
              disabled={refreshing || overviewLoading}
              className="ep-btn-secondary"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                padding: '10px 16px',
                fontSize: '13px',
                fontWeight: 600,
              }}
            >
              <RefreshCw size={14} className={refreshing ? 'spin' : ''} />
              <span>{refreshing ? 'Refreshing...' : 'Refresh All'}</span>
            </button>

            <Link
              to="/admin/organizer-applications"
              className="ep-btn-secondary"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '8px',
                padding: '10px 16px',
                fontSize: '13px',
                fontWeight: 600,
                textDecoration: 'none',
              }}
            >
              <UserCheck size={15} />
              <span>Organizer Applications</span>
            </Link>

            <Link
              to="/admin/events/pending"
              className="ep-btn-secondary"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '8px',
                padding: '10px 16px',
                fontSize: '13px',
                fontWeight: 600,
                textDecoration: 'none',
              }}
            >
              <FileCheck size={15} />
              <span>Pending Events</span>
            </Link>

            <a
              href="#admin-user-management-section"
              className="ep-btn-primary"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '8px',
                padding: '10px 18px',
                fontSize: '13px',
                fontWeight: 600,
                textDecoration: 'none',
              }}
            >
              <Users size={15} />
              <span>Manage Users</span>
            </a>
          </div>
        </div>

        {/* Platform Overview Error Banner */}
        {overviewError && (
          <div
            data-testid="platform-overview-error"
            style={{
              padding: '14px 18px',
              backgroundColor: '#FFF0EF',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid #FFCDD2',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              marginBottom: '24px',
              gap: '12px',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
              <AlertCircle size={18} color="var(--ep-danger)" />
              <span style={{ fontSize: '13px', color: 'var(--ep-text-primary)' }}>{overviewError}</span>
            </div>
            <button
              type="button"
              onClick={loadOverview}
              className="ep-btn-secondary"
              style={{ fontSize: '12px', padding: '4px 12px' }}
            >
              Retry
            </button>
          </div>
        )}

        {/* Platform Overview Metrics Grid */}
        <section
          aria-label="Platform Overview Metrics"
          data-testid="platform-overview-metrics-grid"
          style={{ marginBottom: '32px' }}
        >
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              marginBottom: '16px',
            }}
          >
            <h2 style={{ fontSize: '16px', fontWeight: 700, color: 'var(--ep-text-primary)', margin: 0 }}>
              Platform Overview Metrics
            </h2>
            {overview?.generatedAtUtc && (
              <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                Updated {new Date(overview.generatedAtUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
              </span>
            )}
          </div>

          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))',
              gap: '14px',
            }}
          >
            {/* Total Users */}
            <div
              data-testid="metric-card-total-users"
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card)',
                border: '1px solid var(--ep-border)',
                padding: '18px 16px',
                boxShadow: 'var(--ep-shadow-card)',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '8px' }}>
                <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--ep-text-secondary)' }}>Total Users</span>
                <Users size={16} color="var(--ep-primary)" />
              </div>
              <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--ep-text-primary)', lineHeight: 1.2 }}>
                {overviewLoading ? '...' : (userMetrics.totalUsers ?? 0).toLocaleString()}
              </div>
              <div style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', marginTop: '4px' }}>
                Registered accounts
              </div>
            </div>

            {/* Active Users */}
            <div
              data-testid="metric-card-active-users"
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card)',
                border: '1px solid var(--ep-border)',
                padding: '18px 16px',
                boxShadow: 'var(--ep-shadow-card)',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '8px' }}>
                <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--ep-text-secondary)' }}>Active Users</span>
                <CheckCircle2 size={16} color="#2E7D32" />
              </div>
              <div style={{ fontSize: '24px', fontWeight: 700, color: '#2E7D32', lineHeight: 1.2 }}>
                {overviewLoading ? '...' : (userMetrics.activeUsers ?? 0).toLocaleString()}
              </div>
              <div style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', marginTop: '4px' }}>
                Operational & eligible
              </div>
            </div>

            {/* Inactive Users */}
            <div
              data-testid="metric-card-inactive-users"
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card)',
                border: '1px solid var(--ep-border)',
                padding: '18px 16px',
                boxShadow: 'var(--ep-shadow-card)',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '8px' }}>
                <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--ep-text-secondary)' }}>Inactive Users</span>
                <UserX size={16} color="var(--ep-danger)" />
              </div>
              <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--ep-danger)', lineHeight: 1.2 }}>
                {overviewLoading ? '...' : (userMetrics.inactiveUsers ?? 0).toLocaleString()}
              </div>
              <div style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', marginTop: '4px' }}>
                Deactivated accounts
              </div>
            </div>

            {/* Total Organizers */}
            <div
              data-testid="metric-card-organizers"
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card)',
                border: '1px solid var(--ep-border)',
                padding: '18px 16px',
                boxShadow: 'var(--ep-shadow-card)',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '8px' }}>
                <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--ep-text-secondary)' }}>Organizers</span>
                <UserCheck size={16} color="#0284C7" />
              </div>
              <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--ep-text-primary)', lineHeight: 1.2 }}>
                {overviewLoading ? '...' : (userMetrics.organizers ?? 0).toLocaleString()}
              </div>
              <div style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', marginTop: '4px' }}>
                Event organizers
              </div>
            </div>

            {/* Total Customers */}
            <div
              data-testid="metric-card-customers"
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card)',
                border: '1px solid var(--ep-border)',
                padding: '18px 16px',
                boxShadow: 'var(--ep-shadow-card)',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '8px' }}>
                <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--ep-text-secondary)' }}>Customers</span>
                <Users size={16} color="#7C3AED" />
              </div>
              <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--ep-text-primary)', lineHeight: 1.2 }}>
                {overviewLoading ? '...' : (userMetrics.customers ?? 0).toLocaleString()}
              </div>
              <div style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', marginTop: '4px' }}>
                Ticket buyers
              </div>
            </div>

            {/* Total Events */}
            <div
              data-testid="metric-card-events"
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card)',
                border: '1px solid var(--ep-border)',
                padding: '18px 16px',
                boxShadow: 'var(--ep-shadow-card)',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '8px' }}>
                <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--ep-text-secondary)' }}>Total Events</span>
                <FileCheck size={16} color="#EA580C" />
              </div>
              <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--ep-text-primary)', lineHeight: 1.2 }}>
                {overviewLoading ? '...' : eventsMetric.display}
              </div>
              <div
                style={{
                  fontSize: '11px',
                  color: eventsMetric.isAvailable ? 'var(--ep-text-secondary)' : '#C2410C',
                  marginTop: '4px',
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                  whiteSpace: 'nowrap',
                }}
                title={eventsMetric.subtext || (eventsMetric.isAvailable ? 'Platform events' : 'Service unavailable')}
              >
                {eventsMetric.subtext || (eventsMetric.isAvailable ? 'Platform events' : 'Service unavailable')}
              </div>
            </div>

            {/* Total Bookings */}
            <div
              data-testid="metric-card-bookings"
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card)',
                border: '1px solid var(--ep-border)',
                padding: '18px 16px',
                boxShadow: 'var(--ep-shadow-card)',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '8px' }}>
                <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--ep-text-secondary)' }}>Total Bookings</span>
                <Ticket size={16} color="#0D9488" />
              </div>
              <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--ep-text-primary)', lineHeight: 1.2 }}>
                {overviewLoading ? '...' : bookingsMetric.display}
              </div>
              <div
                style={{
                  fontSize: '11px',
                  color: bookingsMetric.isAvailable ? 'var(--ep-text-secondary)' : '#0F766E',
                  marginTop: '4px',
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                  whiteSpace: 'nowrap',
                }}
                title={bookingsMetric.subtext || (bookingsMetric.isAvailable ? 'Confirmed reservations' : 'Service unavailable')}
              >
                {bookingsMetric.subtext || (bookingsMetric.isAvailable ? 'Confirmed reservations' : 'Service unavailable')}
              </div>
            </div>
          </div>
        </section>

        {/* Dedicated User Management Section (EP-372 / EP-370 / EP-371) */}
        <section id="admin-user-management-section" aria-label="User Management Directory">
          <UserManagementSection
            token={getEffectiveToken()}
            currentAdminId={currentAdminId}
            onUserMutated={loadOverview}
          />
        </section>

        {/* Live Pending Event Submissions & Notifications Section (EP-151 / EP-32) */}
        <div
          style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            padding: '28px',
            boxShadow: 'var(--ep-shadow-card)',
          }}
        >
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              marginBottom: '20px',
              flexWrap: 'wrap',
              gap: '12px',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
              <Bell size={20} color="var(--ep-primary)" />
              <h2 style={{ fontSize: '18px', fontWeight: 700, color: 'var(--ep-text-primary)', margin: 0 }}>
                Pending Event Submissions
              </h2>
              {unreadCount > 0 && (
                <span
                  data-testid="admin-dashboard-unread-badge"
                  style={{
                    backgroundColor: '#FFF0E6',
                    color: 'var(--ep-primary)',
                    border: '1px solid #FFE0CC',
                    borderRadius: 'var(--ep-radius-pill)',
                    padding: '2px 8px',
                    fontSize: '12px',
                    fontWeight: 700,
                  }}
                >
                  {unreadCount} pending review
                </span>
              )}
            </div>

            <button
              type="button"
              data-testid="admin-notifications-refresh-btn"
              onClick={loadNotifications}
              disabled={notificationsLoading}
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                backgroundColor: 'transparent',
                border: '1px solid var(--ep-border)',
                borderRadius: 'var(--ep-radius-btn)',
                padding: '6px 14px',
                fontSize: '13px',
                fontWeight: 500,
                color: 'var(--ep-text-secondary)',
                cursor: notificationsLoading ? 'not-allowed' : 'pointer',
              }}
            >
              <RefreshCw size={14} className={notificationsLoading ? 'spin' : ''} />
              <span>Refresh</span>
            </button>
          </div>

          {/* Loading state */}
          {notificationsLoading && (
            <div data-testid="admin-notifications-loading" style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              {[1, 2].map((i) => (
                <div
                  key={i}
                  style={{
                    padding: '16px',
                    borderRadius: 'var(--ep-radius-btn)',
                    border: '1px solid var(--ep-border)',
                    backgroundColor: 'var(--ep-canvas)',
                  }}
                >
                  <div className="ep-skeleton" style={{ width: '40%', height: '18px', marginBottom: '8px' }} />
                  <div className="ep-skeleton" style={{ width: '60%', height: '14px' }} />
                </div>
              ))}
            </div>
          )}

          {/* Empty state */}
          {!notificationsLoading && notifications.length === 0 && (
            <div
              data-testid="admin-notifications-empty-state"
              style={{
                padding: '36px 20px',
                backgroundColor: 'var(--ep-canvas)',
                borderRadius: 'var(--ep-radius-btn)',
                border: '1px dashed var(--ep-border)',
                textAlign: 'center',
              }}
            >
              <CheckCircle2 size={32} color="#34C759" style={{ margin: '0 auto 8px auto' }} />
              <h3 style={{ fontSize: '15px', fontWeight: 600, color: 'var(--ep-text-primary)', margin: '0 0 4px 0' }}>
                No event submissions are waiting for review.
              </h3>
              <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: 0 }}>
                All organizer events have been evaluated. New submissions will appear here automatically.
              </p>
            </div>
          )}

          {/* Notifications List */}
          {!notificationsLoading && notifications.length > 0 && (
            <div data-testid="admin-notifications-list" style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              {notifications.map((item) => {
                const isPending = (item.reviewStatus || item.ReviewStatus || '').toLowerCase() === 'pending';
                const eventId = item.eventId || item.EventId;
                const notifId = item.id || item.Id;
                return (
                  <div
                    key={notifId}
                    data-testid={`admin-notification-card-${notifId}`}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      padding: '16px 20px',
                      backgroundColor: isPending ? '#FFF9F5' : '#ffffff',
                      border: `1px solid ${isPending ? '#FFE0CC' : 'var(--ep-border)'}`,
                      borderRadius: 'var(--ep-radius-btn)',
                      flexWrap: 'wrap',
                      gap: '12px',
                    }}
                  >
                    <div style={{ flex: 1, minWidth: '240px' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '4px' }}>
                        <span style={{ fontSize: '15px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                          {item.eventTitle || item.EventTitle || 'Untitled Event'}
                        </span>
                        <span
                          style={{
                            fontSize: '11px',
                            fontWeight: 700,
                            padding: '2px 8px',
                            borderRadius: 'var(--ep-radius-pill)',
                            textTransform: 'uppercase',
                            backgroundColor: isPending ? '#FFF0E6' : '#E8F5E9',
                            color: isPending ? 'var(--ep-primary)' : '#2E7D32',
                            border: `1px solid ${isPending ? '#FFE0CC' : '#C8E6C9'}`,
                          }}
                        >
                          {item.reviewStatus || item.ReviewStatus || 'Pending'}
                        </span>
                      </div>
                      <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: '0 0 6px 0' }}>
                        {item.message || item.Message || 'New event submitted for review.'}
                      </p>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                        <Clock size={13} />
                        <span>
                          {item.submittedAtUtc || item.SubmittedAtUtc
                            ? `Submitted on ${new Date(item.submittedAtUtc || item.SubmittedAtUtc).toLocaleDateString('en-US', {
                                month: 'short',
                                day: 'numeric',
                                year: 'numeric',
                                hour: '2-digit',
                                minute: '2-digit',
                              })}`
                            : 'Submitted recently'}
                        </span>
                      </div>
                    </div>

                    <div>
                      <Link
                        to={`/admin/events/pending?reviewId=${eventId}`}
                        data-testid={`admin-review-link-${notifId}`}
                        className="ep-btn-secondary"
                        style={{
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '6px',
                          fontSize: '13px',
                          fontWeight: 600,
                          padding: '8px 16px',
                          textDecoration: 'none',
                          borderRadius: 'var(--ep-radius-btn)',
                        }}
                      >
                        <span>{isPending ? 'Review Event' : 'View Submission'}</span>
                        <ChevronRight size={14} />
                      </Link>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      </main>
    </div>
  );
}