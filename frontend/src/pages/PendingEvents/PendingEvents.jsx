import React, { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { useAuth } from '../../context/AuthContext';
import {
  getPendingEvents,
  getPendingEventById,
  approveEvent,
  rejectEvent,
  getPendingEventUpdateRequests,
  getEventUpdateRequestReview,
  approveEventUpdateRequest,
  rejectEventUpdateRequest,
  getPendingEventCancellationRequests,
  getEventCancellationRequestReview,
  approveEventCancellationRequest,
  rejectEventCancellationRequest,
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
  AlertTriangle,
  Ticket,
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
  const [activeTab, setActiveTab] = useState('new_submissions'); // 'new_submissions' | 'update_requests' | 'cancellation_requests'
  const [events, setEvents] = useState([]);
  const [updateRequests, setUpdateRequests] = useState([]);
  const [cancellationRequests, setCancellationRequests] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);
  const [feedback, setFeedback] = useState(null);

  // Review Modal / Drawer state (New Submissions)
  const [selectedEvent, setSelectedEvent] = useState(null);
  const [loadingDetails, setLoadingDetails] = useState(false);
  const [confirmMode, setConfirmMode] = useState(null); // 'approve' | 'reject' | null
  const [rejectionNotes, setRejectionNotes] = useState('');
  const [actionInProgress, setActionInProgress] = useState(false);
  const [actionError, setActionError] = useState(null);

  // Review Modal state (Event Update Requests)
  const [selectedUpdateRequest, setSelectedUpdateRequest] = useState(null);
  const [updateConfirmMode, setConfirmModeUpdate] = useState(null); // 'approve' | 'reject' | null
  const [updateReviewNotes, setUpdateReviewNotes] = useState('');
  const [updateActionInProgress, setUpdateActionInProgress] = useState(false);
  const [updateActionError, setUpdateActionError] = useState(null);

  // Review Modal state (Event Cancellation Requests EP-35 / US-15)
  const [selectedCancellationRequest, setSelectedCancellationRequest] = useState(null);
  const [cancelConfirmMode, setCancelConfirmMode] = useState(null); // 'approve' | 'reject' | null
  const [cancelReviewNotes, setCancelReviewNotes] = useState('');
  const [cancelActionInProgress, setCancelActionInProgress] = useState(false);
  const [cancelActionError, setCancelActionError] = useState(null);

  const getEffectiveToken = useCallback(() => {
    return accessToken || sessionStorage.getItem('ep_access_token');
  }, [accessToken]);

  const loadAllData = useCallback(async (isManualRefresh = false) => {
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
      const [eventsData, updatesData, cancellationsData] = await Promise.all([
        getPendingEvents(token),
        getPendingEventUpdateRequests(token).catch((err) => {
          console.warn('Failed to load pending update requests:', err);
          return [];
        }),
        getPendingEventCancellationRequests(token).catch((err) => {
          console.warn('Failed to load pending cancellation requests:', err);
          return [];
        }),
      ]);
      setEvents(Array.isArray(eventsData) ? eventsData : []);
      setUpdateRequests(Array.isArray(updatesData) ? updatesData : []);
      setCancellationRequests(Array.isArray(cancellationsData) ? cancellationsData : []);
    } catch (err) {
      setError(err.message || 'Unable to load pending event submissions.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [getEffectiveToken]);

  useEffect(() => {
    loadAllData();
  }, [loadAllData]);

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

  const handleOpenUpdateRequestReview = async (item) => {
    setUpdateActionError(null);
    setConfirmModeUpdate(null);
    setUpdateReviewNotes('');
    setSelectedUpdateRequest(item);

    const token = getEffectiveToken();
    if (token) {
      try {
        const fullReview = await getEventUpdateRequestReview(item.id, token);
        if (fullReview) {
          setSelectedUpdateRequest(fullReview);
        }
      } catch (err) {
        // Fallback to item from list
      }
    }
  };

  const handleCloseUpdateRequestReview = () => {
    if (updateActionInProgress) return;
    setSelectedUpdateRequest(null);
    setConfirmModeUpdate(null);
    setUpdateActionError(null);
    setUpdateReviewNotes('');
  };

  const handleApproveUpdateRequest = async () => {
    if (!selectedUpdateRequest || updateActionInProgress) return;
    setUpdateActionInProgress(true);
    setUpdateActionError(null);

    const token = getEffectiveToken();
    try {
      await approveEventUpdateRequest(selectedUpdateRequest.id, token, updateReviewNotes.trim());
      const approvedTitle = selectedUpdateRequest.proposed?.title || 'Event';
      handleCloseUpdateRequestReview();
      setFeedback({
        type: 'success',
        message: `Event update approved successfully. Changes applied to live event ("${approvedTitle}").`,
      });
      setUpdateRequests((prev) => prev.filter((r) => r.id !== selectedUpdateRequest.id));
    } catch (err) {
      setUpdateActionError(err.message || 'Failed to approve event update request.');
      if (err.status === 409) {
        loadAllData(true);
      }
    } finally {
      setUpdateActionInProgress(false);
    }
  };

  const handleRejectUpdateRequest = async () => {
    if (!selectedUpdateRequest || updateActionInProgress) return;
    if (!updateReviewNotes.trim()) {
      setUpdateActionError('Rejection feedback is required. Please explain why this update request was rejected.');
      return;
    }
    setUpdateActionInProgress(true);
    setUpdateActionError(null);

    const token = getEffectiveToken();
    try {
      await rejectEventUpdateRequest(selectedUpdateRequest.id, token, updateReviewNotes.trim());
      const eventTitle = selectedUpdateRequest.proposed?.title || 'Event';
      handleCloseUpdateRequestReview();
      setFeedback({
        type: 'info',
        message: `Event update request rejected. The live event remains unchanged. ("${eventTitle}")`,
      });
      setUpdateRequests((prev) => prev.filter((r) => r.id !== selectedUpdateRequest.id));
    } catch (err) {
      setUpdateActionError(err.message || 'Failed to reject event update request.');
      if (err.status === 409) {
        loadAllData(true);
      }
    } finally {
      setUpdateActionInProgress(false);
    }
  };

  const handleOpenCancellationReview = async (item) => {
    setCancelActionError(null);
    setCancelConfirmMode(null);
    setCancelReviewNotes('');
    setSelectedCancellationRequest(item);

    const token = getEffectiveToken();
    if (token) {
      try {
        const fullReview = await getEventCancellationRequestReview(item.id, token);
        if (fullReview) {
          setSelectedCancellationRequest(fullReview);
        }
      } catch (err) {
        // Fallback to item from list
      }
    }
  };

  const handleCloseCancellationReview = () => {
    if (cancelActionInProgress) return;
    setSelectedCancellationRequest(null);
    setCancelConfirmMode(null);
    setCancelActionError(null);
    setCancelReviewNotes('');
  };

  const handleApproveCancellation = async () => {
    if (!selectedCancellationRequest || cancelActionInProgress) return;
    setCancelActionInProgress(true);
    setCancelActionError(null);

    const token = getEffectiveToken();
    try {
      await approveEventCancellationRequest(selectedCancellationRequest.id, token, cancelReviewNotes.trim());
      const eventTitle = selectedCancellationRequest.eventTitle || 'Event';
      handleCloseCancellationReview();
      setFeedback({
        type: 'success',
        message: `Event cancellation approved successfully. The event ("${eventTitle}") has been marked as Cancelled and ticket sales stopped.`,
      });
      setCancellationRequests((prev) => prev.filter((r) => r.id !== selectedCancellationRequest.id));
    } catch (err) {
      setCancelActionError(err.message || 'Failed to approve event cancellation.');
      if (err.status === 409) {
        loadAllData(true);
      }
    } finally {
      setCancelActionInProgress(false);
    }
  };

  const handleRejectCancellation = async () => {
    if (!selectedCancellationRequest || cancelActionInProgress) return;
    if (!cancelReviewNotes.trim()) {
      setCancelActionError('Rejection feedback is required. Please explain why this cancellation request was rejected.');
      return;
    }
    setCancelActionInProgress(true);
    setCancelActionError(null);

    const token = getEffectiveToken();
    try {
      await rejectEventCancellationRequest(selectedCancellationRequest.id, token, cancelReviewNotes.trim());
      const eventTitle = selectedCancellationRequest.eventTitle || 'Event';
      handleCloseCancellationReview();
      setFeedback({
        type: 'info',
        message: `Event cancellation request rejected. The event ("${eventTitle}") remains active and ticket sales may resume.`,
      });
      setCancellationRequests((prev) => prev.filter((r) => r.id !== selectedCancellationRequest.id));
    } catch (err) {
      setCancelActionError(err.message || 'Failed to reject event cancellation request.');
      if (err.status === 409) {
        loadAllData(true);
      }
    } finally {
      setCancelActionInProgress(false);
    }
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
            marginBottom: '16px',
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
                Event Approvals & Updates
              </h1>
            </div>

            <button
              type="button"
              onClick={() => loadAllData(true)}
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

          {/* Navigation Tabs (EP-210 / EP-34) */}
          <div style={{
            display: 'flex',
            gap: '8px',
            borderBottom: '1px solid var(--ep-border)',
            marginBottom: '20px',
          }}>
            <button
              type="button"
              onClick={() => setActiveTab('new_submissions')}
              style={{
                padding: '10px 18px',
                fontSize: '14px',
                fontWeight: 600,
                border: 'none',
                borderBottom: activeTab === 'new_submissions' ? '2px solid var(--ep-primary)' : '2px solid transparent',
                color: activeTab === 'new_submissions' ? 'var(--ep-primary)' : 'var(--ep-text-secondary)',
                backgroundColor: 'transparent',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: '8px',
                transition: 'var(--ep-transition)',
              }}
            >
              <span>New Event Submissions</span>
              <span style={{
                backgroundColor: activeTab === 'new_submissions' ? 'var(--ep-primary)' : 'var(--ep-canvas)',
                color: activeTab === 'new_submissions' ? '#ffffff' : 'var(--ep-text-secondary)',
                borderRadius: 'var(--ep-radius-pill, 9999px)',
                padding: '2px 8px',
                fontSize: '12px',
                fontWeight: 700,
              }}>
                {events.length}
              </span>
            </button>

            <button
              type="button"
              onClick={() => setActiveTab('update_requests')}
              style={{
                padding: '10px 18px',
                fontSize: '14px',
                fontWeight: 600,
                border: 'none',
                borderBottom: activeTab === 'update_requests' ? '2px solid var(--ep-primary)' : '2px solid transparent',
                color: activeTab === 'update_requests' ? 'var(--ep-primary)' : 'var(--ep-text-secondary)',
                backgroundColor: 'transparent',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: '8px',
                transition: 'var(--ep-transition)',
              }}
            >
              <span>Event Updates</span>
              <span style={{
                backgroundColor: activeTab === 'update_requests' ? 'var(--ep-primary)' : 'var(--ep-canvas)',
                color: activeTab === 'update_requests' ? '#ffffff' : 'var(--ep-text-secondary)',
                borderRadius: 'var(--ep-radius-pill, 9999px)',
                padding: '2px 8px',
                fontSize: '12px',
                fontWeight: 700,
              }}>
                {updateRequests.length}
              </span>
            </button>

            <button
              type="button"
              onClick={() => setActiveTab('cancellation_requests')}
              style={{
                padding: '10px 18px',
                fontSize: '14px',
                fontWeight: 600,
                border: 'none',
                borderBottom: activeTab === 'cancellation_requests' ? '2px solid var(--ep-primary)' : '2px solid transparent',
                color: activeTab === 'cancellation_requests' ? 'var(--ep-primary)' : 'var(--ep-text-secondary)',
                backgroundColor: 'transparent',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: '8px',
                transition: 'var(--ep-transition)',
              }}
            >
              <span>Cancellation Requests</span>
              <span style={{
                backgroundColor: activeTab === 'cancellation_requests' ? 'var(--ep-primary)' : 'var(--ep-canvas)',
                color: activeTab === 'cancellation_requests' ? '#ffffff' : 'var(--ep-text-secondary)',
                borderRadius: 'var(--ep-radius-pill, 9999px)',
                padding: '2px 8px',
                fontSize: '12px',
                fontWeight: 700,
              }}>
                {cancellationRequests.length}
              </span>
            </button>
          </div>

          <p style={{
            fontSize: '14px',
            color: 'var(--ep-text-secondary)',
            margin: '0 0 28px 0',
          }}>
            {activeTab === 'new_submissions'
              ? 'Review and verify organizer event submissions before they can be published to visitors.'
              : activeTab === 'update_requests'
                ? 'Review and verify requested modifications to approved live events before changes take effect.'
                : 'Review organizer event cancellation requests. Approving will cancel the event and permanently prevent ticket sales.'}
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
                  onClick={() => loadAllData(false)}
                  className="ep-btn-secondary"
                  style={{ fontSize: '13px', padding: '6px 16px' }}
                >
                  Retry
                </button>
              </div>
            </div>
          )}

          {/* Empty State: New Submissions */}
          {!loading && !error && activeTab === 'new_submissions' && events.length === 0 && (
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
          {!loading && !error && activeTab === 'new_submissions' && events.length > 0 && (
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

                        {(item.category || item.venueType) && (
                          <div style={{ marginBottom: '8px' }}>
                            <span style={{
                              display: 'inline-flex',
                              alignItems: 'center',
                              backgroundColor: '#FFF0E6',
                              color: '#1D1D1F',
                              border: '1px solid rgba(255, 91, 0, 0.18)',
                              borderRadius: 'var(--ep-radius-pill, 9999px)',
                              padding: '3px 10px',
                              fontSize: '11px',
                              fontWeight: 600,
                            }}>
                              {item.venueType && item.category
                                ? `${item.venueType} • ${item.category}`
                                : (item.category || item.venueType)}
                            </span>
                          </div>
                        )}

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

          {/* Empty State: Event Updates (EP-210 / EP-34) */}
          {!loading && !error && activeTab === 'update_requests' && updateRequests.length === 0 && (
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
                No pending update requests
              </h3>
              <p style={{
                fontSize: '13px',
                color: 'var(--ep-text-secondary)',
                margin: 0,
                lineHeight: 1.5,
              }}>
                All organizer update requests have been reviewed. When organizers modify approved events, update requests will appear here for verification.
              </p>
            </div>
          )}

          {/* Pending Event Updates Queue (EP-210 / EP-34) */}
          {!loading && !error && activeTab === 'update_requests' && updateRequests.length > 0 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              {updateRequests.map((item) => {
                const displayTitle = item.proposed?.title || item.current?.title || 'Untitled Event';
                const displayPoster = item.proposed?.imageUrl || item.current?.imageUrl;
                const displayVenue = item.proposed?.venue || item.current?.venue;
                const displayDate = item.proposed?.eventDate || item.current?.eventDate;
                const displayCategory = item.proposed?.category || item.current?.category;
                const displayVenueType = item.proposed?.venueType || item.current?.venueType;

                return (
                  <div
                    key={item.id}
                    className="ep-card"
                    style={{
                      padding: '20px',
                      borderRadius: 'var(--ep-radius-card)',
                      border: item.isMajorChange ? '1px solid #FCA5A5' : '1px solid var(--ep-border)',
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
                        {displayPoster ? (
                          <img
                            src={displayPoster}
                            alt={displayTitle}
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
                              {displayTitle}
                            </h3>

                            <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                              {item.isMajorChange && (
                                <span style={{
                                  backgroundColor: '#FEE2E2',
                                  color: '#B91C1C',
                                  border: '1px solid #FCA5A5',
                                  borderRadius: 'var(--ep-radius-pill)',
                                  padding: '4px 10px',
                                  fontSize: '11px',
                                  fontWeight: 700,
                                  letterSpacing: '0.04em',
                                  textTransform: 'uppercase',
                                  display: 'inline-flex',
                                  alignItems: 'center',
                                  gap: '4px',
                                }}>
                                  <AlertTriangle size={12} />
                                  MAJOR CHANGE
                                </span>
                              )}

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
                                UPDATE PENDING
                              </span>
                            </div>
                          </div>

                          {(displayCategory || displayVenueType) && (
                            <div style={{ marginBottom: '8px' }}>
                              <span style={{
                                display: 'inline-flex',
                                alignItems: 'center',
                                backgroundColor: '#FFF0E6',
                                color: '#1D1D1F',
                                border: '1px solid rgba(255, 91, 0, 0.18)',
                                borderRadius: 'var(--ep-radius-pill, 9999px)',
                                padding: '3px 10px',
                                fontSize: '11px',
                                fontWeight: 600,
                              }}>
                                {displayVenueType && displayCategory
                                  ? `${displayVenueType} • ${displayCategory}`
                                  : (displayCategory || displayVenueType)}
                              </span>
                            </div>
                          )}

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
                              color: item.venueChanged ? '#B91C1C' : 'var(--ep-text-secondary)',
                              fontWeight: item.venueChanged ? 600 : 400,
                            }}>
                              <MapPin size={15} color={item.venueChanged ? '#DC2626' : 'var(--ep-text-secondary)'} style={{ flexShrink: 0 }} />
                              <span>{displayVenue || 'Venue TBA'}</span>
                              {item.venueChanged && (
                                <span style={{ fontSize: '11px', color: '#B91C1C', backgroundColor: '#FEE2E2', padding: '1px 6px', borderRadius: '4px' }}>
                                  Venue changed
                                </span>
                              )}
                            </div>

                            <div style={{
                              display: 'flex',
                              alignItems: 'center',
                              gap: '8px',
                              fontSize: '13px',
                              color: item.dateChanged ? '#B91C1C' : 'var(--ep-text-secondary)',
                              fontWeight: item.dateChanged ? 600 : 400,
                            }}>
                              <Calendar size={15} color={item.dateChanged ? '#DC2626' : 'var(--ep-text-secondary)'} style={{ flexShrink: 0 }} />
                              <span>{formatEventDateTime(displayDate)}</span>
                              {item.dateChanged && (
                                <span style={{ fontSize: '11px', color: '#B91C1C', backgroundColor: '#FEE2E2', padding: '1px 6px', borderRadius: '4px' }}>
                                  Date/Time changed
                                </span>
                              )}
                            </div>
                          </div>
                        </div>

                        {/* Card Footer with Requested Date & Review Action */}
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
                            {item.requestedAt && (
                              <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                                Requested on {formatSubmittedDate(item.requestedAt).replace('Submitted ', '')}
                              </span>
                            )}
                          </div>

                          <button
                            type="button"
                            onClick={() => handleOpenUpdateRequestReview(item)}
                            className="ep-btn-secondary"
                            style={{
                              fontSize: '13px',
                              padding: '6px 16px',
                              fontWeight: 600,
                              cursor: 'pointer',
                            }}
                          >
                            Review Update
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}

          {/* Empty State: Cancellation Requests */}
          {!loading && !error && activeTab === 'cancellation_requests' && cancellationRequests.length === 0 && (
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
                No pending cancellation requests
              </h3>
              <p style={{
                fontSize: '13px',
                color: 'var(--ep-text-secondary)',
                margin: 0,
                lineHeight: 1.5,
              }}>
                All organizer cancellation requests have been reviewed. When organizers request to cancel an approved event, requests will appear here.
              </p>
            </div>
          )}

          {/* Pending Cancellation Requests Queue */}
          {!loading && !error && activeTab === 'cancellation_requests' && cancellationRequests.length > 0 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              {cancellationRequests.map((item) => (
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
                          alt={item.eventTitle || 'Event'}
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
                        <Calendar size={28} color="var(--ep-primary)" />
                      )}
                    </div>

                    {/* Details */}
                    <div style={{
                      flex: 1,
                      minWidth: '280px',
                      display: 'flex',
                      flexDirection: 'column',
                      justifyContent: 'space-between',
                    }}>
                      <div>
                        <div style={{
                          display: 'flex',
                          alignItems: 'flex-start',
                          justifyContent: 'space-between',
                          gap: '12px',
                          marginBottom: '6px',
                          flexWrap: 'wrap',
                        }}>
                          <h3 style={{
                            fontSize: '18px',
                            fontWeight: 600,
                            color: 'var(--ep-text-primary)',
                            margin: 0,
                            lineHeight: 1.3,
                          }}>
                            {item.eventTitle || 'Untitled Event'}
                          </h3>
                          <span style={{
                            backgroundColor: '#FFF0F0',
                            color: '#D32F2F',
                            border: '1px solid rgba(211, 47, 47, 0.2)',
                            borderRadius: 'var(--ep-radius-pill, 9999px)',
                            padding: '2px 10px',
                            fontSize: '11px',
                            fontWeight: 700,
                            letterSpacing: '0.04em',
                            display: 'inline-block',
                          }}>
                            CANCELLATION REQUESTED
                          </span>
                        </div>

                        {/* Event Metadata (Date, Venue, Category) */}
                        <div style={{
                          display: 'flex',
                          flexWrap: 'wrap',
                          gap: '16px',
                          fontSize: '13px',
                          color: 'var(--ep-text-secondary)',
                          marginBottom: '12px',
                        }}>
                          {item.eventDate && (
                            <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                              <Calendar size={14} />
                              <span>{formatEventDateTime(item.eventDate)}</span>
                            </div>
                          )}
                          {item.venue && (
                            <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                              <MapPin size={14} />
                              <span>{item.venue}</span>
                            </div>
                          )}
                          <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                            <Ticket size={14} />
                            <span>Tickets sold: <strong>{item.totalTicketsSold ?? 0}</strong></span>
                          </div>
                        </div>

                        {/* Cancellation Reason Preview */}
                        <div style={{
                          backgroundColor: '#FFF8F6',
                          borderLeft: '3px solid #FF5B00',
                          padding: '10px 14px',
                          borderRadius: '0 6px 6px 0',
                          marginBottom: '14px',
                          fontSize: '13px',
                          color: 'var(--ep-text-primary)',
                        }}>
                          <div style={{ fontSize: '11px', fontWeight: 700, textTransform: 'uppercase', color: '#D84315', marginBottom: '4px', letterSpacing: '0.05em' }}>
                            Reason for Cancellation:
                          </div>
                          <p style={{ margin: 0, fontStyle: 'italic', lineHeight: 1.4 }}>
                            "{item.reason}"
                          </p>
                        </div>
                      </div>

                      {/* Card Footer with Requested Date & Review Action */}
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
                          {item.requestedAt && (
                            <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                              Requested on {formatSubmittedDate(item.requestedAt).replace('Submitted ', '')}
                            </span>
                          )}
                        </div>

                        <button
                          type="button"
                          onClick={() => handleOpenCancellationReview(item)}
                          className="ep-btn-secondary"
                          style={{
                            fontSize: '13px',
                            padding: '6px 16px',
                            fontWeight: 600,
                            cursor: 'pointer',
                            color: '#C62828',
                            borderColor: 'rgba(198, 40, 40, 0.3)',
                          }}
                        >
                          Review Request
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

      {/* Review Modal Panel (New Submissions) */}
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

                {(selectedEvent.category || selectedEvent.venueType) && (
                  <div style={{ marginTop: '8px' }}>
                    <span style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      backgroundColor: '#FFF0E6',
                      color: '#1D1D1F',
                      border: '1px solid rgba(255, 91, 0, 0.18)',
                      borderRadius: 'var(--ep-radius-pill, 9999px)',
                      padding: '3px 10px',
                      fontSize: '12px',
                      fontWeight: 600,
                    }}>
                      {selectedEvent.venueType && selectedEvent.category
                        ? `${selectedEvent.venueType} • ${selectedEvent.category}`
                        : (selectedEvent.category || selectedEvent.venueType)}
                    </span>
                  </div>
                )}
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
                  <div style={{ color: 'var(--ep-text-secondary)', marginBottom: '2px', fontSize: '12px' }}>Category</div>
                  <div style={{ fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                    {selectedEvent.category || 'Not specified'}
                  </div>
                </div>

                <div>
                  <div style={{ color: 'var(--ep-text-secondary)', marginBottom: '2px', fontSize: '12px' }}>Venue Type</div>
                  <div style={{ fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                    {selectedEvent.venueType || 'Not specified'}
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
                    This event will be <strong>approved and published immediately</strong>,
                    becoming visible to all visitors on the platform right away.
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

      {/* Side-by-Side Update Request Comparison Modal (EP-210 / EP-34) */}
      {selectedUpdateRequest && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="update-review-modal-title"
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
            if (e.target === e.currentTarget && !updateActionInProgress) {
              handleCloseUpdateRequestReview();
            }
          }}
        >
          <div style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            boxShadow: 'var(--ep-shadow-modal, 0 12px 36px rgba(0,0,0,0.12))',
            width: '100%',
            maxWidth: '860px',
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
              <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                <Clock size={18} color="var(--ep-primary)" />
                <div>
                  <h2
                    id="update-review-modal-title"
                    style={{
                      fontSize: '17px',
                      fontWeight: 700,
                      color: 'var(--ep-text-primary)',
                      margin: 0,
                    }}
                  >
                    Review Event Update Request
                  </h2>
                  <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', marginTop: '2px' }}>
                    Compare current approved event against requested modifications
                  </div>
                </div>
              </div>
              <button
                type="button"
                onClick={handleCloseUpdateRequestReview}
                disabled={updateActionInProgress}
                style={{
                  background: 'none',
                  border: 'none',
                  cursor: updateActionInProgress ? 'not-allowed' : 'pointer',
                  color: 'var(--ep-text-secondary)',
                  padding: '4px',
                  display: 'flex',
                  alignItems: 'center',
                }}
                aria-label="Close update review dialog"
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
              {/* Error Banner */}
              {updateActionError && (
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
                  <span>{updateActionError}</span>
                </div>
              )}

              {/* Major Change Warning Banner */}
              {selectedUpdateRequest.isMajorChange && (
                <div style={{
                  padding: '14px 18px',
                  backgroundColor: '#FEF2F2',
                  border: '1px solid #FCA5A5',
                  borderRadius: 'var(--ep-radius-container, 10px)',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '12px',
                }}>
                  <AlertTriangle size={22} color="#DC2626" style={{ flexShrink: 0 }} />
                  <div>
                    <div style={{ fontWeight: 700, fontSize: '14px', color: '#991B1B' }}>
                      Major Event Detail Changes Detected
                    </div>
                    <div style={{ fontSize: '13px', color: '#B91C1C', marginTop: '2px' }}>
                      Event date, time, or venue has been modified. Existing ticket holders may need to be notified if this update is approved.
                    </div>
                  </div>
                </div>
              )}

              {/* Side-by-Side Comparison Header */}
              <div style={{
                display: 'grid',
                gridTemplateColumns: '1fr 1fr',
                gap: '16px',
              }}>
                <div style={{
                  padding: '10px 14px',
                  backgroundColor: 'var(--ep-canvas)',
                  borderRadius: 'var(--ep-radius-container, 8px)',
                  border: '1px solid var(--ep-border)',
                  fontWeight: 700,
                  fontSize: '13px',
                  color: 'var(--ep-text-secondary)',
                  textTransform: 'uppercase',
                  letterSpacing: '0.04em',
                }}>
                  Current Approved (Live)
                </div>
                <div style={{
                  padding: '10px 14px',
                  backgroundColor: '#FFF0E6',
                  borderRadius: 'var(--ep-radius-container, 8px)',
                  border: '1px solid #FFE0CC',
                  fontWeight: 700,
                  fontSize: '13px',
                  color: 'var(--ep-primary)',
                  textTransform: 'uppercase',
                  letterSpacing: '0.04em',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                }}>
                  <span>Requested Update</span>
                  <span style={{ fontSize: '11px', fontWeight: 600 }}>Proposed</span>
                </div>
              </div>

              {/* Title Comparison */}
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '6px' }}>
                  <span style={{ fontSize: '12px', fontWeight: 700, color: 'var(--ep-text-secondary)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
                    Event Title
                  </span>
                  {selectedUpdateRequest.titleChanged && (
                    <span style={{ fontSize: '10px', fontWeight: 700, backgroundColor: '#FFF0E6', color: 'var(--ep-primary)', border: '1px solid #FFE0CC', padding: '1px 6px', borderRadius: '4px' }}>
                      CHANGED
                    </span>
                  )}
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
                  <div style={{ padding: '10px 14px', backgroundColor: 'var(--ep-canvas)', borderRadius: '8px', border: '1px solid var(--ep-border)', fontSize: '14px', fontWeight: 500, color: 'var(--ep-text-primary)' }}>
                    {selectedUpdateRequest.current?.title || '-'}
                  </div>
                  <div style={{
                    padding: '10px 14px',
                    backgroundColor: selectedUpdateRequest.titleChanged ? '#FFF7ED' : '#ffffff',
                    borderRadius: '8px',
                    border: selectedUpdateRequest.titleChanged ? '1px solid var(--ep-primary)' : '1px solid var(--ep-border)',
                    fontSize: '14px',
                    fontWeight: 600,
                    color: selectedUpdateRequest.titleChanged ? 'var(--ep-primary)' : 'var(--ep-text-primary)',
                  }}>
                    {selectedUpdateRequest.proposed?.title || '-'}
                  </div>
                </div>
              </div>

              {/* Date & Time Comparison */}
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '6px' }}>
                  <span style={{ fontSize: '12px', fontWeight: 700, color: 'var(--ep-text-secondary)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
                    Date & Time
                  </span>
                  {selectedUpdateRequest.dateChanged && (
                    <span style={{ fontSize: '10px', fontWeight: 700, backgroundColor: '#FEE2E2', color: '#B91C1C', border: '1px solid #FCA5A5', padding: '1px 6px', borderRadius: '4px' }}>
                      MAJOR CHANGE
                    </span>
                  )}
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
                  <div style={{ padding: '10px 14px', backgroundColor: 'var(--ep-canvas)', borderRadius: '8px', border: '1px solid var(--ep-border)', fontSize: '13px', color: 'var(--ep-text-primary)' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                      <Calendar size={14} color="var(--ep-text-secondary)" />
                      <span>{formatEventDateTime(selectedUpdateRequest.current?.eventDate)}</span>
                    </div>
                  </div>
                  <div style={{
                    padding: '10px 14px',
                    backgroundColor: selectedUpdateRequest.dateChanged ? '#FEF2F2' : '#ffffff',
                    borderRadius: '8px',
                    border: selectedUpdateRequest.dateChanged ? '1px solid #DC2626' : '1px solid var(--ep-border)',
                    fontSize: '13px',
                    fontWeight: selectedUpdateRequest.dateChanged ? 600 : 400,
                    color: selectedUpdateRequest.dateChanged ? '#991B1B' : 'var(--ep-text-primary)',
                  }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                      <Calendar size={14} color={selectedUpdateRequest.dateChanged ? '#DC2626' : 'var(--ep-text-secondary)'} />
                      <span>{formatEventDateTime(selectedUpdateRequest.proposed?.eventDate)}</span>
                    </div>
                  </div>
                </div>
              </div>

              {/* Venue Comparison */}
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '6px' }}>
                  <span style={{ fontSize: '12px', fontWeight: 700, color: 'var(--ep-text-secondary)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
                    Venue Location
                  </span>
                  {selectedUpdateRequest.venueChanged && (
                    <span style={{ fontSize: '10px', fontWeight: 700, backgroundColor: '#FEE2E2', color: '#B91C1C', border: '1px solid #FCA5A5', padding: '1px 6px', borderRadius: '4px' }}>
                      MAJOR CHANGE
                    </span>
                  )}
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
                  <div style={{ padding: '10px 14px', backgroundColor: 'var(--ep-canvas)', borderRadius: '8px', border: '1px solid var(--ep-border)', fontSize: '13px', color: 'var(--ep-text-primary)' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                      <MapPin size={14} color="var(--ep-text-secondary)" />
                      <span>{selectedUpdateRequest.current?.venue || 'TBA'}</span>
                    </div>
                  </div>
                  <div style={{
                    padding: '10px 14px',
                    backgroundColor: selectedUpdateRequest.venueChanged ? '#FEF2F2' : '#ffffff',
                    borderRadius: '8px',
                    border: selectedUpdateRequest.venueChanged ? '1px solid #DC2626' : '1px solid var(--ep-border)',
                    fontSize: '13px',
                    fontWeight: selectedUpdateRequest.venueChanged ? 600 : 400,
                    color: selectedUpdateRequest.venueChanged ? '#991B1B' : 'var(--ep-text-primary)',
                  }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                      <MapPin size={14} color={selectedUpdateRequest.venueChanged ? '#DC2626' : 'var(--ep-text-secondary)'} />
                      <span>{selectedUpdateRequest.proposed?.venue || 'TBA'}</span>
                    </div>
                  </div>
                </div>
              </div>

              {/* Category & Venue Type Comparison */}
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '6px' }}>
                  <span style={{ fontSize: '12px', fontWeight: 700, color: 'var(--ep-text-secondary)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
                    Category & Venue Type
                  </span>
                  {(selectedUpdateRequest.categoryChanged || selectedUpdateRequest.venueTypeChanged) && (
                    <span style={{ fontSize: '10px', fontWeight: 700, backgroundColor: '#FFF0E6', color: 'var(--ep-primary)', border: '1px solid #FFE0CC', padding: '1px 6px', borderRadius: '4px' }}>
                      CHANGED
                    </span>
                  )}
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
                  <div style={{ padding: '10px 14px', backgroundColor: 'var(--ep-canvas)', borderRadius: '8px', border: '1px solid var(--ep-border)', fontSize: '13px', color: 'var(--ep-text-primary)' }}>
                    {selectedUpdateRequest.current?.venueType || 'TBA'} • {selectedUpdateRequest.current?.category || 'TBA'}
                  </div>
                  <div style={{
                    padding: '10px 14px',
                    backgroundColor: (selectedUpdateRequest.categoryChanged || selectedUpdateRequest.venueTypeChanged) ? '#FFF7ED' : '#ffffff',
                    borderRadius: '8px',
                    border: (selectedUpdateRequest.categoryChanged || selectedUpdateRequest.venueTypeChanged) ? '1px solid var(--ep-primary)' : '1px solid var(--ep-border)',
                    fontSize: '13px',
                    fontWeight: (selectedUpdateRequest.categoryChanged || selectedUpdateRequest.venueTypeChanged) ? 600 : 400,
                    color: (selectedUpdateRequest.categoryChanged || selectedUpdateRequest.venueTypeChanged) ? 'var(--ep-primary)' : 'var(--ep-text-primary)',
                  }}>
                    {selectedUpdateRequest.proposed?.venueType || 'TBA'} • {selectedUpdateRequest.proposed?.category || 'TBA'}
                  </div>
                </div>
              </div>

              {/* Description Comparison */}
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '6px' }}>
                  <span style={{ fontSize: '12px', fontWeight: 700, color: 'var(--ep-text-secondary)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
                    Description
                  </span>
                  {selectedUpdateRequest.descriptionChanged && (
                    <span style={{ fontSize: '10px', fontWeight: 700, backgroundColor: '#FFF0E6', color: 'var(--ep-primary)', border: '1px solid #FFE0CC', padding: '1px 6px', borderRadius: '4px' }}>
                      CHANGED
                    </span>
                  )}
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
                  <div style={{
                    padding: '10px 14px',
                    backgroundColor: 'var(--ep-canvas)',
                    borderRadius: '8px',
                    border: '1px solid var(--ep-border)',
                    fontSize: '13px',
                    color: 'var(--ep-text-secondary)',
                    lineHeight: 1.5,
                    maxHeight: '150px',
                    overflowY: 'auto',
                    whiteSpace: 'pre-wrap',
                  }}>
                    {selectedUpdateRequest.current?.description || 'No description'}
                  </div>
                  <div style={{
                    padding: '10px 14px',
                    backgroundColor: selectedUpdateRequest.descriptionChanged ? '#FFF7ED' : '#ffffff',
                    borderRadius: '8px',
                    border: selectedUpdateRequest.descriptionChanged ? '1px solid var(--ep-primary)' : '1px solid var(--ep-border)',
                    fontSize: '13px',
                    color: selectedUpdateRequest.descriptionChanged ? 'var(--ep-text-primary)' : 'var(--ep-text-secondary)',
                    lineHeight: 1.5,
                    maxHeight: '150px',
                    overflowY: 'auto',
                    whiteSpace: 'pre-wrap',
                  }}>
                    {selectedUpdateRequest.proposed?.description || 'No description'}
                  </div>
                </div>
              </div>

              {/* Media Comparison: Poster & Cover */}
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '6px' }}>
                  <span style={{ fontSize: '12px', fontWeight: 700, color: 'var(--ep-text-secondary)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
                    Visual Assets (Poster & Cover)
                  </span>
                  {(selectedUpdateRequest.posterUrlChanged || selectedUpdateRequest.coverUrlChanged) && (
                    <span style={{ fontSize: '10px', fontWeight: 700, backgroundColor: '#FFF0E6', color: 'var(--ep-primary)', border: '1px solid #FFE0CC', padding: '1px 6px', borderRadius: '4px' }}>
                      IMAGE UPDATED
                    </span>
                  )}
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
                  {/* Current Media */}
                  <div style={{
                    padding: '12px',
                    backgroundColor: 'var(--ep-canvas)',
                    borderRadius: '8px',
                    border: '1px solid var(--ep-border)',
                    display: 'flex',
                    gap: '12px',
                    alignItems: 'center',
                  }}>
                    <div style={{ width: '60px', height: '80px', borderRadius: '6px', overflow: 'hidden', backgroundColor: '#e2e8f0', flexShrink: 0 }}>
                      {selectedUpdateRequest.current?.imageUrl ? (
                        <img src={selectedUpdateRequest.current.imageUrl} alt="Current poster" style={{ width: '100%', height: '100%', objectFit: 'cover' }} />
                      ) : (
                        <div style={{ height: '100%', display: 'flex', alignItems: 'center', justifyContent: 'center' }}><ImageIcon size={18} /></div>
                      )}
                    </div>
                    <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                      <div>Poster: {selectedUpdateRequest.current?.imageUrl ? 'Attached' : 'None'}</div>
                      <div style={{ marginTop: '4px' }}>Cover: {selectedUpdateRequest.current?.coverUrl ? 'Attached' : 'None'}</div>
                    </div>
                  </div>

                  {/* Proposed Media */}
                  <div style={{
                    padding: '12px',
                    backgroundColor: (selectedUpdateRequest.posterUrlChanged || selectedUpdateRequest.coverUrlChanged) ? '#FFF7ED' : '#ffffff',
                    borderRadius: '8px',
                    border: (selectedUpdateRequest.posterUrlChanged || selectedUpdateRequest.coverUrlChanged) ? '1px solid var(--ep-primary)' : '1px solid var(--ep-border)',
                    display: 'flex',
                    gap: '12px',
                    alignItems: 'center',
                  }}>
                    <div style={{ width: '60px', height: '80px', borderRadius: '6px', overflow: 'hidden', backgroundColor: '#e2e8f0', flexShrink: 0 }}>
                      {selectedUpdateRequest.proposed?.imageUrl ? (
                        <img src={selectedUpdateRequest.proposed.imageUrl} alt="Proposed poster" style={{ width: '100%', height: '100%', objectFit: 'cover' }} />
                      ) : (
                        <div style={{ height: '100%', display: 'flex', alignItems: 'center', justifyContent: 'center' }}><ImageIcon size={18} /></div>
                      )}
                    </div>
                    <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                      <div style={{ color: selectedUpdateRequest.posterUrlChanged ? 'var(--ep-primary)' : 'inherit', fontWeight: selectedUpdateRequest.posterUrlChanged ? 600 : 400 }}>
                        Poster: {selectedUpdateRequest.posterUrlChanged ? 'New Image Attached' : (selectedUpdateRequest.proposed?.imageUrl ? 'Unchanged' : 'None')}
                      </div>
                      <div style={{ marginTop: '4px', color: selectedUpdateRequest.coverUrlChanged ? 'var(--ep-primary)' : 'inherit', fontWeight: selectedUpdateRequest.coverUrlChanged ? 600 : 400 }}>
                        Cover: {selectedUpdateRequest.coverUrlChanged ? 'New Banner Attached' : (selectedUpdateRequest.proposed?.coverUrl ? 'Unchanged' : 'None')}
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              {/* Approve Confirmation Box */}
              {updateConfirmMode === 'approve' && (
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
                    Confirm Event Update Approval
                  </h4>
                  <p style={{
                    fontSize: '13px',
                    color: '#2E7D32',
                    margin: '0 0 12px 0',
                    lineHeight: 1.5,
                  }}>
                    The proposed changes will be immediately applied to the live event. The update request will be marked as Approved.
                  </p>
                  <div style={{ marginBottom: '14px' }}>
                    <label
                      htmlFor="update-approve-notes"
                      style={{
                        display: 'block',
                        fontSize: '12px',
                        fontWeight: 600,
                        color: 'var(--ep-text-primary)',
                        marginBottom: '6px',
                      }}
                    >
                      Reviewer Notes (Optional)
                    </label>
                    <textarea
                      id="update-approve-notes"
                      rows={2}
                      value={updateReviewNotes}
                      onChange={(e) => setUpdateReviewNotes(e.target.value)}
                      placeholder="Optional notes for organizer..."
                      disabled={updateActionInProgress}
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
                      onClick={handleApproveUpdateRequest}
                      disabled={updateActionInProgress}
                      style={{
                        backgroundColor: '#2E7D32',
                        color: '#ffffff',
                        border: 'none',
                        borderRadius: 'var(--ep-radius-btn)',
                        padding: '8px 18px',
                        fontSize: '13px',
                        fontWeight: 600,
                        cursor: updateActionInProgress ? 'not-allowed' : 'pointer',
                        opacity: updateActionInProgress ? 0.7 : 1,
                      }}
                    >
                      {updateActionInProgress ? 'Approving...' : 'Confirm Approval'}
                    </button>
                    <button
                      type="button"
                      onClick={() => setConfirmModeUpdate(null)}
                      disabled={updateActionInProgress}
                      className="ep-btn-secondary"
                      style={{ fontSize: '13px', padding: '8px 16px' }}
                    >
                      Cancel
                    </button>
                  </div>
                </div>
              )}

              {/* Reject Confirmation Box */}
              {updateConfirmMode === 'reject' && (
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
                    Confirm Event Update Rejection
                  </h4>
                  <p style={{
                    fontSize: '13px',
                    color: 'var(--ep-text-secondary)',
                    margin: '0 0 12px 0',
                    lineHeight: 1.5,
                  }}>
                    The update request will be rejected and the live event will remain completely unchanged. The organizer will see your reason on their event page.
                  </p>
                  <div style={{ marginBottom: '14px' }}>
                    <label
                      htmlFor="update-reject-notes"
                      style={{
                        display: 'block',
                        fontSize: '12px',
                        fontWeight: 600,
                        color: 'var(--ep-text-primary)',
                        marginBottom: '6px',
                      }}
                    >
                      Rejection Reason / Feedback <span style={{ color: 'var(--ep-danger)' }}>*</span>
                    </label>
                    <textarea
                      id="update-reject-notes"
                      rows={3}
                      value={updateReviewNotes}
                      onChange={(e) => setUpdateReviewNotes(e.target.value)}
                      placeholder="Explain what needs to be changed or why this update cannot be accepted..."
                      disabled={updateActionInProgress}
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
                      onClick={handleRejectUpdateRequest}
                      disabled={updateActionInProgress || !updateReviewNotes.trim()}
                      style={{
                        backgroundColor: 'var(--ep-danger)',
                        color: '#ffffff',
                        border: 'none',
                        borderRadius: 'var(--ep-radius-btn)',
                        padding: '8px 18px',
                        fontSize: '13px',
                        fontWeight: 600,
                        cursor: (updateActionInProgress || !updateReviewNotes.trim()) ? 'not-allowed' : 'pointer',
                        opacity: (updateActionInProgress || !updateReviewNotes.trim()) ? 0.6 : 1,
                      }}
                    >
                      {updateActionInProgress ? 'Rejecting...' : 'Confirm Rejection'}
                    </button>
                    <button
                      type="button"
                      onClick={() => setConfirmModeUpdate(null)}
                      disabled={updateActionInProgress}
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
            {!updateConfirmMode && (
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
                  onClick={handleCloseUpdateRequestReview}
                  disabled={updateActionInProgress}
                  className="ep-btn-secondary"
                  style={{ fontSize: '13px', padding: '8px 16px' }}
                >
                  Close
                </button>

                <div style={{ display: 'flex', gap: '10px' }}>
                  <button
                    type="button"
                    onClick={() => setConfirmModeUpdate('reject')}
                    disabled={updateActionInProgress}
                    style={{
                      backgroundColor: 'transparent',
                      color: 'var(--ep-danger)',
                      border: '1px solid var(--ep-danger)',
                      borderRadius: 'var(--ep-radius-btn)',
                      padding: '8px 16px',
                      fontSize: '13px',
                      fontWeight: 600,
                      cursor: updateActionInProgress ? 'not-allowed' : 'pointer',
                      transition: 'var(--ep-transition)',
                    }}
                  >
                    Reject Update
                  </button>

                  <button
                    type="button"
                    onClick={() => setConfirmModeUpdate('approve')}
                    disabled={updateActionInProgress}
                    style={{
                      backgroundColor: '#2E7D32',
                      color: '#ffffff',
                      border: 'none',
                      borderRadius: 'var(--ep-radius-btn)',
                      padding: '8px 20px',
                      fontSize: '13px',
                      fontWeight: 600,
                      cursor: updateActionInProgress ? 'not-allowed' : 'pointer',
                      transition: 'var(--ep-transition)',
                    }}
                  >
                    Approve Update
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Review Modal Panel (Cancellation Requests) */}
      {selectedCancellationRequest && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="cancellation-modal-title"
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
            if (e.target === e.currentTarget && !cancelActionInProgress) {
              handleCloseCancellationReview();
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
              <div>
                <h3
                  id="cancellation-modal-title"
                  style={{
                    fontSize: '18px',
                    fontWeight: 700,
                    color: 'var(--ep-text-primary)',
                    margin: '0 0 2px 0',
                  }}
                >
                  Review Cancellation Request
                </h3>
                <span style={{ fontSize: '13px', color: 'var(--ep-text-secondary)' }}>
                  {selectedCancellationRequest.eventTitle || 'Event'}
                </span>
              </div>
              <button
                type="button"
                onClick={handleCloseCancellationReview}
                disabled={cancelActionInProgress}
                style={{
                  background: 'none',
                  border: 'none',
                  cursor: cancelActionInProgress ? 'not-allowed' : 'pointer',
                  color: 'var(--ep-text-secondary)',
                  padding: '4px',
                  borderRadius: '4px',
                }}
                aria-label="Close modal"
              >
                <X size={20} />
              </button>
            </div>

            {/* Modal Body */}
            <div style={{
              padding: '24px',
              overflowY: 'auto',
              flex: 1,
              display: 'flex',
              flexDirection: 'column',
              gap: '20px',
            }}>
              {/* Error Notice */}
              {cancelActionError && (
                <div style={{
                  padding: '12px 16px',
                  backgroundColor: '#FFF5F5',
                  borderRadius: 'var(--ep-radius-container, 8px)',
                  border: '1px solid #FED7D7',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '10px',
                }}>
                  <AlertCircle size={16} color="var(--ep-danger)" style={{ flexShrink: 0 }} />
                  <span style={{ fontSize: '13px', color: 'var(--ep-danger)', fontWeight: 500 }}>
                    {cancelActionError}
                  </span>
                </div>
              )}

              {/* Event Details Card */}
              <div style={{
                padding: '16px',
                borderRadius: '8px',
                border: '1px solid var(--ep-border)',
                backgroundColor: 'var(--ep-canvas)',
                display: 'flex',
                gap: '16px',
                alignItems: 'flex-start',
              }}>
                <div style={{
                  width: '80px',
                  height: '80px',
                  borderRadius: '6px',
                  overflow: 'hidden',
                  backgroundColor: 'var(--ep-soft-accent)',
                  flexShrink: 0,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  border: '1px solid var(--ep-border)',
                }}>
                  {selectedCancellationRequest.imageUrl ? (
                    <img
                      src={selectedCancellationRequest.imageUrl}
                      alt={selectedCancellationRequest.eventTitle}
                      style={{ width: '100%', height: '100%', objectFit: 'cover' }}
                      onError={(e) => {
                        e.currentTarget.style.display = 'none';
                        e.currentTarget.parentElement.innerHTML = '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="var(--ep-primary)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="18" height="18" x="3" y="3" rx="2" ry="2"/><circle cx="9" cy="9" r="2"/><path d="m21 15-3.086-3.086a2 2 0 0 0-2.828 0L6 21"/></svg>';
                      }}
                    />
                  ) : (
                    <Calendar size={24} color="var(--ep-primary)" />
                  )}
                </div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '4px', flexWrap: 'wrap' }}>
                    <h4 style={{ fontSize: '16px', fontWeight: 700, margin: 0, color: 'var(--ep-text-primary)' }}>
                      {selectedCancellationRequest.eventTitle}
                    </h4>
                    <span style={{
                      fontSize: '11px',
                      fontWeight: 600,
                      backgroundColor: '#E8F5E9',
                      color: '#2E7D32',
                      padding: '2px 8px',
                      borderRadius: '9999px',
                    }}>
                      Status: {selectedCancellationRequest.eventStatus || 'Published'}
                    </span>
                  </div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '4px', fontSize: '13px', color: 'var(--ep-text-secondary)' }}>
                    {selectedCancellationRequest.eventDate && (
                      <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                        <Calendar size={13} />
                        <span>{formatEventDateTime(selectedCancellationRequest.eventDate)}</span>
                      </div>
                    )}
                    {selectedCancellationRequest.venue && (
                      <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                        <MapPin size={13} />
                        <span>{selectedCancellationRequest.venue}</span>
                      </div>
                    )}
                  </div>
                </div>
              </div>

              {/* Tickets Sold Impact Box */}
              <div style={{
                padding: '14px 16px',
                borderRadius: '8px',
                border: (selectedCancellationRequest.totalTicketsSold ?? 0) > 0 ? '1px solid #FED7D7' : '1px solid var(--ep-border)',
                backgroundColor: (selectedCancellationRequest.totalTicketsSold ?? 0) > 0 ? '#FFF5F5' : '#F9FAFB',
                display: 'flex',
                alignItems: 'flex-start',
                gap: '12px',
              }}>
                <Ticket size={20} color={(selectedCancellationRequest.totalTicketsSold ?? 0) > 0 ? '#DC2626' : 'var(--ep-text-secondary)'} style={{ marginTop: '2px', flexShrink: 0 }} />
                <div>
                  <div style={{ fontSize: '13px', fontWeight: 700, color: (selectedCancellationRequest.totalTicketsSold ?? 0) > 0 ? '#991B1B' : 'var(--ep-text-primary)', marginBottom: '2px' }}>
                    Ticket Sales Summary: {selectedCancellationRequest.totalTicketsSold ?? 0} tickets sold (out of {selectedCancellationRequest.totalCapacity ?? 0} total capacity)
                  </div>
                  <p style={{ margin: 0, fontSize: '12px', color: (selectedCancellationRequest.totalTicketsSold ?? 0) > 0 ? '#B91C1C' : 'var(--ep-text-secondary)', lineHeight: 1.4 }}>
                    {(selectedCancellationRequest.totalTicketsSold ?? 0) > 0
                      ? 'Caution: Tickets have already been purchased. Approving cancellation will mark the event as cancelled for all ticket holders. Ticket sales will be stopped permanently.'
                      : 'No tickets have been sold for this event yet. Cancelling will not impact existing bookings.'}
                  </p>
                </div>
              </div>

              {/* Organizer Cancellation Reason */}
              <div>
                <label style={{
                  display: 'block',
                  fontSize: '12px',
                  fontWeight: 700,
                  color: 'var(--ep-text-secondary)',
                  textTransform: 'uppercase',
                  letterSpacing: '0.04em',
                  marginBottom: '6px',
                }}>
                  Organizer's Cancellation Reason
                </label>
                <div style={{
                  backgroundColor: '#FFF8F6',
                  border: '1px solid #FFCCBC',
                  borderRadius: '8px',
                  padding: '14px 16px',
                  fontSize: '13px',
                  color: '#1D1D1F',
                  lineHeight: 1.5,
                  whiteSpace: 'pre-wrap',
                }}>
                  {selectedCancellationRequest.reason}
                </div>
                {selectedCancellationRequest.requestedAt && (
                  <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', marginTop: '6px' }}>
                    Submitted on {new Date(selectedCancellationRequest.requestedAt).toLocaleString('en-GB', { dateStyle: 'medium', timeStyle: 'short' })}
                  </div>
                )}
              </div>

              {/* Approve Confirmation Dialog (Step 10) */}
              {cancelConfirmMode === 'approve' && (
                <div style={{
                  padding: '18px',
                  backgroundColor: '#FFF5F5',
                  borderRadius: '10px',
                  border: '1px solid #FED7D7',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: '12px',
                }}>
                  <div>
                    <h4 style={{
                      fontSize: '15px',
                      fontWeight: 700,
                      color: '#B91C1C',
                      margin: '0 0 6px 0',
                    }}>
                      Approve event cancellation?
                    </h4>
                    <p style={{
                      fontSize: '14px',
                      fontWeight: 600,
                      color: 'var(--ep-text-primary)',
                      margin: '0 0 4px 0',
                    }}>
                      {selectedCancellationRequest.eventTitle}
                    </p>
                    <p style={{
                      fontSize: '13px',
                      color: 'var(--ep-text-secondary)',
                      margin: 0,
                      lineHeight: 1.5,
                    }}>
                      This will mark the event as cancelled and stop new ticket purchases.
                    </p>
                  </div>

                  <div style={{ display: 'flex', gap: '10px', marginTop: '4px' }}>
                    <button
                      type="button"
                      onClick={() => setCancelConfirmMode(null)}
                      disabled={cancelActionInProgress}
                      className="ep-btn-secondary"
                      style={{ fontSize: '13px', padding: '8px 18px' }}
                    >
                      Keep Event
                    </button>
                    <button
                      type="button"
                      onClick={handleApproveCancellation}
                      disabled={cancelActionInProgress}
                      style={{
                        backgroundColor: '#DC2626',
                        color: '#ffffff',
                        border: 'none',
                        borderRadius: 'var(--ep-radius-btn)',
                        padding: '8px 20px',
                        fontSize: '13px',
                        fontWeight: 600,
                        cursor: cancelActionInProgress ? 'not-allowed' : 'pointer',
                        transition: 'var(--ep-transition)',
                      }}
                    >
                      {cancelActionInProgress ? 'Approving...' : 'Approve Cancellation'}
                    </button>
                  </div>
                </div>
              )}

              {/* Reject Confirmation Dialog */}
              {cancelConfirmMode === 'reject' && (
                <div style={{
                  padding: '18px',
                  backgroundColor: '#FFF8F6',
                  borderRadius: '10px',
                  border: '1px solid #FFCCBC',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: '12px',
                }}>
                  <div>
                    <h4 style={{
                      fontSize: '15px',
                      fontWeight: 700,
                      color: 'var(--ep-primary)',
                      margin: '0 0 6px 0',
                    }}>
                      Reject event cancellation request?
                    </h4>
                    <p style={{
                      fontSize: '13px',
                      color: 'var(--ep-text-secondary)',
                      margin: 0,
                      lineHeight: 1.5,
                    }}>
                      The event will remain active and published. Ticket sales will remain eligible. Please provide a reason below so the organizer knows why the request was rejected.
                    </p>
                  </div>

                  <div>
                    <label
                      htmlFor="cancellation-reject-notes"
                      style={{
                        display: 'block',
                        fontSize: '12px',
                        fontWeight: 600,
                        color: 'var(--ep-text-primary)',
                        marginBottom: '6px',
                      }}
                    >
                      Rejection Reason <span style={{ color: 'var(--ep-danger)' }}>*</span>
                    </label>
                    <textarea
                      id="cancellation-reject-notes"
                      rows={3}
                      value={cancelReviewNotes}
                      onChange={(e) => setCancelReviewNotes(e.target.value)}
                      placeholder="e.g. Event can proceed based on the information provided..."
                      disabled={cancelActionInProgress}
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
                      onClick={() => setCancelConfirmMode(null)}
                      disabled={cancelActionInProgress}
                      className="ep-btn-secondary"
                      style={{ fontSize: '13px', padding: '8px 16px' }}
                    >
                      Cancel
                    </button>
                    <button
                      type="button"
                      onClick={handleRejectCancellation}
                      disabled={cancelActionInProgress || !cancelReviewNotes.trim()}
                      style={{
                        backgroundColor: 'var(--ep-danger)',
                        color: '#ffffff',
                        border: 'none',
                        borderRadius: 'var(--ep-radius-btn)',
                        padding: '8px 18px',
                        fontSize: '13px',
                        fontWeight: 600,
                        cursor: (cancelActionInProgress || !cancelReviewNotes.trim()) ? 'not-allowed' : 'pointer',
                        opacity: (cancelActionInProgress || !cancelReviewNotes.trim()) ? 0.6 : 1,
                      }}
                    >
                      {cancelActionInProgress ? 'Rejecting...' : 'Confirm Rejection'}
                    </button>
                  </div>
                </div>
              )}
            </div>

            {/* Modal Footer (When not in confirm mode) */}
            {!cancelConfirmMode && (
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
                  onClick={handleCloseCancellationReview}
                  disabled={cancelActionInProgress}
                  className="ep-btn-secondary"
                  style={{ fontSize: '13px', padding: '8px 16px' }}
                >
                  Close
                </button>

                <div style={{ display: 'flex', gap: '10px' }}>
                  <button
                    type="button"
                    onClick={() => setCancelConfirmMode('reject')}
                    disabled={cancelActionInProgress}
                    style={{
                      backgroundColor: 'transparent',
                      color: 'var(--ep-danger)',
                      border: '1px solid var(--ep-danger)',
                      borderRadius: 'var(--ep-radius-btn)',
                      padding: '8px 16px',
                      fontSize: '13px',
                      fontWeight: 600,
                      cursor: cancelActionInProgress ? 'not-allowed' : 'pointer',
                      transition: 'var(--ep-transition)',
                    }}
                  >
                    Reject Cancellation
                  </button>

                  <button
                    type="button"
                    onClick={() => setCancelConfirmMode('approve')}
                    disabled={cancelActionInProgress}
                    style={{
                      backgroundColor: '#DC2626',
                      color: '#ffffff',
                      border: 'none',
                      borderRadius: 'var(--ep-radius-btn)',
                      padding: '8px 20px',
                      fontSize: '13px',
                      fontWeight: 600,
                      cursor: cancelActionInProgress ? 'not-allowed' : 'pointer',
                      transition: 'var(--ep-transition)',
                    }}
                  >
                    Approve Cancellation
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
