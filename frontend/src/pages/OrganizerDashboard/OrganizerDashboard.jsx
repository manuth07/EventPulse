import React, { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { useAuth } from '../../context/AuthContext';
import { getMySubmissions } from '../../services/eventService';
import { formatPrice } from '../../utils/currencyFormatter';
import {
  Calendar,
  Plus,
  LayoutDashboard,
  MapPin,
  RefreshCw,
  AlertCircle,
  Image as ImageIcon,
} from 'lucide-react';

function formatEventDateTime(dateString) {
  if (!dateString) return 'Date TBA';
  try {
    const date = new Date(dateString);
    const datePart = date.toLocaleDateString('en-GB', {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
    });
    const timePart = date.toLocaleTimeString('en-US', {
      hour: 'numeric',
      minute: '2-digit',
      hour12: true,
    });
    return `${datePart} • ${timePart}`;
  } catch (e) {
    return dateString;
  }
}

function formatSubmittedDate(dateString) {
  if (!dateString) return '';
  try {
    const date = new Date(dateString);
    return `Submitted ${date.toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
    })}`;
  } catch (e) {
    return `Submitted ${dateString}`;
  }
}

const STATUS_CONFIG = {
  Pending: {
    label: 'PENDING REVIEW',
    badgeBg: '#FFF0E6',
    badgeColor: 'var(--ep-primary)',
    badgeBorder: '#FFE0CC',
    note: 'Awaiting administrator review',
  },
  Approved: {
    label: 'APPROVED',
    badgeBg: '#E8F5E9',
    badgeColor: '#2E7D32',
    badgeBorder: '#C8E6C9',
    note: null,
  },
  Rejected: {
    label: 'REJECTED',
    badgeBg: '#FFEBEE',
    badgeColor: '#C62828',
    badgeBorder: '#FFCDD2',
    note: null,
  },
  Published: {
    label: 'PUBLISHED',
    badgeBg: '#E3F2FD',
    badgeColor: '#1565C0',
    badgeBorder: '#BBDEFB',
    note: null,
  },
};

export function OrganizerDashboard() {
  const { accessToken } = useAuth();
  const [events, setEvents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);

  const loadSubmissions = useCallback(async (isManualRefresh = false) => {
    if (isManualRefresh) {
      setRefreshing(true);
    } else {
      setLoading(true);
    }
    setError(null);

    const token = accessToken || sessionStorage.getItem('ep_access_token');
    if (!token) {
      setError('You are not authenticated. Please log in again.');
      setLoading(false);
      setRefreshing(false);
      return;
    }

    try {
      const data = await getMySubmissions(token);
      setEvents(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message || 'Unable to load your event submissions. Please try again.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [accessToken]);

  useEffect(() => {
    loadSubmissions();
  }, [loadSubmissions]);

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
              backgroundColor: '#FFF0E6',
              color: 'var(--ep-primary)',
              padding: '4px 10px',
              borderRadius: 'var(--ep-radius-pill)',
              fontSize: '12px',
              fontWeight: 600,
              marginBottom: '8px',
            }}>
              <LayoutDashboard size={13} />
              <span>Organizer Workspace</span>
            </div>
            <h1 style={{
              fontSize: '28px',
              fontWeight: 700,
              color: 'var(--ep-text-primary)',
              margin: 0,
              letterSpacing: '-0.01em',
            }}>
              Organizer Dashboard
            </h1>
            <p style={{
              fontSize: '14px',
              color: 'var(--ep-text-secondary)',
              marginTop: '4px',
              margin: 0,
            }}>
              Manage your event submissions and upcoming events.
            </p>
          </div>

          <Link
            to="/events/create"
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
            <Plus size={16} />
            <span>Create Event</span>
          </Link>
        </div>

        {/* Dashboard Shell Content */}
        <div style={{
          backgroundColor: '#ffffff',
          borderRadius: 'var(--ep-radius-card)',
          border: '1px solid var(--ep-border)',
          padding: '32px',
          boxShadow: 'var(--ep-shadow-card)',
        }}>
          <div style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            marginBottom: '16px',
            flexWrap: 'wrap',
            gap: '12px',
          }}>
            <div style={{
              display: 'flex',
              alignItems: 'center',
              gap: '12px',
            }}>
              <Calendar size={20} color="var(--ep-primary)" />
              <h2 style={{
                fontSize: '18px',
                fontWeight: 600,
                color: 'var(--ep-text-primary)',
                margin: 0,
              }}>
                My Event Submissions
              </h2>
            </div>

            <button
              type="button"
              onClick={() => loadSubmissions(true)}
              disabled={refreshing || loading}
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
                cursor: refreshing || loading ? 'not-allowed' : 'pointer',
                transition: 'var(--ep-transition)',
              }}
            >
              <RefreshCw
                size={14}
                style={{
                  animation: refreshing ? 'spin 1s linear infinite' : 'none',
                }}
              />
              <span>{refreshing ? 'Refreshing...' : 'Refresh'}</span>
            </button>
          </div>

          <p style={{
            fontSize: '14px',
            color: 'var(--ep-text-secondary)',
            marginBottom: '24px',
            lineHeight: 1.5,
          }}>
            Events submitted here undergo Administrator review before being published to public visitors.
          </p>

          {/* Loading State: Skeletons */}
          {loading && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              {[1, 2].map((key) => (
                <div
                  key={key}
                  style={{
                    padding: '20px',
                    borderRadius: 'var(--ep-radius-card)',
                    border: '1px solid var(--ep-border)',
                    backgroundColor: '#ffffff',
                    display: 'flex',
                    gap: '20px',
                    alignItems: 'center',
                  }}
                >
                  <div
                    className="ep-skeleton"
                    style={{
                      width: '120px',
                      height: '120px',
                      borderRadius: 'var(--ep-radius-badge)',
                      flexShrink: 0,
                    }}
                  />
                  <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: '12px' }}>
                    <div className="ep-skeleton" style={{ width: '45%', height: '22px' }} />
                    <div className="ep-skeleton" style={{ width: '65%', height: '14px' }} />
                    <div className="ep-skeleton" style={{ width: '35%', height: '14px' }} />
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Error State: Explicit error container */}
          {!loading && error && (
            <div style={{
              padding: '24px',
              backgroundColor: '#FFF5F5',
              borderRadius: 'var(--ep-radius-container, 12px)',
              border: '1px solid #FED7D7',
              display: 'flex',
              alignItems: 'flex-start',
              gap: '16px',
            }}>
              <AlertCircle size={22} color="var(--ep-danger)" style={{ marginTop: '2px', flexShrink: 0 }} />
              <div style={{ flex: 1 }}>
                <h4 style={{
                  fontSize: '15px',
                  fontWeight: 600,
                  color: 'var(--ep-text-primary)',
                  margin: '0 0 6px 0',
                }}>
                  Unable to load your event submissions
                </h4>
                <p style={{
                  fontSize: '13px',
                  color: 'var(--ep-text-secondary)',
                  margin: '0 0 16px 0',
                  lineHeight: 1.5,
                }}>
                  {error}
                </p>
                <button
                  type="button"
                  onClick={() => loadSubmissions(false)}
                  className="ep-btn-secondary"
                  style={{ fontSize: '13px', padding: '6px 16px' }}
                >
                  Retry
                </button>
              </div>
            </div>
          )}

          {/* Empty State: Only when successfully loaded and events count is 0 */}
          {!loading && !error && events.length === 0 && (
            <div style={{
              padding: '40px 24px',
              backgroundColor: 'var(--ep-canvas)',
              borderRadius: 'var(--ep-radius-container, 12px)',
              border: '1px dashed var(--ep-border)',
              textAlign: 'center',
            }}>
              <p style={{
                fontSize: '15px',
                fontWeight: 500,
                color: 'var(--ep-text-secondary)',
                margin: '0 0 16px 0',
              }}>
                No event submissions yet.
              </p>
              <Link
                to="/events/create"
                className="ep-btn-primary"
                style={{
                  fontSize: '13px',
                  padding: '8px 18px',
                  textDecoration: 'none',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '6px',
                }}
              >
                <Plus size={14} />
                <span>Submit your first event</span>
              </Link>
            </div>
          )}

          {/* Submissions List */}
          {!loading && !error && events.length > 0 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              {events.map((item) => {
                const statusInfo = STATUS_CONFIG[item.status] || {
                  label: item.status?.toUpperCase() || 'UNKNOWN',
                  badgeBg: 'var(--ep-canvas)',
                  badgeColor: 'var(--ep-text-primary)',
                  badgeBorder: 'var(--ep-border)',
                  note: null,
                };

                return (
                  <div
                    key={item.id}
                    className="ep-card"
                    style={{
                      padding: '20px',
                      borderRadius: 'var(--ep-radius-card)',
                      border: '1px solid var(--ep-border)',
                      backgroundColor: '#ffffff',
                    }}
                  >
                    <div style={{
                      display: 'flex',
                      gap: '20px',
                      alignItems: 'flex-start',
                      flexWrap: 'wrap',
                    }}>
                      {/* Poster Thumbnail */}
                      <div style={{
                        width: '120px',
                        height: '120px',
                        borderRadius: 'var(--ep-radius-badge)',
                        overflow: 'hidden',
                        backgroundColor: 'var(--ep-soft-accent)',
                        flexShrink: 0,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        border: '1px solid var(--ep-border)',
                      }}>
                        {item.imageUrl ? (
                          <img
                            src={item.imageUrl}
                            alt={item.title}
                            style={{
                              width: '100%',
                              height: '100%',
                              objectFit: 'cover',
                            }}
                            onError={(e) => {
                              // If image fails to load, replace with poster icon
                              e.currentTarget.style.display = 'none';
                              e.currentTarget.parentElement.innerHTML = '<svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="var(--ep-primary)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="18" height="18" x="3" y="3" rx="2" ry="2"/><circle cx="9" cy="9" r="2"/><path d="m21 15-3.086-3.086a2 2 0 0 0-2.828 0L6 21"/></svg>';
                            }}
                          />
                        ) : (
                          <ImageIcon size={28} color="var(--ep-primary)" />
                        )}
                      </div>

                      {/* Details Column */}
                      <div style={{
                        flex: 1,
                        minWidth: '240px',
                        display: 'flex',
                        flexDirection: 'column',
                        justifyContent: 'space-between',
                      }}>
                        <div>
                          <div style={{
                            display: 'flex',
                            justifyContent: 'space-between',
                            alignItems: 'flex-start',
                            gap: '12px',
                            flexWrap: 'wrap',
                            marginBottom: '8px',
                          }}>
                            <h3 style={{
                              fontSize: '18px',
                              fontWeight: 700,
                              color: 'var(--ep-text-primary)',
                              margin: 0,
                            }}>
                              {item.title}
                            </h3>

                            {/* Status Chip */}
                            <span style={{
                              backgroundColor: statusInfo.badgeBg,
                              color: statusInfo.badgeColor,
                              border: `1px solid ${statusInfo.badgeBorder}`,
                              borderRadius: 'var(--ep-radius-pill)',
                              padding: '4px 12px',
                              fontSize: '12px',
                              fontWeight: 700,
                              letterSpacing: '0.04em',
                              textTransform: 'uppercase',
                              display: 'inline-block',
                            }}>
                              {statusInfo.label}
                            </span>
                          </div>

                          <div style={{
                            display: 'flex',
                            flexDirection: 'column',
                            gap: '6px',
                            marginBottom: '14px',
                          }}>
                            <div style={{
                              display: 'flex',
                              alignItems: 'center',
                              gap: '8px',
                              fontSize: '13px',
                              color: 'var(--ep-text-secondary)',
                            }}>
                              <MapPin size={15} color="var(--ep-text-secondary)" style={{ flexShrink: 0 }} />
                              <span>{item.venue || 'Location TBA'}</span>
                            </div>

                            <div style={{
                              display: 'flex',
                              alignItems: 'center',
                              gap: '8px',
                              fontSize: '13px',
                              color: 'var(--ep-text-secondary)',
                            }}>
                              <Calendar size={15} color="var(--ep-text-secondary)" style={{ flexShrink: 0 }} />
                              <span>{formatEventDateTime(item.eventDate)}</span>
                            </div>
                          </div>
                        </div>

                        {/* Card Footer */}
                        <div style={{
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'space-between',
                          paddingTop: '12px',
                          borderTop: '1px solid var(--ep-border)',
                          flexWrap: 'wrap',
                          gap: '12px',
                        }}>
                          <div style={{
                            fontSize: '15px',
                            fontWeight: 700,
                            color: 'var(--ep-primary)',
                          }}>
                            {formatPrice(item.price)}
                          </div>

                          <div style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: '16px',
                            fontSize: '12px',
                            color: 'var(--ep-text-secondary)',
                            flexWrap: 'wrap',
                          }}>
                            {statusInfo.note && (
                              <span style={{ fontStyle: 'italic' }}>
                                {statusInfo.note}
                              </span>
                            )}
                            {item.createdAt && (
                              <span>{formatSubmittedDate(item.createdAt)}</span>
                            )}
                          </div>
                        </div>
                      </div>
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
