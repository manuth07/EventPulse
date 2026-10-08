import React, { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { useAuth } from '../../context/AuthContext';
import { ShieldCheck, FileCheck, Users, UserCheck, Bell, Clock, CheckCircle2, AlertCircle, RefreshCw, ChevronRight } from 'lucide-react';
import { getAdminNotifications, getAdminNotificationsUnreadCount } from '../../services/eventService';

export function AdminDashboard() {
  const { accessToken } = useAuth();
  const [notifications, setNotifications] = useState([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);

  const getEffectiveToken = useCallback(() => {
    return accessToken || sessionStorage.getItem('ep_access_token');
  }, [accessToken]);

  const loadNotifications = useCallback(async (isManualRefresh = false) => {
    if (isManualRefresh) {
      setRefreshing(true);
    } else {
      setLoading(true);
    }
    setError(null);

    const token = getEffectiveToken();
    if (!token) {
      setError('You are not authenticated as an Administrator.');
      setLoading(false);
      setRefreshing(false);
      return;
    }

    try {
      const [list, count] = await Promise.all([
        getAdminNotifications(token),
        getAdminNotificationsUnreadCount(token).catch(() => 0),
      ]);
      setNotifications(Array.isArray(list) ? list : []);
      setUnreadCount(typeof count === 'number' ? count : 0);
    } catch (err) {
      setError(err.message || 'Unable to load event review notifications.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [getEffectiveToken]);

  useEffect(() => {
    loadNotifications();
  }, [loadNotifications]);

  return (
    <div style={{
      minHeight: '100vh',
      backgroundColor: 'var(--ep-canvas)',
      display: 'flex',
      flexDirection: 'column',
    }}>
      <Header />
      <main className="container" style={{
        flex: 1,
        paddingTop: '40px',
        paddingBottom: '64px',
      }}>
        {/* Header Section */}
        <div style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          marginBottom: '32px',
          flexWrap: 'wrap',
          gap: '16px',
        }}>
          <div>
            <div style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '6px',
              backgroundColor: 'var(--ep-canvas)',
              color: 'var(--ep-text-primary)',
              padding: '4px 10px',
              borderRadius: 'var(--ep-radius-pill)',
              fontSize: '12px',
              fontWeight: 600,
              border: '1px solid var(--ep-border)',
              marginBottom: '8px',
            }}>
              <ShieldCheck size={13} color="var(--ep-primary)" />
              <span>Platform Administration</span>
            </div>
            <h1 style={{
              fontSize: '28px',
              fontWeight: 700,
              color: 'var(--ep-text-primary)',
              margin: 0,
              letterSpacing: '-0.01em',
            }}>
              Admin Dashboard
            </h1>
            <p style={{
              fontSize: '14px',
              color: 'var(--ep-text-secondary)',
              marginTop: '4px',
              margin: 0,
            }}>
              Review EventPulse platform activity and pending event submissions.
            </p>
          </div>

          <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap' }}>
            <Link
              to="/admin/organizer-applications"
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
              <UserCheck size={16} />
              <span>Organizer Applications</span>
            </Link>

            <Link
              to="/admin/events/pending"
              className="ep-btn-secondary"
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
              <FileCheck size={16} />
              <span>Pending Events</span>
            </Link>
          </div>
        </div>

        {/* Overview Grid */}
        <div style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))',
          gap: '20px',
          marginBottom: '32px',
        }}>
          <div style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            padding: '24px',
            boxShadow: 'var(--ep-shadow-card)',
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '12px' }}>
              <UserCheck size={20} color="var(--ep-primary)" />
              <h3 style={{ fontSize: '15px', fontWeight: 600, color: 'var(--ep-text-primary)', margin: 0 }}>
                Organizer Applications
              </h3>
            </div>
            <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: '0 0 16px 0', lineHeight: 1.5 }}>
              Review customer requests to list events, verify organizer details, and grant Organizer role permissions.
            </p>
            <Link
              to="/admin/organizer-applications"
              className="ep-btn-secondary"
              style={{ fontSize: '13px', padding: '6px 14px', textDecoration: 'none', display: 'inline-block' }}
            >
              Review Applications →
            </Link>
          </div>

          <div style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            padding: '24px',
            boxShadow: 'var(--ep-shadow-card)',
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '12px' }}>
              <FileCheck size={20} color="var(--ep-primary)" />
              <h3 style={{ fontSize: '15px', fontWeight: 600, color: 'var(--ep-text-primary)', margin: 0 }}>
                Event Approvals
              </h3>
            </div>
            <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: '0 0 16px 0', lineHeight: 1.5 }}>
              Review Organizer event submissions and publish approved events to the platform.
            </p>
            <Link
              to="/admin/events/pending"
              className="ep-btn-secondary"
              style={{ fontSize: '13px', padding: '6px 14px', textDecoration: 'none', display: 'inline-block' }}
            >
              Review Submissions →
            </Link>
          </div>

          <div style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            padding: '24px',
            boxShadow: 'var(--ep-shadow-card)',
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '12px' }}>
              <Users size={20} color="var(--ep-text-secondary)" />
              <h3 style={{ fontSize: '15px', fontWeight: 600, color: 'var(--ep-text-primary)', margin: 0 }}>
                Platform Security & Role Governance
              </h3>
            </div>
            <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: 0, lineHeight: 1.5 }}>
              Role-based authorization active across Identity Service and Event Service backend policies.
            </p>
          </div>
        </div>

        {/* Live Pending Event Submissions & Notifications Section (EP-151 / EP-32) */}
        <div style={{
          backgroundColor: '#ffffff',
          borderRadius: 'var(--ep-radius-card)',
          border: '1px solid var(--ep-border)',
          padding: '28px',
          boxShadow: 'var(--ep-shadow-card)',
        }}>
          <div style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            marginBottom: '20px',
            flexWrap: 'wrap',
            gap: '12px',
          }}>
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
              onClick={() => loadNotifications(true)}
              disabled={loading || refreshing}
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
                cursor: loading || refreshing ? 'not-allowed' : 'pointer',
              }}
            >
              <RefreshCw size={14} style={{ animation: refreshing ? 'spin 1s linear infinite' : 'none' }} />
              <span>{refreshing ? 'Refreshing...' : 'Refresh'}</span>
            </button>
          </div>

          {/* Loading state */}
          {loading && (
            <div data-testid="admin-notifications-loading" style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              {[1, 2].map((i) => (
                <div key={i} style={{ padding: '16px', borderRadius: 'var(--ep-radius-btn)', border: '1px solid var(--ep-border)', backgroundColor: 'var(--ep-canvas)' }}>
                  <div className="ep-skeleton" style={{ width: '40%', height: '18px', marginBottom: '8px' }} />
                  <div className="ep-skeleton" style={{ width: '60%', height: '14px' }} />
                </div>
              ))}
            </div>
          )}

          {/* Error state */}
          {!loading && error && (
            <div
              data-testid="admin-notifications-error"
              style={{
                padding: '16px 20px',
                backgroundColor: '#FFF5F5',
                borderRadius: 'var(--ep-radius-btn)',
                border: '1px solid #FED7D7',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                gap: '12px',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                <AlertCircle size={18} color="var(--ep-danger)" />
                <span style={{ fontSize: '13px', color: 'var(--ep-text-primary)' }}>{error}</span>
              </div>
              <button
                type="button"
                data-testid="admin-notifications-retry-btn"
                onClick={() => loadNotifications(false)}
                className="ep-btn-secondary"
                style={{ fontSize: '12px', padding: '4px 12px' }}
              >
                Retry
              </button>
            </div>
          )}

          {/* Empty state */}
          {!loading && !error && notifications.length === 0 && (
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
          {!loading && !error && notifications.length > 0 && (
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