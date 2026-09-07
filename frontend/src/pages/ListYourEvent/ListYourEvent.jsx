import React, { useState, useEffect, useCallback } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import {
  Clock,
  CheckCircle2,
  XCircle,
  AlertCircle,
  Building2,
  User,
  ExternalLink,
  ChevronRight,
  ArrowLeft,
  RotateCcw,
  LogOut,
  Sparkles,
} from 'lucide-react';
import { Header } from '../../components/Header/Header';
import { useAuth } from '../../context/AuthContext';
import {
  getMyOrganizerApplication,
  submitOrganizerApplication,
  resubmitOrganizerApplication,
} from '../../services/organizerApplicationService';

export function ListYourEvent() {
  const navigate = useNavigate();
  const { isAuthenticated, accessToken, currentUser, hasRole, logout } = useAuth();

  // Possible states: 'loading' | 'noApplication' | 'pending' | 'rejected' | 'approved' | 'error'
  const [appState, setAppState] = useState('loading');
  const [application, setApplication] = useState(null);
  const [errorMessage, setErrorMessage] = useState('');

  // Form State
  const [organizerName, setOrganizerName] = useState('');
  const [organizerType, setOrganizerType] = useState('Individual'); // 'Individual' | 'Organization'
  const [contactNumber, setContactNumber] = useState('');
  const [description, setDescription] = useState('');
  const [website, setWebsite] = useState('');

  const [fieldErrors, setFieldErrors] = useState({});
  const [formError, setFormError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isEditingResubmit, setIsEditingResubmit] = useState(false);

  // -------------------------------------------------------------------------
  // Authentication & Role Redirection
  // -------------------------------------------------------------------------
  useEffect(() => {
    if (!isAuthenticated) {
      navigate('/login', {
        state: { returnTo: '/list-your-event' },
        replace: true,
      });
      return;
    }

    // Existing Organizer -> Send to Organizer Dashboard
    if (hasRole('Organizer')) {
      navigate('/organizer', { replace: true });
      return;
    }

    // Administrator without Customer role -> Send to Admin Dashboard
    if (hasRole('Administrator') && !hasRole('Customer')) {
      navigate('/admin', { replace: true });
      return;
    }
  }, [isAuthenticated, hasRole, navigate]);

  // -------------------------------------------------------------------------
  // Fetch Existing Application
  // -------------------------------------------------------------------------
  const loadApplication = useCallback(async () => {
    if (!isAuthenticated || !accessToken) return;
    if (hasRole('Organizer')) return;

    setAppState('loading');
    setErrorMessage('');

    try {
      const data = await getMyOrganizerApplication(accessToken);

      if (!data) {
        // 404: User has no application on file
        setApplication(null);
        setAppState('noApplication');
        return;
      }

      setApplication(data);

      const status = (data.status || '').toLowerCase();
      if (status === 'pending') {
        setAppState('pending');
      } else if (status === 'rejected') {
        setAppState('rejected');
      } else if (status === 'approved') {
        setAppState('approved');
      } else {
        // Fallback for unexpected status
        setAppState('pending');
      }
    } catch (err) {
      if (err.status === 401) {
        logout();
        navigate('/login', {
          state: { returnTo: '/list-your-event' },
          replace: true,
        });
        return;
      }
      setAppState('error');
      setErrorMessage(
        err.message || 'We could not verify your application status. Please check your connection and try again.'
      );
    }
  }, [isAuthenticated, accessToken, hasRole, logout, navigate]);

  useEffect(() => {
    loadApplication();
  }, [loadApplication]);

  // -------------------------------------------------------------------------
  // Form Validation & Submission
  // -------------------------------------------------------------------------
  function validateForm() {
    const errors = {};

    const trimmedName = organizerName.trim();
    if (!trimmedName) {
      errors.organizerName = 'Organizer or business name is required.';
    } else if (trimmedName.length > 200) {
      errors.organizerName = 'Organizer name cannot exceed 200 characters.';
    }

    if (!organizerType || !['Individual', 'Organization'].includes(organizerType)) {
      errors.organizerType = 'Please select a valid organizer type.';
    }

    const trimmedContact = contactNumber.trim();
    if (!trimmedContact) {
      errors.contactNumber = 'Contact number is required.';
    } else if (trimmedContact.length < 7 || trimmedContact.length > 50) {
      errors.contactNumber = 'Contact number must be between 7 and 50 characters.';
    }

    const trimmedDesc = description.trim();
    if (!trimmedDesc) {
      errors.description = 'Please provide a short description about yourself or your organization.';
    } else if (trimmedDesc.length > 2000) {
      errors.description = 'Description cannot exceed 2000 characters.';
    }

    const trimmedWeb = website.trim();
    if (trimmedWeb && trimmedWeb.length > 500) {
      errors.website = 'Website URL cannot exceed 500 characters.';
    }

    return errors;
  }

  async function handleSubmit(e) {
    e.preventDefault();
    setFormError('');

    const errors = validateForm();
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }

    setIsSubmitting(true);

    try {
      const payload = {
        organizerName: organizerName.trim(),
        organizerType,
        contactNumber: contactNumber.trim(),
        description: description.trim(),
        website: website.trim() ? website.trim() : null,
      };

      const result = isEditingResubmit
        ? await resubmitOrganizerApplication(payload, accessToken)
        : await submitOrganizerApplication(payload, accessToken);

      // On 200/201: Immediately transition to pending review card
      setApplication(result);
      setAppState('pending');
      setIsEditingResubmit(false);
    } catch (err) {
      if (err.status === 400 && err.errors?.length > 0) {
        setFormError(err.errors.join(' '));
      } else if (err.status === 409) {
        setFormError(
          err.message || 'An application already exists or conflict occurred.'
        );
        // Refresh from backend to sync state
        loadApplication();
      } else if (err.status === 401) {
        logout();
        navigate('/login', {
          state: { returnTo: '/list-your-event' },
          replace: true,
        });
      } else {
        setFormError(
          err.message || 'Failed to submit application. Please check your connection and try again.'
        );
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  const handleSignOutAgain = () => {
    logout();
    navigate('/login', {
      state: { returnTo: '/organizer' },
      replace: true,
    });
  };

  // Helper date formatter
  function formatDate(isoString) {
    if (!isoString) return '—';
    try {
      const date = new Date(isoString);
      return date.toLocaleDateString('en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      });
    } catch {
      return isoString;
    }
  }

  // -------------------------------------------------------------------------
  // Render: Loading State
  // -------------------------------------------------------------------------
  if (!isAuthenticated || hasRole('Organizer') || appState === 'loading') {
    return (
      <div style={{ minHeight: '100vh', backgroundColor: 'var(--ep-canvas)' }}>
        <Header />
        <main className="container" style={{ padding: '60px 16px', maxWidth: '640px' }}>
          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              padding: '48px 32px',
              textAlign: 'center',
              boxShadow: 'var(--ep-shadow-card)',
            }}
          >
            <div
              style={{
                width: '40px',
                height: '40px',
                border: '3px solid var(--ep-border)',
                borderTopColor: 'var(--ep-primary)',
                borderRadius: '50%',
                animation: 'ep-spin 0.8s linear infinite',
                margin: '0 auto 20px',
              }}
            />
            <h2 className="ep-h3" style={{ marginBottom: '8px' }}>
              Checking application status
            </h2>
            <p className="ep-body" style={{ margin: 0, color: 'var(--ep-text-secondary)' }}>
              Please wait while we verify your EventPulse account details…
            </p>
          </div>
          <style>{`
            @keyframes ep-spin {
              0% { transform: rotate(0deg); }
              100% { transform: rotate(360deg); }
            }
          `}</style>
        </main>
      </div>
    );
  }

  // -------------------------------------------------------------------------
  // Render: Error State (API or network error, distinct from noApplication)
  // -------------------------------------------------------------------------
  if (appState === 'error') {
    return (
      <div style={{ minHeight: '100vh', backgroundColor: 'var(--ep-canvas)' }}>
        <Header />
        <main className="container" style={{ padding: '60px 16px', maxWidth: '640px' }}>
          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              padding: '40px 32px',
              textAlign: 'center',
              boxShadow: 'var(--ep-shadow-card)',
            }}
          >
            <div
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                justifyContent: 'center',
                width: '56px',
                height: '56px',
                borderRadius: '50%',
                backgroundColor: '#FFF0EF',
                color: 'var(--ep-danger)',
                marginBottom: '20px',
              }}
            >
              <AlertCircle size={28} />
            </div>
            <h2 className="ep-h2" style={{ marginBottom: '10px' }}>
              Unable to Load Application
            </h2>
            <p className="ep-body" style={{ marginBottom: '24px', color: 'var(--ep-text-secondary)' }}>
              {errorMessage || 'A communication issue occurred while connecting to EventPulse services.'}
            </p>
            <div style={{ display: 'flex', gap: '12px', justifyContent: 'center' }}>
              <button
                type="button"
                onClick={loadApplication}
                className="ep-btn-primary"
                style={{ display: 'inline-flex', alignItems: 'center', gap: '8px' }}
              >
                <RotateCcw size={15} />
                <span>Try Again</span>
              </button>
              <Link to="/" className="ep-btn-secondary" style={{ textDecoration: 'none' }}>
                Return to Home
              </Link>
            </div>
          </div>
        </main>
      </div>
    );
  }

  // -------------------------------------------------------------------------
  // Render: Pending Review State
  // -------------------------------------------------------------------------
  if (appState === 'pending' && application) {
    return (
      <div style={{ minHeight: '100vh', backgroundColor: 'var(--ep-canvas)' }}>
        <Header />
        <main className="container" style={{ padding: '48px 16px 80px', maxWidth: '640px' }}>
          {/* Breadcrumb / Back */}
          <div style={{ marginBottom: '20px' }}>
            <Link
              to="/"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                fontSize: '13px',
                color: 'var(--ep-text-secondary)',
                textDecoration: 'none',
              }}
            >
              <ArrowLeft size={14} />
              <span>Back to Home</span>
            </Link>
          </div>

          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              boxShadow: 'var(--ep-shadow-card)',
              padding: '36px 32px',
            }}
          >
            {/* Status Pill */}
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '18px' }}>
              <span
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '6px',
                  backgroundColor: 'var(--ep-soft-accent)',
                  color: 'var(--ep-primary)',
                  border: '1px solid #FFE0CC',
                  padding: '4px 10px',
                  borderRadius: 'var(--ep-radius-pill)',
                  fontSize: '11px',
                  fontWeight: 700,
                  letterSpacing: '0.05em',
                  textTransform: 'uppercase',
                }}
              >
                <Clock size={12} />
                <span>Pending Review</span>
              </span>
            </div>

            <h1 className="ep-h2" style={{ marginBottom: '8px' }}>
              Application Under Review
            </h1>
            <p className="ep-body" style={{ color: 'var(--ep-text-secondary)', marginBottom: '28px' }}>
              Your Organizer application has been received and is currently awaiting administrator review.
              Organizer capabilities will be activated once your application is approved.
            </p>

            {/* Submitted Application Details Box */}
            <div
              style={{
                backgroundColor: 'var(--ep-canvas)',
                borderRadius: '12px',
                border: '1px solid var(--ep-border)',
                padding: '20px',
                marginBottom: '28px',
              }}
            >
              <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--ep-text-secondary)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '14px' }}>
                Application Summary
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginBottom: '16px' }}>
                <div>
                  <div style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', marginBottom: '2px' }}>Organizer Name</div>
                  <div style={{ fontSize: '14px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>{application.organizerName}</div>
                </div>
                <div>
                  <div style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', marginBottom: '2px' }}>Organizer Type</div>
                  <div style={{ fontSize: '14px', fontWeight: 500, color: 'var(--ep-text-primary)' }}>{application.organizerType}</div>
                </div>
                <div>
                  <div style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', marginBottom: '2px' }}>Contact Number</div>
                  <div style={{ fontSize: '14px', fontWeight: 500, color: 'var(--ep-text-primary)' }}>{application.contactNumber}</div>
                </div>
                <div>
                  <div style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', marginBottom: '2px' }}>Submitted Date</div>
                  <div style={{ fontSize: '14px', fontWeight: 500, color: 'var(--ep-text-primary)' }}>{formatDate(application.submittedAt)}</div>
                </div>
              </div>

              {application.website && (
                <div style={{ marginBottom: '16px' }}>
                  <div style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', marginBottom: '2px' }}>Website / Social Link</div>
                  <a
                    href={application.website.startsWith('http') ? application.website : `https://${application.website}`}
                    target="_blank"
                    rel="noopener noreferrer"
                    style={{ fontSize: '13px', color: 'var(--ep-primary)', textDecoration: 'none', display: 'inline-flex', alignItems: 'center', gap: '4px' }}
                  >
                    <span>{application.website}</span>
                    <ExternalLink size={12} />
                  </a>
                </div>
              )}

              <div>
                <div style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', marginBottom: '4px' }}>Description</div>
                <div style={{ fontSize: '13px', color: 'var(--ep-text-primary)', whiteSpace: 'pre-wrap', lineHeight: '1.5' }}>
                  {application.description}
                </div>
              </div>
            </div>

            {/* Read-only reminder */}
            <div
              style={{
                fontSize: '12px',
                color: 'var(--ep-text-secondary)',
                lineHeight: '1.5',
                marginBottom: '24px',
              }}
            >
              Notice: Only one active organizer application is permitted at a time.
              You cannot edit this submission while review is in progress.
            </div>

            <div style={{ display: 'flex', gap: '12px' }}>
              <Link to="/" className="ep-btn-secondary" style={{ textDecoration: 'none' }}>
                Return to Home
              </Link>
            </div>
          </div>
        </main>
      </div>
    );
  }

  // -------------------------------------------------------------------------
  // Render: Rejected State
  // -------------------------------------------------------------------------
  if (appState === 'rejected' && application && !isEditingResubmit) {
    return (
      <div style={{ minHeight: '100vh', backgroundColor: 'var(--ep-canvas)' }}>
        <Header />
        <main className="container" style={{ padding: '48px 16px 80px', maxWidth: '640px' }}>
          <div style={{ marginBottom: '20px' }}>
            <Link
              to="/"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                fontSize: '13px',
                color: 'var(--ep-text-secondary)',
                textDecoration: 'none',
              }}
            >
              <ArrowLeft size={14} />
              <span>Back to Home</span>
            </Link>
          </div>

          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              boxShadow: 'var(--ep-shadow-card)',
              padding: '36px 32px',
            }}
          >
            {/* Rejected Badge */}
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '18px' }}>
              <span
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '6px',
                  backgroundColor: '#FFF0EF',
                  color: 'var(--ep-danger)',
                  border: '1px solid #FFCDD2',
                  padding: '4px 10px',
                  borderRadius: 'var(--ep-radius-pill)',
                  fontSize: '11px',
                  fontWeight: 700,
                  letterSpacing: '0.05em',
                  textTransform: 'uppercase',
                }}
              >
                <XCircle size={12} />
                <span>Application Rejected</span>
              </span>
            </div>

            <h1 className="ep-h2" style={{ marginBottom: '8px' }}>
              Organizer Application Not Approved
            </h1>
            <p className="ep-body" style={{ color: 'var(--ep-text-secondary)', marginBottom: '24px' }}>
              Thank you for your interest in organizing events on EventPulse. After review, your application
              was not approved.
            </p>

            {/* Administrator Feedback Box */}
            <div
              style={{
                backgroundColor: '#FFF8F7',
                border: '1px solid #FFEBEA',
                borderRadius: '12px',
                padding: '18px 20px',
                marginBottom: '24px',
              }}
            >
              <div
                style={{
                  fontSize: '12px',
                  fontWeight: 600,
                  color: 'var(--ep-danger)',
                  textTransform: 'uppercase',
                  letterSpacing: '0.05em',
                  marginBottom: '6px',
                }}
              >
                Administrator Feedback
              </div>
              <p
                style={{
                  margin: 0,
                  fontSize: '13px',
                  color: 'var(--ep-text-primary)',
                  lineHeight: '1.5',
                  fontStyle: application.reviewComment ? 'normal' : 'italic',
                }}
              >
                {application.reviewComment || 'No additional feedback was provided.'}
              </p>
            </div>

            {/* Summary Information */}
            <div
              style={{
                backgroundColor: 'var(--ep-canvas)',
                borderRadius: '12px',
                border: '1px solid var(--ep-border)',
                padding: '16px 20px',
                marginBottom: '28px',
                fontSize: '13px',
              }}
            >
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <span style={{ color: 'var(--ep-text-secondary)' }}>Organizer: </span>
                  <span style={{ fontWeight: 500 }}>{application.organizerName}</span>
                </div>
                <div>
                  <span style={{ color: 'var(--ep-text-secondary)' }}>Submitted: </span>
                  <span>{formatDate(application.submittedAt)}</span>
                </div>
                {application.reviewedAt && (
                  <div style={{ gridColumn: 'span 2' }}>
                    <span style={{ color: 'var(--ep-text-secondary)' }}>Reviewed: </span>
                    <span>{formatDate(application.reviewedAt)}</span>
                  </div>
                )}
              </div>
            </div>

            <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap' }}>
              <button
                type="button"
                onClick={() => {
                  setOrganizerName(application.organizerName || '');
                  setOrganizerType(application.organizerType || 'Individual');
                  setContactNumber(application.contactNumber || '');
                  setDescription(application.description || '');
                  setWebsite(application.website || '');
                  setFieldErrors({});
                  setFormError('');
                  setIsEditingResubmit(true);
                }}
                className="ep-btn-primary"
                style={{ display: 'inline-flex', alignItems: 'center', gap: '8px' }}
              >
                <RotateCcw size={15} />
                <span>Edit & Resubmit Application</span>
              </button>
              <Link to="/" className="ep-btn-secondary" style={{ textDecoration: 'none' }}>
                Return to Home
              </Link>
            </div>
          </div>
        </main>
      </div>
    );
  }

  // -------------------------------------------------------------------------
  // Render: Approved State (Stale JWT — prompt sign out / sign in)
  // -------------------------------------------------------------------------
  if (appState === 'approved' && application) {
    return (
      <div style={{ minHeight: '100vh', backgroundColor: 'var(--ep-canvas)' }}>
        <Header />
        <main className="container" style={{ padding: '48px 16px 80px', maxWidth: '640px' }}>
          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              boxShadow: 'var(--ep-shadow-card)',
              padding: '36px 32px',
              textAlign: 'center',
            }}
          >
            <div
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                justifyContent: 'center',
                width: '56px',
                height: '56px',
                borderRadius: '50%',
                backgroundColor: '#E8F5E9',
                color: '#2E7D32',
                marginBottom: '20px',
              }}
            >
              <CheckCircle2 size={28} />
            </div>

            <h1 className="ep-h2" style={{ marginBottom: '8px' }}>
              Organizer Application Approved
            </h1>
            <p className="ep-body" style={{ color: 'var(--ep-text-secondary)', maxWidth: '480px', margin: '0 auto 24px' }}>
              Congratulations! Your application to become an EventPulse Organizer has been approved.
              To activate your new Organizer role and access the Organizer Dashboard, please sign out and sign in again.
            </p>

            <button
              type="button"
              onClick={handleSignOutAgain}
              className="ep-btn-primary"
              style={{ display: 'inline-flex', alignItems: 'center', gap: '8px', padding: '10px 24px' }}
            >
              <LogOut size={15} />
              <span>Sign Out & Re-authenticate</span>
            </button>
          </div>
        </main>
      </div>
    );
  }

  // -------------------------------------------------------------------------
  // Render: No Application State -> Application Form
  // -------------------------------------------------------------------------
  return (
    <div style={{ minHeight: '100vh', backgroundColor: 'var(--ep-canvas)' }}>
      <Header />
      <main className="container" style={{ padding: '40px 16px 80px', maxWidth: '680px' }}>
        {/* Breadcrumb / Back */}
        <div style={{ marginBottom: '20px' }}>
          <Link
            to="/"
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '6px',
              fontSize: '13px',
              color: 'var(--ep-text-secondary)',
              textDecoration: 'none',
            }}
          >
            <ArrowLeft size={14} />
            <span>Back to Home</span>
          </Link>
        </div>

        {/* Application Form Card */}
        <div
          style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            boxShadow: 'var(--ep-shadow-card)',
            padding: '36px 32px',
          }}
        >
          {/* Header Banner */}
          <div style={{ marginBottom: '28px' }}>
            <div
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                fontSize: '11px',
                fontWeight: 700,
                letterSpacing: '0.05em',
                textTransform: 'uppercase',
                color: 'var(--ep-primary)',
                marginBottom: '8px',
              }}
            >
              {isEditingResubmit ? <RotateCcw size={13} /> : <Sparkles size={13} />}
              <span>{isEditingResubmit ? 'Resubmit Application' : 'List Your Event'}</span>
            </div>
            <h1 className="ep-h2" style={{ marginBottom: '8px' }}>
              {isEditingResubmit ? 'Update Organizer Application' : 'Become an EventPulse Organizer'}
            </h1>
            <p className="ep-body" style={{ margin: 0, color: 'var(--ep-text-secondary)' }}>
              {isEditingResubmit
                ? 'Update your details below to address administrator feedback and resubmit your application for review.'
                : 'Tell us a little about yourself or your organization. An EventPulse administrator will review your application before Organizer features are activated.'}
            </p>
          </div>

          {/* Feedback from Administrator when editing a rejected application */}
          {isEditingResubmit && application?.reviewComment && (
            <div
              style={{
                backgroundColor: '#FFF8F7',
                border: '1px solid #FFEBEA',
                borderRadius: '12px',
                padding: '16px 20px',
                marginBottom: '24px',
              }}
            >
              <div
                style={{
                  fontSize: '11px',
                  fontWeight: 700,
                  color: 'var(--ep-danger)',
                  textTransform: 'uppercase',
                  letterSpacing: '0.05em',
                  marginBottom: '6px',
                }}
              >
                Administrator Feedback to Address
              </div>
              <p style={{ margin: 0, fontSize: '13px', color: 'var(--ep-text-primary)', lineHeight: '1.5' }}>
                {application.reviewComment}
              </p>
            </div>
          )}

          {/* Form-level Error Alert */}
          {formError && (
            <div
              style={{
                backgroundColor: '#FFF0EF',
                border: '1px solid #FFCDD2',
                borderRadius: '10px',
                padding: '12px 14px',
                marginBottom: '24px',
                fontSize: '13px',
                color: 'var(--ep-danger)',
                display: 'flex',
                alignItems: 'flex-start',
                gap: '10px',
              }}
            >
              <AlertCircle size={16} style={{ flexShrink: 0, marginTop: '2px' }} />
              <span>{formError}</span>
            </div>
          )}

          <form onSubmit={handleSubmit} noValidate>
            {/* Account Email (Read-Only Informational) */}
            <div
              style={{
                backgroundColor: 'var(--ep-canvas)',
                borderRadius: '10px',
                border: '1px solid var(--ep-border)',
                padding: '12px 14px',
                marginBottom: '20px',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
              }}
            >
              <div>
                <span style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', display: 'block', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
                  Authenticated Account
                </span>
                <span style={{ fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                  {currentUser?.email || 'Current user'}
                </span>
              </div>
              <span
                style={{
                  fontSize: '11px',
                  backgroundColor: '#E8F5E9',
                  color: '#2E7D32',
                  padding: '2px 8px',
                  borderRadius: 'var(--ep-radius-pill)',
                  fontWeight: 600,
                }}
              >
                Verified
              </span>
            </div>

            {/* Organizer Name */}
            <div style={{ marginBottom: '20px' }}>
              <label
                htmlFor="organizer-name"
                style={{
                  display: 'block',
                  fontSize: '13px',
                  fontWeight: 500,
                  color: 'var(--ep-text-primary)',
                  marginBottom: '6px',
                }}
              >
                Organizer / Business Name <span style={{ color: 'var(--ep-danger)' }}>*</span>
              </label>
              <input
                id="organizer-name"
                type="text"
                className="ep-input"
                style={fieldErrors.organizerName ? errorInputStyle : {}}
                value={organizerName}
                onChange={(e) => {
                  setOrganizerName(e.target.value);
                  if (fieldErrors.organizerName) {
                    setFieldErrors((prev) => {
                      const next = { ...prev };
                      delete next.organizerName;
                      return next;
                    });
                  }
                  setFormError('');
                }}
                maxLength={200}
                placeholder="e.g. Acme Productions or John Doe Events"
                disabled={isSubmitting}
              />
              {fieldErrors.organizerName && (
                <p style={{ margin: '5px 0 0', fontSize: '12px', color: 'var(--ep-danger)' }}>
                  {fieldErrors.organizerName}
                </p>
              )}
            </div>

            {/* Row: Organizer Type & Contact Number */}
            <div
              style={{
                display: 'grid',
                gridTemplateColumns: '1fr 1fr',
                gap: '16px',
                marginBottom: '20px',
              }}
            >
              {/* Organizer Type */}
              <div>
                <label
                  style={{
                    display: 'block',
                    fontSize: '13px',
                    fontWeight: 500,
                    color: 'var(--ep-text-primary)',
                    marginBottom: '6px',
                  }}
                >
                  Organizer Type <span style={{ color: 'var(--ep-danger)' }}>*</span>
                </label>
                <div style={{ display: 'flex', gap: '8px' }}>
                  <button
                    type="button"
                    onClick={() => setOrganizerType('Individual')}
                    disabled={isSubmitting}
                    style={{
                      flex: 1,
                      padding: '10px 12px',
                      borderRadius: '10px',
                      border: organizerType === 'Individual'
                        ? '2px solid var(--ep-primary)'
                        : '1px solid var(--ep-border)',
                      backgroundColor: organizerType === 'Individual'
                        ? 'var(--ep-soft-accent)'
                        : '#ffffff',
                      color: organizerType === 'Individual'
                        ? 'var(--ep-primary)'
                        : 'var(--ep-text-primary)',
                      fontSize: '13px',
                      fontWeight: 600,
                      cursor: 'pointer',
                      display: 'inline-flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      gap: '6px',
                      transition: 'var(--ep-transition)',
                    }}
                  >
                    <User size={14} />
                    <span>Individual</span>
                  </button>

                  <button
                    type="button"
                    onClick={() => setOrganizerType('Organization')}
                    disabled={isSubmitting}
                    style={{
                      flex: 1,
                      padding: '10px 12px',
                      borderRadius: '10px',
                      border: organizerType === 'Organization'
                        ? '2px solid var(--ep-primary)'
                        : '1px solid var(--ep-border)',
                      backgroundColor: organizerType === 'Organization'
                        ? 'var(--ep-soft-accent)'
                        : '#ffffff',
                      color: organizerType === 'Organization'
                        ? 'var(--ep-primary)'
                        : 'var(--ep-text-primary)',
                      fontSize: '13px',
                      fontWeight: 600,
                      cursor: 'pointer',
                      display: 'inline-flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      gap: '6px',
                      transition: 'var(--ep-transition)',
                    }}
                  >
                    <Building2 size={14} />
                    <span>Organization</span>
                  </button>
                </div>
                {fieldErrors.organizerType && (
                  <p style={{ margin: '5px 0 0', fontSize: '12px', color: 'var(--ep-danger)' }}>
                    {fieldErrors.organizerType}
                  </p>
                )}
              </div>

              {/* Contact Number */}
              <div>
                <label
                  htmlFor="contact-number"
                  style={{
                    display: 'block',
                    fontSize: '13px',
                    fontWeight: 500,
                    color: 'var(--ep-text-primary)',
                    marginBottom: '6px',
                  }}
                >
                  Contact Number <span style={{ color: 'var(--ep-danger)' }}>*</span>
                </label>
                <input
                  id="contact-number"
                  type="tel"
                  className="ep-input"
                  style={fieldErrors.contactNumber ? errorInputStyle : {}}
                  value={contactNumber}
                  onChange={(e) => {
                    setContactNumber(e.target.value);
                    if (fieldErrors.contactNumber) {
                      setFieldErrors((prev) => {
                        const next = { ...prev };
                        delete next.contactNumber;
                        return next;
                      });
                    }
                    setFormError('');
                  }}
                  maxLength={50}
                  placeholder="+94 77 123 4567"
                  disabled={isSubmitting}
                />
                {fieldErrors.contactNumber && (
                  <p style={{ margin: '5px 0 0', fontSize: '12px', color: 'var(--ep-danger)' }}>
                    {fieldErrors.contactNumber}
                  </p>
                )}
              </div>
            </div>

            {/* Description */}
            <div style={{ marginBottom: '20px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '6px' }}>
                <label
                  htmlFor="organizer-description"
                  style={{
                    fontSize: '13px',
                    fontWeight: 500,
                    color: 'var(--ep-text-primary)',
                  }}
                >
                  Description <span style={{ color: 'var(--ep-danger)' }}>*</span>
                </label>
                <span style={{ fontSize: '11px', color: 'var(--ep-text-secondary)' }}>
                  {description.length} / 2000
                </span>
              </div>
              <textarea
                id="organizer-description"
                className="ep-input"
                style={{
                  minHeight: '110px',
                  resize: 'vertical',
                  lineHeight: '1.5',
                  ...(fieldErrors.description ? errorInputStyle : {}),
                }}
                value={description}
                onChange={(e) => {
                  setDescription(e.target.value);
                  if (fieldErrors.description) {
                    setFieldErrors((prev) => {
                      const next = { ...prev };
                      delete next.description;
                      return next;
                    });
                  }
                  setFormError('');
                }}
                maxLength={2000}
                placeholder="Tell us what type of events you organize and a little about your organization or experience…"
                disabled={isSubmitting}
              />
              {fieldErrors.description && (
                <p style={{ margin: '5px 0 0', fontSize: '12px', color: 'var(--ep-danger)' }}>
                  {fieldErrors.description}
                </p>
              )}
            </div>

            {/* Website or Social Link */}
            <div style={{ marginBottom: '28px' }}>
              <label
                htmlFor="organizer-website"
                style={{
                  display: 'block',
                  fontSize: '13px',
                  fontWeight: 500,
                  color: 'var(--ep-text-primary)',
                  marginBottom: '6px',
                }}
              >
                Website or Social Media Link{' '}
                <span style={{ fontSize: '12px', fontWeight: 400, color: 'var(--ep-text-secondary)' }}>
                  (Optional)
                </span>
              </label>
              <input
                id="organizer-website"
                type="text"
                className="ep-input"
                style={fieldErrors.website ? errorInputStyle : {}}
                value={website}
                onChange={(e) => {
                  setWebsite(e.target.value);
                  if (fieldErrors.website) {
                    setFieldErrors((prev) => {
                      const next = { ...prev };
                      delete next.website;
                      return next;
                    });
                  }
                  setFormError('');
                }}
                maxLength={500}
                placeholder="https://yourwebsite.com or instagram.com/handle"
                disabled={isSubmitting}
              />
              {fieldErrors.website && (
                <p style={{ margin: '5px 0 0', fontSize: '12px', color: 'var(--ep-danger)' }}>
                  {fieldErrors.website}
                </p>
              )}
            </div>

            {/* Form Actions */}
            <div style={{ display: 'flex', gap: '12px', justifyContent: 'flex-end', alignItems: 'center' }}>
              {isEditingResubmit ? (
                <button
                  type="button"
                  onClick={() => {
                    setIsEditingResubmit(false);
                    setFormError('');
                    setFieldErrors({});
                  }}
                  className="ep-btn-secondary"
                  style={{ padding: '10px 18px', fontSize: '14px' }}
                  disabled={isSubmitting}
                >
                  Cancel
                </button>
              ) : (
                <Link
                  to="/"
                  className="ep-btn-secondary"
                  style={{ textDecoration: 'none', padding: '10px 18px', fontSize: '14px' }}
                >
                  Cancel
                </Link>
              )}
              <button
                type="submit"
                className="ep-btn-primary"
                style={{
                  padding: '10px 24px',
                  fontSize: '14px',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '8px',
                }}
                disabled={isSubmitting}
              >
                {isSubmitting ? (
                  <>
                    <div
                      style={{
                        width: '14px',
                        height: '14px',
                        border: '2px solid rgba(255,255,255,0.4)',
                        borderTopColor: '#ffffff',
                        borderRadius: '50%',
                        animation: 'ep-spin 0.8s linear infinite',
                      }}
                    />
                    <span>{isEditingResubmit ? 'Resubmitting…' : 'Submitting Application…'}</span>
                  </>
                ) : isEditingResubmit ? (
                  <>
                    <RotateCcw size={15} />
                    <span>Submit Updated Application</span>
                  </>
                ) : (
                  <>
                    <span>Submit Application</span>
                    <ChevronRight size={15} />
                  </>
                )}
              </button>
            </div>
          </form>
        </div>
      </main>
    </div>
  );
}

const errorInputStyle = {
  borderColor: 'var(--ep-danger)',
  boxShadow: '0 0 0 3px rgba(255,59,48,0.12)',
};
