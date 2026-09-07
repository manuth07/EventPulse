import React, { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { useAuth } from '../../context/AuthContext';
import {
  getAdminOrganizerApplications,
  getAdminOrganizerApplicationById,
  approveOrganizerApplication,
  rejectOrganizerApplication,
} from '../../services/organizerApplicationService';
import {
  ArrowLeft,
  Clock,
  CheckCircle2,
  XCircle,
  AlertCircle,
  RefreshCw,
  Building2,
  User,
  ExternalLink,
  ShieldCheck,
  X,
  ChevronRight,
  Info,
  Phone,
  Mail,
  Calendar,
  FileText,
} from 'lucide-react';

function formatDate(isoString) {
  if (!isoString) return '—';
  try {
    const date = new Date(isoString);
    return date.toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return isoString;
  }
}

export function AdminOrganizerApplications() {
  const { accessToken } = useAuth();

  const [applications, setApplications] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);
  const [feedback, setFeedback] = useState(null);

  // Active filter tab: 'Pending' | 'Approved' | 'Rejected' | 'All'
  const [statusFilter, setStatusFilter] = useState('Pending');

  // Review Modal state
  const [selectedApplication, setSelectedApplication] = useState(null);
  const [loadingDetails, setLoadingDetails] = useState(false);
  const [activeActionTab, setActiveActionTab] = useState(null); // 'approve' | 'reject' | null
  const [approveComment, setApproveComment] = useState('');
  const [rejectComment, setRejectComment] = useState('');
  const [actionInProgress, setActionInProgress] = useState(false);
  const [actionError, setActionError] = useState(null);

  const getEffectiveToken = useCallback(() => {
    return accessToken || sessionStorage.getItem('ep_access_token');
  }, [accessToken]);

  const loadApplications = useCallback(
    async (isManualRefresh = false) => {
      if (isManualRefresh) {
        setRefreshing(true);
      } else {
        setLoading(true);
      }
      setError(null);

      const token = getEffectiveToken();
      if (!token) {
        setError('Administrator authentication token not found. Please log in.');
        setLoading(false);
        setRefreshing(false);
        return;
      }

      try {
        const filterParam = statusFilter === 'All' ? null : statusFilter;
        const data = await getAdminOrganizerApplications(filterParam, token);
        setApplications(Array.isArray(data) ? data : []);
      } catch (err) {
        setError(err.message || 'Failed to load organizer applications.');
      } finally {
        setLoading(false);
        setRefreshing(false);
      }
    },
    [getEffectiveToken, statusFilter]
  );

  useEffect(() => {
    loadApplications();
  }, [loadApplications]);

  const handleOpenReview = async (app) => {
    setActionError(null);
    setActiveActionTab(null);
    setApproveComment('');
    setRejectComment('');
    setSelectedApplication(app);
    setLoadingDetails(true);

    const token = getEffectiveToken();
    if (token && app.id) {
      try {
        const fresh = await getAdminOrganizerApplicationById(app.id, token);
        if (fresh) {
          setSelectedApplication(fresh);
        }
      } catch {
        // Fallback to app from list
      } finally {
        setLoadingDetails(false);
      }
    } else {
      setLoadingDetails(false);
    }
  };

  const handleCloseReview = () => {
    if (actionInProgress) return;
    setSelectedApplication(null);
    setActiveActionTab(null);
    setActionError(null);
    setApproveComment('');
    setRejectComment('');
  };

  const handleApprove = async () => {
    if (!selectedApplication || actionInProgress) return;
    setActionInProgress(true);
    setActionError(null);

    const token = getEffectiveToken();
    try {
      const payload = {
        reviewComment: approveComment.trim() ? approveComment.trim() : null,
      };

      await approveOrganizerApplication(selectedApplication.id, payload, token);
      const appName = selectedApplication.organizerName;

      handleCloseReview();
      setFeedback({
        type: 'success',
        message: `Organizer role successfully granted to "${appName}". The user can now access Organizer features.`,
      });

      // Reload list
      await loadApplications();
    } catch (err) {
      setActionError(err.message || 'Failed to approve organizer application.');
    } finally {
      setActionInProgress(false);
    }
  };

  const handleReject = async () => {
    if (!selectedApplication || actionInProgress) return;
    const trimmed = rejectComment.trim();
    if (!trimmed) {
      setActionError('Feedback comment is required when rejecting an application.');
      return;
    }

    setActionInProgress(true);
    setActionError(null);

    const token = getEffectiveToken();
    try {
      const payload = {
        reviewComment: trimmed,
      };

      await rejectOrganizerApplication(selectedApplication.id, payload, token);
      const appName = selectedApplication.organizerName;

      handleCloseReview();
      setFeedback({
        type: 'info',
        message: `Application for "${appName}" has been rejected. Feedback has been recorded for the user to edit and resubmit.`,
      });

      // Reload list
      await loadApplications();
    } catch (err) {
      setActionError(err.message || 'Failed to reject organizer application.');
    } finally {
      setActionInProgress(false);
    }
  };

  // Status badge styling helper
  const renderStatusBadge = (status) => {
    const s = (status || '').toLowerCase();
    if (s === 'approved') {
      return (
        <span
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: '5px',
            backgroundColor: '#E8F5E9',
            color: '#2E7D32',
            padding: '3px 9px',
            borderRadius: 'var(--ep-radius-pill)',
            fontSize: '11px',
            fontWeight: 700,
            letterSpacing: '0.04em',
            textTransform: 'uppercase',
          }}
        >
          <CheckCircle2 size={12} />
          <span>Approved</span>
        </span>
      );
    }
    if (s === 'rejected') {
      return (
        <span
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: '5px',
            backgroundColor: '#FFF0EF',
            color: 'var(--ep-danger)',
            padding: '3px 9px',
            borderRadius: 'var(--ep-radius-pill)',
            fontSize: '11px',
            fontWeight: 700,
            letterSpacing: '0.04em',
            textTransform: 'uppercase',
          }}
        >
          <XCircle size={12} />
          <span>Rejected</span>
        </span>
      );
    }
    return (
      <span
        style={{
          display: 'inline-flex',
          alignItems: 'center',
          gap: '5px',
          backgroundColor: 'var(--ep-soft-accent)',
          color: 'var(--ep-primary)',
          padding: '3px 9px',
          borderRadius: 'var(--ep-radius-pill)',
          fontSize: '11px',
          fontWeight: 700,
          letterSpacing: '0.04em',
          textTransform: 'uppercase',
        }}
      >
        <Clock size={12} />
        <span>Pending Review</span>
      </span>
    );
  };

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
          paddingTop: '32px',
          paddingBottom: '64px',
        }}
      >
        {/* Navigation Breadcrumb */}
        <div style={{ marginBottom: '24px' }}>
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
            }}
          >
            <ArrowLeft size={14} />
            <span>Back to Admin Dashboard</span>
          </Link>
        </div>

        {/* Action Feedback Banner */}
        {feedback && (
          <div
            style={{
              marginBottom: '24px',
              padding: '16px 20px',
              borderRadius: 'var(--ep-radius-container, 12px)',
              backgroundColor: feedback.type === 'success' ? '#E8F5E9' : '#FFF0E6',
              border: `1px solid ${feedback.type === 'success' ? '#C8E6C9' : '#FFE0CC'}`,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: '12px',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
              {feedback.type === 'success' ? (
                <CheckCircle2 size={18} color="#2E7D32" />
              ) : (
                <Info size={18} color="var(--ep-primary)" />
              )}
              <span
                style={{
                  fontSize: '14px',
                  fontWeight: 600,
                  color: feedback.type === 'success' ? '#1B5E20' : 'var(--ep-text-primary)',
                }}
              >
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
              aria-label="Dismiss message"
            >
              <X size={16} />
            </button>
          </div>
        )}

        {/* Header Section */}
        <div
          style={{
            display: 'flex',
            alignItems: 'flex-start',
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
                backgroundColor: 'var(--ep-canvas)',
                color: 'var(--ep-text-primary)',
                padding: '4px 10px',
                borderRadius: 'var(--ep-radius-pill)',
                fontSize: '12px',
                fontWeight: 600,
                border: '1px solid var(--ep-border)',
                marginBottom: '8px',
              }}
            >
              <ShieldCheck size={13} color="var(--ep-primary)" />
              <span>Organizer Verification</span>
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
              Organizer Applications
            </h1>
            <p
              style={{
                fontSize: '14px',
                color: 'var(--ep-text-secondary)',
                marginTop: '4px',
                margin: 0,
              }}
            >
              Review customer requests to list events, inspect verification details, and manage Organizer role access.
            </p>
          </div>

          <button
            type="button"
            onClick={() => loadApplications(true)}
            disabled={loading || refreshing}
            className="ep-btn-secondary"
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '8px',
              fontSize: '13px',
              padding: '8px 16px',
              cursor: loading || refreshing ? 'not-allowed' : 'pointer',
            }}
          >
            <RefreshCw
              size={14}
              style={{ animation: refreshing ? 'ep-spin 0.8s linear infinite' : 'none' }}
            />
            <span>Refresh</span>
          </button>
        </div>

        {/* Filter Tabs */}
        <div
          style={{
            display: 'flex',
            gap: '8px',
            borderBottom: '1px solid var(--ep-border)',
            marginBottom: '24px',
            overflowX: 'auto',
          }}
        >
          {['Pending', 'Approved', 'Rejected', 'All'].map((tab) => {
            const isActive = statusFilter === tab;
            return (
              <button
                key={tab}
                type="button"
                onClick={() => setStatusFilter(tab)}
                style={{
                  background: 'none',
                  border: 'none',
                  borderBottom: isActive ? '2px solid var(--ep-primary)' : '2px solid transparent',
                  padding: '10px 16px',
                  fontSize: '14px',
                  fontWeight: isActive ? 700 : 500,
                  color: isActive ? 'var(--ep-primary)' : 'var(--ep-text-secondary)',
                  cursor: 'pointer',
                  transition: 'all 0.15s ease',
                  marginBottom: '-1px',
                }}
              >
                {tab === 'Pending' && 'Pending Review'}
                {tab === 'Approved' && 'Approved'}
                {tab === 'Rejected' && 'Rejected'}
                {tab === 'All' && 'All Applications'}
              </button>
            );
          })}
        </div>

        {/* Error Alert */}
        {error && (
          <div
            style={{
              padding: '16px 20px',
              backgroundColor: '#FFF0EF',
              border: '1px solid #FFCDD2',
              borderRadius: 'var(--ep-radius-card)',
              color: 'var(--ep-danger)',
              display: 'flex',
              alignItems: 'center',
              gap: '12px',
              marginBottom: '24px',
            }}
          >
            <AlertCircle size={20} />
            <span style={{ fontSize: '14px', fontWeight: 500 }}>{error}</span>
          </div>
        )}

        {/* Content: Loading vs Empty vs List */}
        {loading && !refreshing ? (
          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              padding: '60px 24px',
              textAlign: 'center',
              boxShadow: 'var(--ep-shadow-card)',
            }}
          >
            <div
              style={{
                width: '36px',
                height: '36px',
                border: '3px solid var(--ep-border)',
                borderTopColor: 'var(--ep-primary)',
                borderRadius: '50%',
                animation: 'ep-spin 0.8s linear infinite',
                margin: '0 auto 16px',
              }}
            />
            <p style={{ margin: 0, fontSize: '14px', color: 'var(--ep-text-secondary)' }}>
              Loading organizer applications…
            </p>
          </div>
        ) : applications.length === 0 ? (
          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              padding: '60px 24px',
              textAlign: 'center',
              boxShadow: 'var(--ep-shadow-card)',
            }}
          >
            <div
              style={{
                width: '52px',
                height: '52px',
                borderRadius: '50%',
                backgroundColor: 'var(--ep-canvas)',
                color: 'var(--ep-text-secondary)',
                display: 'inline-flex',
                alignItems: 'center',
                justifyContent: 'center',
                marginBottom: '16px',
              }}
            >
              <ShieldCheck size={24} />
            </div>
            <h3 style={{ fontSize: '16px', fontWeight: 600, color: 'var(--ep-text-primary)', margin: '0 0 6px' }}>
              No {statusFilter === 'All' ? '' : statusFilter.toLowerCase()} applications
            </h3>
            <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: 0 }}>
              {statusFilter === 'Pending'
                ? 'Great news! There are currently no pending organizer applications awaiting review.'
                : `There are currently no organizer applications with "${statusFilter}" status.`}
            </p>
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
            {applications.map((app) => (
              <div
                key={app.id}
                style={{
                  backgroundColor: '#ffffff',
                  borderRadius: 'var(--ep-radius-card)',
                  border: '1px solid var(--ep-border)',
                  boxShadow: 'var(--ep-shadow-card)',
                  padding: '24px',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: '16px',
                  transition: 'border-color 0.15s ease',
                }}
              >
                {/* Top Row: Info & Status */}
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'flex-start',
                    justifyContent: 'space-between',
                    flexWrap: 'wrap',
                    gap: '12px',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                    <div
                      style={{
                        width: '44px',
                        height: '44px',
                        borderRadius: '10px',
                        backgroundColor: 'var(--ep-canvas)',
                        border: '1px solid var(--ep-border)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        color: 'var(--ep-text-primary)',
                      }}
                    >
                      {app.organizerType === 'Organization' ? (
                        <Building2 size={22} color="var(--ep-primary)" />
                      ) : (
                        <User size={22} color="var(--ep-primary)" />
                      )}
                    </div>
                    <div>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <h3
                          style={{
                            fontSize: '17px',
                            fontWeight: 700,
                            color: 'var(--ep-text-primary)',
                            margin: 0,
                          }}
                        >
                          {app.organizerName}
                        </h3>
                        <span
                          style={{
                            fontSize: '11px',
                            padding: '2px 8px',
                            borderRadius: 'var(--ep-radius-pill)',
                            backgroundColor: 'var(--ep-canvas)',
                            border: '1px solid var(--ep-border)',
                            color: 'var(--ep-text-secondary)',
                            fontWeight: 500,
                          }}
                        >
                          {app.organizerType}
                        </span>
                      </div>
                      <div
                        style={{
                          fontSize: '13px',
                          color: 'var(--ep-text-secondary)',
                          marginTop: '3px',
                          display: 'flex',
                          alignItems: 'center',
                          gap: '12px',
                          flexWrap: 'wrap',
                        }}
                      >
                        {app.accountEmail && (
                          <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                            <Mail size={13} />
                            <span>{app.accountEmail}</span>
                          </span>
                        )}
                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                          <Phone size={13} />
                          <span>{app.contactNumber}</span>
                        </span>
                      </div>
                    </div>
                  </div>

                  <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                    {renderStatusBadge(app.status)}
                    <button
                      type="button"
                      onClick={() => handleOpenReview(app)}
                      className="ep-btn-primary"
                      style={{
                        padding: '8px 16px',
                        fontSize: '13px',
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '6px',
                      }}
                    >
                      <span>Review</span>
                      <ChevronRight size={14} />
                    </button>
                  </div>
                </div>

                {/* Description Snippet */}
                {app.description && (
                  <p
                    style={{
                      margin: 0,
                      fontSize: '13px',
                      color: 'var(--ep-text-secondary)',
                      lineHeight: '1.5',
                      display: '-webkit-box',
                      WebkitLineClamp: 2,
                      WebkitBoxOrient: 'vertical',
                      overflow: 'hidden',
                    }}
                  >
                    {app.description}
                  </p>
                )}

                {/* Bottom Row: Metadata */}
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    fontSize: '12px',
                    color: 'var(--ep-text-secondary)',
                    borderTop: '1px solid var(--ep-border)',
                    paddingTop: '12px',
                    flexWrap: 'wrap',
                    gap: '8px',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                    <Calendar size={13} />
                    <span>Submitted: {formatDate(app.submittedAt)}</span>
                  </div>

                  {app.website && (
                    <a
                      href={app.website.startsWith('http') ? app.website : `https://${app.website}`}
                      target="_blank"
                      rel="noopener noreferrer"
                      style={{
                        color: 'var(--ep-primary)',
                        textDecoration: 'none',
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '4px',
                        fontSize: '12px',
                      }}
                    >
                      <span>{app.website}</span>
                      <ExternalLink size={12} />
                    </a>
                  )}

                  {app.reviewedAt && (
                    <div>
                      Reviewed: {formatDate(app.reviewedAt)}
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}

        {/* Review Modal / Drawer */}
        {selectedApplication && (
          <div
            style={{
              position: 'fixed',
              inset: 0,
              backgroundColor: 'rgba(0, 0, 0, 0.5)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              zIndex: 1000,
              padding: '20px',
            }}
            onClick={handleCloseReview}
          >
            <div
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card)',
                maxWidth: '680px',
                width: '100%',
                maxHeight: '90vh',
                display: 'flex',
                flexDirection: 'column',
                boxShadow: '0 20px 40px rgba(0, 0, 0, 0.2)',
                overflow: 'hidden',
              }}
              onClick={(e) => e.stopPropagation()}
            >
              {/* Modal Header */}
              <div
                style={{
                  padding: '20px 24px',
                  borderBottom: '1px solid var(--ep-border)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                  <ShieldCheck size={20} color="var(--ep-primary)" />
                  <h2 style={{ fontSize: '18px', fontWeight: 700, margin: 0, color: 'var(--ep-text-primary)' }}>
                    Review Organizer Application
                  </h2>
                </div>
                <button
                  type="button"
                  onClick={handleCloseReview}
                  disabled={actionInProgress}
                  style={{
                    background: 'none',
                    border: 'none',
                    cursor: 'pointer',
                    color: 'var(--ep-text-secondary)',
                    padding: '4px',
                  }}
                >
                  <X size={20} />
                </button>
              </div>

              {/* Modal Body */}
              <div
                style={{
                  padding: '24px',
                  overflowY: 'auto',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: '20px',
                }}
              >
                {/* Header Summary */}
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    backgroundColor: 'var(--ep-canvas)',
                    padding: '16px',
                    borderRadius: '10px',
                    border: '1px solid var(--ep-border)',
                  }}
                >
                  <div>
                    <h3 style={{ margin: '0 0 4px', fontSize: '17px', fontWeight: 700 }}>
                      {selectedApplication.organizerName}
                    </h3>
                    <div style={{ fontSize: '13px', color: 'var(--ep-text-secondary)' }}>
                      Type: <strong>{selectedApplication.organizerType}</strong>
                    </div>
                  </div>
                  <div>{renderStatusBadge(selectedApplication.status)}</div>
                </div>

                {/* Details Grid */}
                <div
                  style={{
                    display: 'grid',
                    gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
                    gap: '16px',
                  }}
                >
                  <div>
                    <div style={{ fontSize: '11px', textTransform: 'uppercase', color: 'var(--ep-text-secondary)', fontWeight: 600 }}>
                      Applicant Email
                    </div>
                    <div style={{ fontSize: '14px', fontWeight: 500, color: 'var(--ep-text-primary)', marginTop: '2px' }}>
                      {selectedApplication.accountEmail || '—'}
                    </div>
                  </div>

                  <div>
                    <div style={{ fontSize: '11px', textTransform: 'uppercase', color: 'var(--ep-text-secondary)', fontWeight: 600 }}>
                      Contact Number
                    </div>
                    <div style={{ fontSize: '14px', fontWeight: 500, color: 'var(--ep-text-primary)', marginTop: '2px' }}>
                      {selectedApplication.contactNumber}
                    </div>
                  </div>

                  <div>
                    <div style={{ fontSize: '11px', textTransform: 'uppercase', color: 'var(--ep-text-secondary)', fontWeight: 600 }}>
                      Submitted Date
                    </div>
                    <div style={{ fontSize: '14px', fontWeight: 500, color: 'var(--ep-text-primary)', marginTop: '2px' }}>
                      {formatDate(selectedApplication.submittedAt)}
                    </div>
                  </div>

                  <div>
                    <div style={{ fontSize: '11px', textTransform: 'uppercase', color: 'var(--ep-text-secondary)', fontWeight: 600 }}>
                      Website / Social
                    </div>
                    <div style={{ fontSize: '14px', fontWeight: 500, marginTop: '2px' }}>
                      {selectedApplication.website ? (
                        <a
                          href={selectedApplication.website.startsWith('http') ? selectedApplication.website : `https://${selectedApplication.website}`}
                          target="_blank"
                          rel="noopener noreferrer"
                          style={{ color: 'var(--ep-primary)', textDecoration: 'none', display: 'inline-flex', alignItems: 'center', gap: '4px' }}
                        >
                          <span>{selectedApplication.website}</span>
                          <ExternalLink size={12} />
                        </a>
                      ) : (
                        <span style={{ color: 'var(--ep-text-secondary)' }}>None provided</span>
                      )}
                    </div>
                  </div>
                </div>

                {/* Description */}
                <div>
                  <div style={{ fontSize: '11px', textTransform: 'uppercase', color: 'var(--ep-text-secondary)', fontWeight: 600, marginBottom: '6px' }}>
                    About / Experience
                  </div>
                  <div
                    style={{
                      fontSize: '13px',
                      color: 'var(--ep-text-primary)',
                      lineHeight: '1.6',
                      backgroundColor: 'var(--ep-canvas)',
                      padding: '14px',
                      borderRadius: '8px',
                      border: '1px solid var(--ep-border)',
                      whiteSpace: 'pre-wrap',
                    }}
                  >
                    {selectedApplication.description}
                  </div>
                </div>

                {/* Existing Review Info if Already Reviewed */}
                {selectedApplication.status !== 'Pending' && (
                  <div
                    style={{
                      backgroundColor: selectedApplication.status === 'Approved' ? '#F1F8E9' : '#FFF8F7',
                      border: `1px solid ${selectedApplication.status === 'Approved' ? '#DCEDC8' : '#FFEBEA'}`,
                      borderRadius: '8px',
                      padding: '16px',
                    }}
                  >
                    <div
                      style={{
                        fontSize: '12px',
                        fontWeight: 700,
                        color: selectedApplication.status === 'Approved' ? '#2E7D32' : 'var(--ep-danger)',
                        textTransform: 'uppercase',
                        marginBottom: '6px',
                      }}
                    >
                      Previous Review Decision
                    </div>
                    <div style={{ fontSize: '13px', color: 'var(--ep-text-primary)', marginBottom: '6px' }}>
                      <strong>Reviewed on:</strong> {formatDate(selectedApplication.reviewedAt)}
                    </div>
                    {selectedApplication.reviewComment && (
                      <div style={{ fontSize: '13px', color: 'var(--ep-text-primary)', lineHeight: '1.5' }}>
                        <strong>Feedback:</strong> {selectedApplication.reviewComment}
                      </div>
                    )}
                  </div>
                )}

                {/* Action Form for Pending Applications */}
                {selectedApplication.status === 'Pending' && (
                  <div
                    style={{
                      borderTop: '1px solid var(--ep-border)',
                      paddingTop: '20px',
                    }}
                  >
                    {actionError && (
                      <div
                        style={{
                          padding: '12px 14px',
                          backgroundColor: '#FFF0EF',
                          border: '1px solid #FFCDD2',
                          borderRadius: '8px',
                          color: 'var(--ep-danger)',
                          fontSize: '13px',
                          marginBottom: '16px',
                          display: 'flex',
                          alignItems: 'center',
                          gap: '8px',
                        }}
                      >
                        <AlertCircle size={16} />
                        <span>{actionError}</span>
                      </div>
                    )}

                    {/* Action Selector Buttons */}
                    {!activeActionTab && (
                      <div style={{ display: 'flex', gap: '12px' }}>
                        <button
                          type="button"
                          onClick={() => {
                            setActiveActionTab('approve');
                            setActionError(null);
                          }}
                          className="ep-btn-primary"
                          style={{
                            flex: 1,
                            padding: '12px',
                            display: 'inline-flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            gap: '8px',
                            backgroundColor: '#2E7D32',
                            borderColor: '#2E7D32',
                          }}
                        >
                          <CheckCircle2 size={16} />
                          <span>Approve & Grant Organizer Role</span>
                        </button>
                        <button
                          type="button"
                          onClick={() => {
                            setActiveActionTab('reject');
                            setActionError(null);
                          }}
                          className="ep-btn-secondary"
                          style={{
                            flex: 1,
                            padding: '12px',
                            display: 'inline-flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            gap: '8px',
                            color: 'var(--ep-danger)',
                            borderColor: 'var(--ep-danger)',
                          }}
                        >
                          <XCircle size={16} />
                          <span>Reject Application</span>
                        </button>
                      </div>
                    )}

                    {/* Approve Mode */}
                    {activeActionTab === 'approve' && (
                      <div
                        style={{
                          backgroundColor: '#F9FBE7',
                          border: '1px solid #E6EE9C',
                          borderRadius: '10px',
                          padding: '18px',
                        }}
                      >
                        <h4 style={{ margin: '0 0 6px', fontSize: '15px', color: '#33691E' }}>
                          Confirm Approval
                        </h4>
                        <p style={{ margin: '0 0 14px', fontSize: '13px', color: 'var(--ep-text-secondary)', lineHeight: '1.4' }}>
                          This will atomically grant the <strong>Organizer</strong> role to <strong>{selectedApplication.organizerName}</strong> while preserving their Customer permissions.
                        </p>

                        <div style={{ marginBottom: '14px' }}>
                          <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '4px' }}>
                            Optional Approval Note
                          </label>
                          <input
                            type="text"
                            value={approveComment}
                            onChange={(e) => setApproveComment(e.target.value)}
                            placeholder="e.g. Verified business registry documents."
                            maxLength={1000}
                            className="ep-input"
                            style={{ fontSize: '13px' }}
                            disabled={actionInProgress}
                          />
                        </div>

                        <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                          <button
                            type="button"
                            onClick={() => {
                              setActiveActionTab(null);
                              setActionError(null);
                            }}
                            disabled={actionInProgress}
                            className="ep-btn-secondary"
                            style={{ fontSize: '13px', padding: '8px 16px' }}
                          >
                            Cancel
                          </button>
                          <button
                            type="button"
                            onClick={handleApprove}
                            disabled={actionInProgress}
                            className="ep-btn-primary"
                            style={{
                              fontSize: '13px',
                              padding: '8px 18px',
                              backgroundColor: '#2E7D32',
                              borderColor: '#2E7D32',
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: '6px',
                            }}
                          >
                            {actionInProgress ? (
                              <>
                                <div style={{ width: '12px', height: '12px', border: '2px solid #ffffff', borderTopColor: 'transparent', borderRadius: '50%', animation: 'ep-spin 0.8s linear infinite' }} />
                                <span>Granting Role…</span>
                              </>
                            ) : (
                              <>
                                <CheckCircle2 size={14} />
                                <span>Confirm Approval</span>
                              </>
                            )}
                          </button>
                        </div>
                      </div>
                    )}

                    {/* Reject Mode */}
                    {activeActionTab === 'reject' && (
                      <div
                        style={{
                          backgroundColor: '#FFF8F7',
                          border: '1px solid #FFCDD2',
                          borderRadius: '10px',
                          padding: '18px',
                        }}
                      >
                        <h4 style={{ margin: '0 0 6px', fontSize: '15px', color: 'var(--ep-danger)' }}>
                          Reject Application
                        </h4>
                        <p style={{ margin: '0 0 14px', fontSize: '13px', color: 'var(--ep-text-secondary)', lineHeight: '1.4' }}>
                          Please explain why this application cannot be approved. The feedback will be presented to the applicant so they can make corrections and resubmit.
                        </p>

                        <div style={{ marginBottom: '14px' }}>
                          <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '4px' }}>
                            Feedback / Correction Notice <span style={{ color: 'var(--ep-danger)' }}>*</span>
                          </label>
                          <textarea
                            value={rejectComment}
                            onChange={(e) => {
                              setRejectComment(e.target.value);
                              if (actionError) setActionError(null);
                            }}
                            rows={3}
                            placeholder="e.g. Please provide your business registration number or a valid website URL verifying past events."
                            maxLength={1000}
                            className="ep-input"
                            style={{ fontSize: '13px', resize: 'vertical' }}
                            disabled={actionInProgress}
                          />
                          <div style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', textAlign: 'right', marginTop: '4px' }}>
                            {rejectComment.length} / 1000
                          </div>
                        </div>

                        <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                          <button
                            type="button"
                            onClick={() => {
                              setActiveActionTab(null);
                              setActionError(null);
                            }}
                            disabled={actionInProgress}
                            className="ep-btn-secondary"
                            style={{ fontSize: '13px', padding: '8px 16px' }}
                          >
                            Cancel
                          </button>
                          <button
                            type="button"
                            onClick={handleReject}
                            disabled={actionInProgress}
                            className="ep-btn-primary"
                            style={{
                              fontSize: '13px',
                              padding: '8px 18px',
                              backgroundColor: 'var(--ep-danger)',
                              borderColor: 'var(--ep-danger)',
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: '6px',
                            }}
                          >
                            {actionInProgress ? (
                              <>
                                <div style={{ width: '12px', height: '12px', border: '2px solid #ffffff', borderTopColor: 'transparent', borderRadius: '50%', animation: 'ep-spin 0.8s linear infinite' }} />
                                <span>Rejecting…</span>
                              </>
                            ) : (
                              <>
                                <XCircle size={14} />
                                <span>Confirm Rejection</span>
                              </>
                            )}
                          </button>
                        </div>
                      </div>
                    )}
                  </div>
                )}
              </div>

              {/* Modal Footer */}
              <div
                style={{
                  padding: '14px 24px',
                  borderTop: '1px solid var(--ep-border)',
                  display: 'flex',
                  justifyContent: 'flex-end',
                  backgroundColor: 'var(--ep-canvas)',
                }}
              >
                <button
                  type="button"
                  onClick={handleCloseReview}
                  disabled={actionInProgress}
                  className="ep-btn-secondary"
                  style={{ fontSize: '13px', padding: '8px 18px' }}
                >
                  Close
                </button>
              </div>
            </div>
          </div>
        )}
      </main>
      <style>{`
        @keyframes ep-spin {
          0% { transform: rotate(0deg); }
          100% { transform: rotate(360deg); }
        }
      `}</style>
    </div>
  );
}
