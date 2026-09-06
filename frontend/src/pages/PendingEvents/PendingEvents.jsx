import React, { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { useAuth } from '../../context/AuthContext';
import {
  getPendingEvents,
  getPendingEventById,
  approveEvent,
  rejectEvent,
} from '../../services/eventService';
import { formatPrice } from '../../utils/currencyFormatter';
import {
  ArrowLeft,
  Clock,
  Calendar,
  MapPin,
  RefreshCw,
  AlertCircle,
  CheckCircle2,
  XCircle,
  X,
  Image as ImageIcon,
  User,
  Info,
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
      year: 'numeric',
    })}`;
  } catch (e) {
    return `Submitted ${dateString}`;
  }
}

export function PendingEvents() {
  const { accessToken } = useAuth();
  const [events, setEvents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);
  const [feedback, setFeedback] = useState(null);

  // Review Modal / Drawer state
  const [selectedEvent, setSelectedEvent] = useState(null);
  const [loadingDetails, setLoadingDetails] = useState(false);
  const [confirmMode, setConfirmMode] = useState(null); // 'approve' | 'reject' | null
  const [rejectionNotes, setRejectionNotes] = useState('');
  const [actionInProgress, setActionInProgress] = useState(false);
  const [actionError, setActionError] = useState(null);

  const getEffectiveToken = useCallback(() => {
    return accessToken || sessionStorage.getItem('ep_access_token');
  }, [accessToken]);

  const loadPendingSubmissions = useCallback(async (isManualRefresh = false) => {
    if (isManualRefresh) {
      setRefreshing(true);
    } else {
      setLoading(true);
    }
    setError(null);

    const token = getEffectiveToken();
    if (!token) {
      setError('You are not authenticated as an Administrator. Please log in.');
      setLoading(false);
      setRefreshing(false);
      return;
    }

    try {
      const data = await getPendingEvents(token);
      setEvents(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message || 'Unable to load pending event submissions.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [getEffectiveToken]);

  useEffect(() => {
    loadPendingSubmissions();
  }, [loadPendingSubmissions]);

  const handleOpenReview = async (item) => {
    setActionError(null);
    setConfirmMode(null);
    setRejectionNotes('');
    setSelectedEvent(item);
    setLoadingDetails(true);

    const token = getEffectiveToken();
    if (token) {
      try {
        const freshDetails = await getPendingEventById(item.id, token);
        if (freshDetails) {
          setSelectedEvent(freshDetails);
        }
      } catch (err) {
        // Fallback to item from list if single fetch fails
      } finally {
        setLoadingDetails(false);
      }
    } else {
      setLoadingDetails(false);
    }
  };

  const handleCloseReview = () => {
    if (actionInProgress) return;
    setSelectedEvent(null);
    setConfirmMode(null);
    setActionError(null);
    setRejectionNotes('');
  };

  const handleApprove = async () => {
    if (!selectedEvent || actionInProgress) return;
    setActionInProgress(true);
    setActionError(null);

    const token = getEffectiveToken();
    try {
      await approveEvent(selectedEvent.id, token);
      const approvedTitle = selectedEvent.title;
      handleCloseReview();
      setFeedback({
        type: 'success',
        message: `Event approved successfully. It is ready for publishing. ("${approvedTitle}")`,
      });
      // Remove approved event from pending list
      setEvents((prev) => prev.filter((e) => e.id !== selectedEvent.id));
    } catch (err) {
      setActionError(err.message || 'Failed to approve event.');
    } finally {
      setActionInProgress(false);
    }
  };

  const handleReject = async () => {
    if (!selectedEvent || actionInProgress) return;
    if (!rejectionNotes.trim()) {
      setActionError('Rejection feedback is required. Please explain what needs to be corrected.');
      return;
    }
    setActionInProgress(true);
    setActionError(null);

    const token = getEffectiveToken();
    try {
      await rejectEvent(selectedEvent.id, token, rejectionNotes.trim());
      const rejectedTitle = selectedEvent.title;
      handleCloseReview();
      setFeedback({
        type: 'info',
        message: `Event submission rejected. ("${rejectedTitle}")`,
      });
      // Remove rejected event from pending list
      setEvents((prev) => prev.filter((e) => e.id !== selectedEvent.id));
    } catch (err) {
      setActionError(err.message || 'Failed to reject event.');
    } finally {
      setActionInProgress(false);
    }
  };

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
        paddingTop: '32px',
        paddingBottom: '64px',
      }}>
        {/* Back Link */}
        <Link
          to="/admin"
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: '6px',
            fontSize: '13px',
            fontWeight: 500,
            color: 'var(--ep-text-secondary)',
            textDecoration: 'none',
            marginBottom: '24px',
          }}
        >
          <ArrowLeft size={14} />
          <span>Back to Admin Dashboard</span>
        </Link>

        {/* Action Feedback Banner */}
        {feedback && (
          <div style={{
            marginBottom: '24px',
            padding: '16px 20px',
            borderRadius: 'var(--ep-radius-container, 12px)',
            backgroundColor: feedback.type === 'success' ? '#E8F5E9' : '#FFF0E6',
            border: `1px solid ${feedback.type === 'success' ? '#C8E6C9' : '#FFE0CC'}`,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: '12px',
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
              {feedback.type === 'success' ? (
                <CheckCircle2 size={18} color="#2E7D32" />
              ) : (
                <Info size={18} color="var(--ep-primary)" />
              )}
              <span style={{
                fontSize: '14px',
                fontWeight: 600,
                color: feedback.type === 'success' ? '#1B5E20' : 'var(--ep-text-primary)',
              }}>
                {feedback.message}
              </span>
            </div>
            <button
              type="button"
              onClick={() => setFeedback(null)}
              style={{
                background: 'none',
                border: 'none',
                cursor: 'pointer',
                color: 'var(--ep-text-secondary)',
                padding: '4px',
              }}
              aria-label="Dismiss feedback"
            >
              <X size={16} />
            </button>
          </div>
        )}

        {/* Content Card */}
        <div style={{
          backgroundColor: '#ffffff',
          borderRadius: 'var(--ep-radius-card)',
          border: '1px solid var(--ep-border)',
          padding: '32px',
          boxShadow: 'var(--ep-shadow-card)',
        }}>
          {/* Section Header */}
          <div style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            marginBottom: '8px',
            flexWrap: 'wrap',
            gap: '16px',
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
              <Clock size={22} color="var(--ep-primary)" />
              <h1 style={{
                fontSize: '24px',
                fontWeight: 700,
                color: 'var(--ep-text-primary)',
                margin: 0,
                letterSpacing: '-0.005em',
              }}>
                Pending Event Submissions
              </h1>
            </div>

            <button
              type="button"
              onClick={() => loadPendingSubmissions(true)}
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
            margin: '0 0 28px 0',
          }}>
            Review and verify organizer event submissions before they can be published to visitors.
          </p>

          {/* Loading Skeletons */}
          {loading && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              {[1, 2, 3].map((key) => (
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
                    <div className="ep-skeleton" style={{ width: '40%', height: '20px' }} />
                    <div className="ep-skeleton" style={{ width: '60%', height: '14px' }} />
                    <div className="ep-skeleton" style={{ width: '30%', height: '14px' }} />
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Error Container */}
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
                  Unable to load pending submissions
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
                  onClick={() => loadPendingSubmissions(false)}
                  className="ep-btn-secondary"
                  style={{ fontSize: '13px', padding: '6px 16px' }}
                >
                  Retry
                </button>
              </div>
            </div>
          )}

          {/* Empty State */}
          {!loading && !error && events.length === 0 && (
            <div style={{
              padding: '48px 24px',
              backgroundColor: 'var(--ep-canvas)',
              borderRadius: 'var(--ep-radius-container, 12px)',
              border: '1px dashed var(--ep-border)',
              textAlign: 'center',
            }}>
              <CheckCircle2 size={32} color="var(--ep-primary)" style={{ margin: '0 auto 12px auto' }} />
              <h3 style={{
                fontSize: '16px',
                fontWeight: 600,
                color: 'var(--ep-text-primary)',
                margin: '0 0 6px 0',
              }}>
                No pending submissions
              </h3>
              <p style={{
                fontSize: '13px',
                color: 'var(--ep-text-secondary)',
                margin: 0,
                lineHeight: 1.5,
              }}>
                All organizer event submissions have been reviewed. New submissions will appear here for verification.
              </p>
            </div>
          )}

          {/* Pending Submissions Queue */}
          {!loading && !error && events.length > 0 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              {events.map((item) => (
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
                            e.currentTarget.style.display = 'none';
                            e.currentTarget.parentElement.innerHTML = '<svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="var(--ep-primary)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="18" height="18" x="3" y="3" rx="2" ry="2"/><circle cx="9" cy="9" r="2"/><path d="m21 15-3.086-3.086a2 2 0 0 0-2.828 0L6 21"/></svg>';
                          }}
                        />
                      ) : (
                        <ImageIcon size={28} color="var(--ep-primary)" />
                      )}
                    </div>

                    {/* Details */}
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

                          {/* Pending Review Badge */}
                          <span style={{
                            backgroundColor: '#FFF0E6',
                            color: 'var(--ep-primary)',
                            border: '1px solid #FFE0CC',
                            borderRadius: 'var(--ep-radius-pill)',
                            padding: '4px 12px',
                            fontSize: '12px',
                            fontWeight: 700,
                            letterSpacing: '0.04em',
                            textTransform: 'uppercase',
                            display: 'inline-block',
                          }}>
                            PENDING REVIEW
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
                            <span>{item.venue || 'Venue TBA'}</span>
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

                      {/* Card Footer with Price, Submitted Date & Review Action */}
                      <div style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        paddingTop: '12px',
                        borderTop: '1px solid var(--ep-border)',
                        flexWrap: 'wrap',
                        gap: '12px',
                      }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
                          <span style={{
                            fontSize: '15px',
                            fontWeight: 700,
                            color: 'var(--ep-primary)',
                          }}>
                            {formatPrice(item.price)}
                          </span>
                          {item.createdAt && (
                            <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                              {formatSubmittedDate(item.createdAt)}
                            </span>
                          )}
                        </div>

                        <button
                          type="button"
                          onClick={() => handleOpenReview(item)}
                          className="ep-btn-secondary"
                          style={{
                            fontSize: '13px',
                            padding: '6px 16px',
                            fontWeight: 600,
                            cursor: 'pointer',
                          }}
                        >
                          Review Submission
                        </button>
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </main>

      {/* Review Modal Panel */}
      {selectedEvent && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="review-modal-title"
          style={{
            position: 'fixed',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            backgroundColor: 'rgba(0, 0, 0, 0.45)',
            backdropFilter: 'blur(3px)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            padding: '24px',
            zIndex: 1000,
          }}
          onClick={(e) => {
            if (e.target === e.currentTarget && !actionInProgress) {
              handleCloseReview();
            }
          }}
        >
          <div style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            boxShadow: 'var(--ep-shadow-modal, 0 12px 36px rgba(0,0,0,0.12))',
            width: '100%',
            maxWidth: '640px',
            maxHeight: '90vh',
            display: 'flex',
            flexDirection: 'column',
            overflow: 'hidden',
          }}>
            {/* Modal Header */}
            <div style={{
              padding: '20px 24px',
              borderBottom: '1px solid var(--ep-border)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
            }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <Clock size={18} color="var(--ep-primary)" />
                <h2
                  id="review-modal-title"
                  style={{
                    fontSize: '17px',
                    fontWeight: 700,
                    color: 'var(--ep-text-primary)',
                    margin: 0,
                  }}
                >
                  Review Event Submission
                </h2>
              </div>
              <button
                type="button"
                onClick={handleCloseReview}
                disabled={actionInProgress}
                style={{
                  background: 'none',
                  border: 'none',
                  cursor: actionInProgress ? 'not-allowed' : 'pointer',
                  color: 'var(--ep-text-secondary)',
                  padding: '4px',
                  display: 'flex',
                  alignItems: 'center',
                }}
                aria-label="Close review dialog"
              >
                <X size={20} />
              </button>
            </div>

            {/* Modal Body */}
            <div style={{
              padding: '24px',
              overflowY: 'auto',
              display: 'flex',
              flexDirection: 'column',
              gap: '20px',
            }}>
              {/* Error Banner inside Modal */}
              {actionError && (
                <div style={{
                  padding: '12px 16px',
                  backgroundColor: '#FFF5F5',
                  border: '1px solid #FED7D7',
                  borderRadius: 'var(--ep-radius-container, 8px)',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '10px',
                  fontSize: '13px',
                  color: 'var(--ep-danger)',
                }}>
                  <AlertCircle size={16} style={{ flexShrink: 0 }} />
                  <span>{actionError}</span>
                </div>
              )}

              {/* Image Assets Preview: Poster & Cover */}
              <div style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
                gap: '16px',
                alignItems: 'stretch',
              }}>
                {/* Poster Preview */}
                <div>
                  <div style={{
                    fontSize: '11px',
                    fontWeight: 700,
                    color: 'var(--ep-text-secondary)',
                    marginBottom: '6px',
                    textTransform: 'uppercase',
                    letterSpacing: '0.04em',
                  }}>
                    Event Poster (Portrait)
                  </div>
                  <div style={{
                    width: '100%',
                    height: '210px',
                    borderRadius: 'var(--ep-radius-container, 10px)',
                    overflow: 'hidden',
                    backgroundColor: 'var(--ep-canvas)',
                    border: '1px solid var(--ep-border)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}>
                    {selectedEvent.imageUrl ? (
                      <img
                        src={selectedEvent.imageUrl}
                        alt={`${selectedEvent.title} poster`}
                        style={{
                          width: '100%',
                          height: '100%',
                          objectFit: 'cover',
                          display: 'block',
                        }}
                        onError={(e) => {
                          e.currentTarget.style.display = 'none';
                          e.currentTarget.parentElement.innerHTML = '<div style="display:flex;align-items:center;gap:8px;color:var(--ep-text-secondary);font-size:12px;padding:8px;text-align:center;"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect width="18" height="18" x="3" y="3" rx="2" ry="2"/><circle cx="9" cy="9" r="2"/><path d="m21 15-3.086-3.086a2 2 0 0 0-2.828 0L6 21"/></svg><span>Poster image unavailable</span></div>';
                        }}
                      />
                    ) : (
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: 'var(--ep-text-secondary)', fontSize: '12px' }}>
                        <ImageIcon size={20} />
                        <span>No poster image attached</span>
                      </div>
                    )}
                  </div>
                </div>

                {/* Cover Preview */}
                <div style={{ display: 'flex', flexDirection: 'column' }}>
                  <div style={{
                    fontSize: '11px',
                    fontWeight: 700,
                    color: 'var(--ep-text-secondary)',
                    marginBottom: '6px',
                    textTransform: 'uppercase',
                    letterSpacing: '0.04em',
                  }}>
                    Event Cover / Banner (Wide)
                  </div>
                  <div style={{
                    width: '100%',
                    flex: 1,
                    minHeight: '140px',
                    borderRadius: 'var(--ep-radius-container, 10px)',
                    overflow: 'hidden',
                    backgroundColor: 'var(--ep-canvas)',
                    border: '1px solid var(--ep-border)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    padding: selectedEvent.coverUrl ? '0' : '16px',
                    boxSizing: 'border-box',
                  }}>
                    {selectedEvent.coverUrl ? (
                      <img
                        src={selectedEvent.coverUrl}
                        alt={`${selectedEvent.title} cover banner`}
                        style={{
                          width: '100%',
                          height: '100%',
                          maxHeight: '210px',
                          objectFit: 'cover',
                          display: 'block',
                        }}
                        onError={(e) => {
                          e.currentTarget.style.display = 'none';
                          e.currentTarget.parentElement.innerHTML = '<div style="display:flex;align-items:center;gap:8px;color:var(--ep-text-secondary);font-size:12px;"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect width="18" height="18" x="3" y="3" rx="2" ry="2"/><circle cx="9" cy="9" r="2"/><path d="m21 15-3.086-3.086a2 2 0 0 0-2.828 0L6 21"/></svg><span>Cover image unavailable</span></div>';
                        }}
                      />
                    ) : (
                      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '8px', color: 'var(--ep-text-secondary)', fontSize: '13px', textAlign: 'center' }}>
                        <ImageIcon size={22} />
                        <span>No dedicated event cover uploaded.</span>
                      </div>
                    )}
                  </div>
                </div>
              </div>

              {/* Title & Status */}
              <div>
                <div style={{
                  display: 'flex',
                  alignItems: 'flex-start',
                  justifyContent: 'space-between',
                  gap: '12px',
                  marginBottom: '6px',
                }}>
                  <h3 style={{
                    fontSize: '20px',
                    fontWeight: 700,
                    color: 'var(--ep-text-primary)',
                    margin: 0,
                  }}>
                    {selectedEvent.title}
                  </h3>
                  <span style={{
                    backgroundColor: '#FFF0E6',
                    color: 'var(--ep-primary)',
                    border: '1px solid #FFE0CC',
                    borderRadius: 'var(--ep-radius-pill)',
                    padding: '3px 10px',
                    fontSize: '11px',
                    fontWeight: 700,
                    letterSpacing: '0.04em',
                    textTransform: 'uppercase',
                    whiteSpace: 'nowrap',
                  }}>
                    {selectedEvent.status?.toUpperCase() || 'PENDING'}
                  </span>
                </div>
              </div>

              {/* Metadata Grid */}
              <div style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
                gap: '12px',
                padding: '16px',
                backgroundColor: 'var(--ep-canvas)',
                borderRadius: 'var(--ep-radius-container, 10px)',
                border: '1px solid var(--ep-border)',
                fontSize: '13px',
              }}>
                <div>
                  <div style={{ color: 'var(--ep-text-secondary)', marginBottom: '2px', fontSize: '12px' }}>Event Date & Time</div>
                  <div style={{ fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                    {formatEventDateTime(selectedEvent.eventDate)}
                  </div>
                </div>

                <div>
                  <div style={{ color: 'var(--ep-text-secondary)', marginBottom: '2px', fontSize: '12px' }}>Venue</div>
                  <div style={{ fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                    {selectedEvent.venue || 'TBA'}
                  </div>
                </div>

                <div>
                  <div style={{ color: 'var(--ep-text-secondary)', marginBottom: '2px', fontSize: '12px' }}>Ticket Price (LKR)</div>
                  <div style={{ fontWeight: 700, color: 'var(--ep-primary)' }}>
                    {formatPrice(selectedEvent.price)}
                  </div>
                </div>

                <div>
                  <div style={{ color: 'var(--ep-text-secondary)', marginBottom: '2px', fontSize: '12px' }}>Submitted Date</div>
                  <div style={{ fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                    {formatSubmittedDate(selectedEvent.createdAt)}
                  </div>
                </div>

                {selectedEvent.organizerId && (
                  <div style={{ gridColumn: '1 / -1' }}>
                    <div style={{ color: 'var(--ep-text-secondary)', marginBottom: '2px', fontSize: '12px' }}>Organizer ID</div>
                    <div style={{
                      fontFamily: 'monospace',
                      fontSize: '12px',
                      color: 'var(--ep-text-primary)',
                      wordBreak: 'break-all',
                    }}>
                      {selectedEvent.organizerId}
                    </div>
                  </div>
                )}
              </div>

              {/* Description */}
              <div>
                <div style={{
                  fontSize: '13px',
                  fontWeight: 600,
                  color: 'var(--ep-text-primary)',
                  marginBottom: '6px',
                }}>
                  Event Description
                </div>
                <div style={{
                  fontSize: '14px',
                  color: 'var(--ep-text-secondary)',
                  lineHeight: 1.6,
                  whiteSpace: 'pre-wrap',
                  backgroundColor: '#ffffff',
                  padding: '12px 14px',
                  borderRadius: 'var(--ep-radius-container, 8px)',
                  border: '1px solid var(--ep-border)',
                }}>
                  {selectedEvent.description || 'No description provided.'}
                </div>
              </div>

              {/* Confirmation Prompt: Approve */}
              {confirmMode === 'approve' && (
                <div style={{
                  padding: '16px',
                  backgroundColor: '#E8F5E9',
                  borderRadius: 'var(--ep-radius-container, 10px)',
                  border: '1px solid #C8E6C9',
                }}>
                  <h4 style={{
                    fontSize: '14px',
                    fontWeight: 600,
                    color: '#1B5E20',
                    margin: '0 0 6px 0',
                  }}>
                    Confirm Event Approval
                  </h4>
                  <p style={{
                    fontSize: '13px',
                    color: '#2E7D32',
                    margin: '0 0 16px 0',
                    lineHeight: 1.5,
                  }}>
                    This event will transition from <strong>Pending</strong> to <strong>Approved</strong>.
                    It will become ready for publishing and will be removed from the review queue.
                  </p>
                  <div style={{ display: 'flex', gap: '10px' }}>
                    <button
                      type="button"
                      onClick={handleApprove}
                      disabled={actionInProgress}
                      style={{
                        backgroundColor: '#2E7D32',
                        color: '#ffffff',
                        border: 'none',
                        borderRadius: 'var(--ep-radius-btn)',
                        padding: '8px 18px',
                        fontSize: '13px',
                        fontWeight: 600,
                        cursor: actionInProgress ? 'not-allowed' : 'pointer',
                        opacity: actionInProgress ? 0.7 : 1,
                      }}
                    >
                      {actionInProgress ? 'Approving...' : 'Confirm Approval'}
                    </button>
                    <button
                      type="button"
                      onClick={() => setConfirmMode(null)}
                      disabled={actionInProgress}
                      className="ep-btn-secondary"
                      style={{ fontSize: '13px', padding: '8px 16px' }}
                    >
                      Cancel
                    </button>
                  </div>
                </div>
              )}

              {/* Confirmation Prompt: Reject */}
              {confirmMode === 'reject' && (
                <div style={{
                  padding: '16px',
                  backgroundColor: '#FFF5F5',
                  borderRadius: 'var(--ep-radius-container, 10px)',
                  border: '1px solid #FED7D7',
                }}>
                  <h4 style={{
                    fontSize: '14px',
                    fontWeight: 600,
                    color: 'var(--ep-danger)',
                    margin: '0 0 6px 0',
                  }}>
                    Confirm Event Rejection
                  </h4>
                  <p style={{
                    fontSize: '13px',
                    color: 'var(--ep-text-secondary)',
                    margin: '0 0 12px 0',
                    lineHeight: 1.5,
                  }}>
                    This event will transition to <strong>Rejected</strong>. The organizer will see this status on their dashboard.
                  </p>
                  <div style={{ marginBottom: '14px' }}>
                    <label
                      htmlFor="rejection-notes"
                      style={{
                        display: 'block',
                        fontSize: '12px',
                        fontWeight: 600,
                        color: 'var(--ep-text-primary)',
                        marginBottom: '6px',
                      }}
                    >
                      Reviewer Notes / Rejection Reason <span style={{ color: 'var(--ep-danger)' }}>*</span>
                    </label>
                    <textarea
                      id="rejection-notes"
                      rows={3}
                      value={rejectionNotes}
                      onChange={(e) => setRejectionNotes(e.target.value)}
                      placeholder="e.g. Incomplete description or policy violation..."
                      disabled={actionInProgress}
                      style={{
                        width: '100%',
                        padding: '8px 12px',
                        borderRadius: 'var(--ep-radius-input, 8px)',
                        border: '1px solid var(--ep-border)',
                        fontSize: '13px',
                        fontFamily: 'inherit',
                        resize: 'vertical',
                        boxSizing: 'border-box',
                      }}
                    />
                  </div>
                  <div style={{ display: 'flex', gap: '10px' }}>
                    <button
                      type="button"
                      onClick={handleReject}
                      disabled={actionInProgress || !rejectionNotes.trim()}
                      style={{
                        backgroundColor: 'var(--ep-danger)',
                        color: '#ffffff',
                        border: 'none',
                        borderRadius: 'var(--ep-radius-btn)',
                        padding: '8px 18px',
                        fontSize: '13px',
                        fontWeight: 600,
                        cursor: (actionInProgress || !rejectionNotes.trim()) ? 'not-allowed' : 'pointer',
                        opacity: (actionInProgress || !rejectionNotes.trim()) ? 0.6 : 1,
                      }}
                    >
                      {actionInProgress ? 'Rejecting...' : 'Confirm Rejection'}
                    </button>
                    <button
                      type="button"
                      onClick={() => setConfirmMode(null)}
                      disabled={actionInProgress}
                      className="ep-btn-secondary"
                      style={{ fontSize: '13px', padding: '8px 16px' }}
                    >
                      Cancel
                    </button>
                  </div>
                </div>
              )}
            </div>

            {/* Modal Footer (when no confirm mode active) */}
            {!confirmMode && (
              <div style={{
                padding: '16px 24px',
                borderTop: '1px solid var(--ep-border)',
                backgroundColor: 'var(--ep-canvas)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                gap: '12px',
              }}>
                <button
                  type="button"
                  onClick={handleCloseReview}
                  disabled={actionInProgress}
                  className="ep-btn-secondary"
                  style={{ fontSize: '13px', padding: '8px 16px' }}
                >
                  Close
                </button>

                <div style={{ display: 'flex', gap: '10px' }}>
                  <button
                    type="button"
                    onClick={() => setConfirmMode('reject')}
                    disabled={actionInProgress}
                    style={{
                      backgroundColor: 'transparent',
                      color: 'var(--ep-danger)',
                      border: '1px solid var(--ep-danger)',
                      borderRadius: 'var(--ep-radius-btn)',
                      padding: '8px 16px',
                      fontSize: '13px',
                      fontWeight: 600,
                      cursor: actionInProgress ? 'not-allowed' : 'pointer',
                      transition: 'var(--ep-transition)',
                    }}
                  >
                    Reject Event
                  </button>

                  <button
                    type="button"
                    onClick={() => setConfirmMode('approve')}
                    disabled={actionInProgress}
                    style={{
                      backgroundColor: '#2E7D32',
                      color: '#ffffff',
                      border: 'none',
                      borderRadius: 'var(--ep-radius-btn)',
                      padding: '8px 20px',
                      fontSize: '13px',
                      fontWeight: 600,
                      cursor: actionInProgress ? 'not-allowed' : 'pointer',
                      transition: 'var(--ep-transition)',
                    }}
                  >
                    Approve Event
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
