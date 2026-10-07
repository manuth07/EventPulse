import React, { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { Layout } from '../../components/layout/Layout';
import { useAuth } from '../../context/AuthContext';
import { getCurrentUserProfile, updateUserEmail, updateUserPhone } from '../../services/userService';
import {
  User,
  Mail,
  Phone,
  Shield,
  Calendar,
  Loader2,
  AlertTriangle,
  RotateCcw,
  Camera,
  KeyRound,
  Ticket,
  ChevronRight,
  ShieldCheck,
  Check,
  X,
  Edit2,
} from 'lucide-react';

function getRoleBadgeConfig(role) {
  switch (role?.toLowerCase()) {
    case 'administrator':
    case 'admin':
      return {
        label: 'Administrator',
        bg: '#FDF2F8',
        color: '#9D174D',
        border: '#FBCFE8',
      };
    case 'organizer':
      return {
        label: 'Organizer',
        bg: '#EFF6FF',
        color: '#1E40AF',
        border: '#BFDBFE',
      };
    case 'customer':
    default:
      return {
        label: 'Customer',
        bg: '#F3F4F6',
        color: '#374151',
        border: '#E5E7EB',
      };
  }
}

function formatDate(dateString) {
  if (!dateString) return 'Unavailable';
  try {
    const d = new Date(dateString);
    if (isNaN(d.getTime())) return String(dateString);
    const day = d.getDate();
    const month = d.toLocaleDateString('en-US', { month: 'short' });
    const year = d.getFullYear();
    return `${day} ${month} ${year}`;
  } catch {
    return String(dateString);
  }
}

export function ProfilePage() {
  const { accessToken } = useAuth();
  const navigate = useNavigate();

  const [loading, setLoading] = useState(true);
  const [profile, setProfile] = useState(null);
  const [error, setError] = useState(null);
  const [toastMessage, setToastMessage] = useState(null);

  // Edit Email Modal / Inline State
  const [emailModalOpen, setEmailModalOpen] = useState(false);
  const [newEmail, setNewEmail] = useState('');
  const [emailSubmitting, setEmailSubmitting] = useState(false);
  const [emailError, setEmailError] = useState(null);

  // Edit Phone Modal / Inline State
  const [phoneModalOpen, setPhoneModalOpen] = useState(false);
  const [newPhone, setNewPhone] = useState('');
  const [phoneSubmitting, setPhoneSubmitting] = useState(false);
  const [phoneError, setPhoneError] = useState(null);

  const fetchProfile = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getCurrentUserProfile(accessToken);
      setProfile(data);
    } catch (err) {
      console.error('Failed to load profile:', err);
      const msg =
        err?.response?.data?.message ||
        err?.data?.message ||
        err?.message ||
        'Unable to load your profile at this time. Please check your connection.';
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, [accessToken]);

  useEffect(() => {
    fetchProfile();
  }, [fetchProfile]);

  const handleOpenEmailModal = () => {
    setNewEmail(profile?.email || '');
    setEmailError(null);
    setEmailModalOpen(true);
  };

  const handleCloseEmailModal = () => {
    if (emailSubmitting) return;
    setEmailModalOpen(false);
    setEmailError(null);
  };

  const handleSubmitEmail = async (e) => {
    e.preventDefault();
    const trimmed = newEmail.trim();

    if (!trimmed) {
      setEmailError('Email address is required.');
      return;
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmed)) {
      setEmailError('Please enter a valid email address.');
      return;
    }

    setEmailSubmitting(true);
    setEmailError(null);

    try {
      const res = await updateUserEmail(trimmed, accessToken);
      setProfile((prev) => (prev ? { ...prev, email: res.email || trimmed } : null));
      setToastMessage(res.message || 'Email address updated successfully.');
      setEmailModalOpen(false);
    } catch (err) {
      console.error('Failed to update email:', err);
      const msg =
        err?.response?.data?.message ||
        err?.data?.message ||
        err?.message ||
        'Failed to update email address. Please try again.';
      setEmailError(msg);
    } finally {
      setEmailSubmitting(false);
    }
  };

  const handleOpenPhoneModal = () => {
    setNewPhone(profile?.phoneNumber || '');
    setPhoneError(null);
    setPhoneModalOpen(true);
  };

  const handleClosePhoneModal = () => {
    if (phoneSubmitting) return;
    setPhoneModalOpen(false);
    setPhoneError(null);
  };

  const handleSubmitPhone = async (e) => {
    e.preventDefault();
    const trimmed = newPhone.trim();

    if (!trimmed) {
      setPhoneError('Phone number is required.');
      return;
    }
    if (!/^\+?[0-9\s\-()]{7,20}$/.test(trimmed)) {
      setPhoneError('Please enter a valid phone number (7-20 digits).');
      return;
    }

    setPhoneSubmitting(true);
    setPhoneError(null);

    try {
      const res = await updateUserPhone(trimmed, accessToken);
      setProfile((prev) => (prev ? { ...prev, phoneNumber: res.phoneNumber || trimmed } : null));
      setToastMessage(res.message || 'Phone number updated successfully.');
      setPhoneModalOpen(false);
    } catch (err) {
      console.error('Failed to update phone number:', err);
      const msg =
        err?.response?.data?.message ||
        err?.data?.message ||
        err?.message ||
        'Failed to update phone number. Please try again.';
      setPhoneError(msg);
    } finally {
      setPhoneSubmitting(false);
    }
  };

  const roleConfig = profile ? getRoleBadgeConfig(profile.role) : null;

  return (
    <Layout>
      <div
        data-testid="user-profile-page"
        style={{
          minHeight: '100vh',
          backgroundColor: 'var(--ep-canvas)',
          padding: '40px 16px 80px',
        }}
      >
        <div style={{ maxWidth: '820px', margin: '0 auto', width: '100%' }}>
          {/* Toast Notification */}
          {toastMessage && (
            <div
              data-testid="profile-toast"
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                padding: '12px 16px',
                marginBottom: '20px',
                backgroundColor: '#ECFDF5',
                border: '1px solid #A7F3D0',
                borderRadius: '8px',
                color: '#065F46',
                fontSize: '14px',
                fontWeight: 500,
                boxShadow: '0 2px 4px rgba(0, 0, 0, 0.05)',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <Check size={18} color="#059669" />
                <span>{toastMessage}</span>
              </div>
              <button
                type="button"
                onClick={() => setToastMessage(null)}
                style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#065F46', padding: '2px' }}
              >
                <X size={16} />
              </button>
            </div>
          )}

          {/* Page Title */}
          <div style={{ marginBottom: '28px' }}>
            <h1
              data-testid="profile-page-title"
              style={{
                fontSize: '28px',
                fontWeight: 800,
                color: 'var(--ep-text-primary)',
                letterSpacing: '-0.5px',
                fontFamily: 'var(--ep-font-heading)',
                margin: 0,
              }}
            >
              My Profile
            </h1>
            <p style={{ margin: '6px 0 0', fontSize: '14px', color: 'var(--ep-text-secondary)' }}>
              Manage your personal information, contact details, and account security.
            </p>
          </div>

          {/* ==================== STATE 1: LOADING ==================== */}
          {loading && (
            <div
              data-testid="profile-loading"
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card)',
                border: '1px solid var(--ep-border)',
                padding: '64px 24px',
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
                  backgroundColor: 'var(--ep-soft-accent)',
                  marginBottom: '16px',
                }}
              >
                <Loader2 size={28} style={{ color: 'var(--ep-primary)', animation: 'spin 1s linear infinite' }} />
              </div>
              <h2 style={{ fontSize: '18px', fontWeight: 700, color: 'var(--ep-text-primary)', margin: '0 0 8px' }}>
                Loading Profile…
              </h2>
              <p style={{ fontSize: '14px', color: 'var(--ep-text-secondary)', margin: 0 }}>
                Retrieving your account details from EventPulse Identity.
              </p>
            </div>
          )}

          {/* ==================== STATE 2: ERROR ==================== */}
          {!loading && error && (
            <div
              data-testid="profile-error"
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card)',
                border: '1px solid var(--ep-border)',
                padding: '48px 24px',
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
                  backgroundColor: '#FEF2F2',
                  color: 'var(--ep-danger)',
                  marginBottom: '16px',
                }}
              >
                <AlertTriangle size={28} />
              </div>
              <h2 style={{ fontSize: '20px', fontWeight: 800, color: 'var(--ep-text-primary)', marginBottom: '8px' }}>
                Unable to Load Profile
              </h2>
              <p style={{ fontSize: '14px', color: 'var(--ep-text-secondary)', marginBottom: '20px' }}>
                {error}
              </p>
              <button
                type="button"
                data-testid="profile-retry-btn"
                onClick={fetchProfile}
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '8px',
                  padding: '10px 20px',
                  backgroundColor: 'var(--ep-primary)',
                  color: '#ffffff',
                  border: 'none',
                  borderRadius: 'var(--ep-radius-btn)',
                  fontSize: '14px',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                <RotateCcw size={15} />
                <span>Retry</span>
              </button>
            </div>
          )}

          {/* ==================== STATE 3: AUTHORITATIVE PROFILE DATA ==================== */}
          {!loading && !error && profile && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '24px' }}>
              {/* Profile Card Header (Avatar + Name + Role Badge) */}
              <div
                style={{
                  backgroundColor: '#ffffff',
                  borderRadius: 'var(--ep-radius-card)',
                  border: '1px solid var(--ep-border)',
                  padding: '32px 28px',
                  boxShadow: 'var(--ep-shadow-card)',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '24px',
                  flexWrap: 'wrap',
                }}
              >
                {/* Avatar Display */}
                <div style={{ position: 'relative' }}>
                  <div
                    data-testid="profile-avatar"
                    style={{
                      width: '96px',
                      height: '96px',
                      borderRadius: '50%',
                      backgroundColor: 'var(--ep-soft-accent)',
                      color: 'var(--ep-primary)',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      fontSize: '36px',
                      fontWeight: 800,
                      fontFamily: 'var(--ep-font-heading)',
                      border: '3px solid #ffffff',
                      boxShadow: '0 4px 12px rgba(0, 0, 0, 0.08)',
                    }}
                  >
                    {profile.firstName
                      ? `${profile.firstName.charAt(0)}${profile.lastName ? profile.lastName.charAt(0) : ''}`
                      : 'U'}
                  </div>
                </div>

                {/* Identity Info & Authoritative Role Badge */}
                <div style={{ flex: 1, minWidth: '240px' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap', marginBottom: '6px' }}>
                    <h2
                      data-testid="profile-fullname"
                      style={{
                        margin: 0,
                        fontSize: '24px',
                        fontWeight: 800,
                        color: 'var(--ep-text-primary)',
                        fontFamily: 'var(--ep-font-heading)',
                      }}
                    >
                      {profile.firstName} {profile.lastName}
                    </h2>

                    {/* Authoritative Role Badge */}
                    <div
                      data-testid="profile-role-badge"
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '5px',
                        padding: '4px 12px',
                        borderRadius: 'var(--ep-radius-pill)',
                        fontSize: '12px',
                        fontWeight: 700,
                        backgroundColor: roleConfig?.bg || '#F3F4F6',
                        color: roleConfig?.color || '#374151',
                        border: `1px solid ${roleConfig?.border || '#E5E7EB'}`,
                      }}
                    >
                      <Shield size={13} />
                      <span data-testid="profile-role-name">{roleConfig?.label || profile.role}</span>
                    </div>
                  </div>

                  <p
                    data-testid="profile-email-sub"
                    style={{
                      margin: 0,
                      fontSize: '14px',
                      color: 'var(--ep-text-secondary)',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '6px',
                    }}
                  >
                    <Mail size={14} />
                    <span>{profile.email}</span>
                  </p>

                  <div style={{ display: 'flex', alignItems: 'center', gap: '6px', marginTop: '8px', fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                    <Calendar size={13} />
                    <span>Member since {formatDate(profile.createdAt)}</span>
                  </div>
                </div>
              </div>

              {/* Section 1: Account Information */}
              <div
                data-testid="account-info-card"
                style={{
                  backgroundColor: '#ffffff',
                  borderRadius: 'var(--ep-radius-card)',
                  border: '1px solid var(--ep-border)',
                  padding: '24px 28px',
                  boxShadow: 'var(--ep-shadow-card)',
                }}
              >
                <div style={{ marginBottom: '18px', borderBottom: '1px solid var(--ep-border)', paddingBottom: '12px' }}>
                  <h3
                    style={{
                      margin: 0,
                      fontSize: '18px',
                      fontWeight: 700,
                      color: 'var(--ep-text-primary)',
                      fontFamily: 'var(--ep-font-heading)',
                    }}
                  >
                    Account Information
                  </h3>
                </div>

                <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                  {/* Email Row */}
                  <div
                    data-testid="profile-email-row"
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      flexWrap: 'wrap',
                      gap: '12px',
                      padding: '12px 16px',
                      backgroundColor: 'var(--ep-canvas)',
                      borderRadius: 'var(--ep-radius-btn)',
                    }}
                  >
                    <div>
                      <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--ep-text-secondary)', textTransform: 'uppercase' }}>
                        Email Address
                      </div>
                      <div data-testid="profile-email-val" style={{ fontSize: '15px', fontWeight: 600, color: 'var(--ep-text-primary)', marginTop: '2px' }}>
                        {profile.email}
                      </div>
                    </div>
                    <div>
                      <button
                        type="button"
                        data-testid="edit-email-btn"
                        onClick={handleOpenEmailModal}
                        style={{
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '6px',
                          padding: '6px 14px',
                          borderRadius: 'var(--ep-radius-btn)',
                          fontSize: '13px',
                          fontWeight: 600,
                          backgroundColor: '#ffffff',
                          color: 'var(--ep-primary)',
                          border: '1px solid var(--ep-border)',
                          cursor: 'pointer',
                          transition: 'var(--ep-transition)',
                        }}
                        onMouseEnter={(e) => (e.currentTarget.style.borderColor = 'var(--ep-primary)')}
                        onMouseLeave={(e) => (e.currentTarget.style.borderColor = 'var(--ep-border)')}
                      >
                        <Edit2 size={13} />
                        <span>Edit</span>
                      </button>
                    </div>
                  </div>

                  {/* Phone Row */}
                  <div
                    data-testid="profile-phone-row"
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      flexWrap: 'wrap',
                      gap: '12px',
                      padding: '12px 16px',
                      backgroundColor: 'var(--ep-canvas)',
                      borderRadius: 'var(--ep-radius-btn)',
                    }}
                  >
                    <div>
                      <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--ep-text-secondary)', textTransform: 'uppercase' }}>
                        Phone Number
                      </div>
                      <div data-testid="profile-phone-val" style={{ fontSize: '15px', fontWeight: 600, color: 'var(--ep-text-primary)', marginTop: '2px' }}>
                        {profile.phoneNumber || 'Not provided'}
                      </div>
                    </div>
                    <div>
                      <button
                        type="button"
                        data-testid="edit-phone-btn"
                        onClick={handleOpenPhoneModal}
                        style={{
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '6px',
                          padding: '6px 14px',
                          borderRadius: 'var(--ep-radius-btn)',
                          fontSize: '13px',
                          fontWeight: 600,
                          backgroundColor: '#ffffff',
                          color: 'var(--ep-primary)',
                          border: '1px solid var(--ep-border)',
                          cursor: 'pointer',
                          transition: 'var(--ep-transition)',
                        }}
                        onMouseEnter={(e) => (e.currentTarget.style.borderColor = 'var(--ep-primary)')}
                        onMouseLeave={(e) => (e.currentTarget.style.borderColor = 'var(--ep-border)')}
                      >
                        <Edit2 size={13} />
                        <span>Edit</span>
                      </button>
                    </div>
                  </div>

                  {/* Role Row (Strictly Read-Only, Authoritative) */}
                  <div
                    data-testid="profile-role-row"
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      flexWrap: 'wrap',
                      gap: '12px',
                      padding: '12px 16px',
                      backgroundColor: 'var(--ep-canvas)',
                      borderRadius: 'var(--ep-radius-btn)',
                    }}
                  >
                    <div>
                      <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--ep-text-secondary)', textTransform: 'uppercase' }}>
                        Account Role
                      </div>
                      <div data-testid="profile-role-display-val" style={{ fontSize: '15px', fontWeight: 700, color: 'var(--ep-text-primary)', marginTop: '2px' }}>
                        {roleConfig?.label || profile.role}
                      </div>
                      <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', marginTop: '2px' }}>
                        Assigned authoritatively by EventPulse. Cannot be edited directly.
                      </div>
                    </div>
                    <div>
                      <span
                        data-testid="role-readonly-badge"
                        style={{
                          padding: '4px 10px',
                          borderRadius: 'var(--ep-radius-pill)',
                          fontSize: '12px',
                          fontWeight: 600,
                          backgroundColor: roleConfig?.bg || '#F3F4F6',
                          color: roleConfig?.color || '#374151',
                          border: `1px solid ${roleConfig?.border || '#E5E7EB'}`,
                        }}
                      >
                        Read-Only
                      </span>
                    </div>
                  </div>
                </div>
              </div>

              {/* Section 2: Security */}
              <div
                data-testid="security-card"
                style={{
                  backgroundColor: '#ffffff',
                  borderRadius: 'var(--ep-radius-card)',
                  border: '1px solid var(--ep-border)',
                  padding: '24px 28px',
                  boxShadow: 'var(--ep-shadow-card)',
                }}
              >
                <div style={{ marginBottom: '18px', borderBottom: '1px solid var(--ep-border)', paddingBottom: '12px' }}>
                  <h3
                    style={{
                      margin: 0,
                      fontSize: '18px',
                      fontWeight: 700,
                      color: 'var(--ep-text-primary)',
                      fontFamily: 'var(--ep-font-heading)',
                    }}
                  >
                    Security
                  </h3>
                </div>

                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    flexWrap: 'wrap',
                    gap: '12px',
                    padding: '12px 16px',
                    backgroundColor: 'var(--ep-canvas)',
                    borderRadius: 'var(--ep-radius-btn)',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                    <div
                      style={{
                        width: '36px',
                        height: '36px',
                        borderRadius: '8px',
                        backgroundColor: 'var(--ep-soft-accent)',
                        color: 'var(--ep-primary)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                      }}
                    >
                      <KeyRound size={18} />
                    </div>
                    <div>
                      <div style={{ fontSize: '14px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                        Password Authentication
                      </div>
                      <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', marginTop: '2px' }}>
                        {profile.hasPassword
                          ? 'Password protection is active.'
                          : 'Linked via Google OAuth (no EventPulse password set).'}
                      </div>
                    </div>
                  </div>

                  <div>
                    <button
                      type="button"
                      data-testid="change-password-btn"
                      disabled
                      title="Coming in Phase 4"
                      style={{
                        padding: '8px 16px',
                        borderRadius: 'var(--ep-radius-btn)',
                        fontSize: '13px',
                        fontWeight: 600,
                        backgroundColor: '#ffffff',
                        color: 'var(--ep-text-secondary)',
                        border: '1px solid var(--ep-border)',
                        cursor: 'not-allowed',
                        opacity: 0.8,
                      }}
                    >
                      {profile.hasPassword ? 'Change Password' : 'Set Password'}
                    </button>
                  </div>
                </div>
              </div>

              {/* Section 3: My Ticket Purchases Quick Access */}
              <div
                data-testid="profile-ticket-purchases-card"
                style={{
                  backgroundColor: '#ffffff',
                  borderRadius: 'var(--ep-radius-card)',
                  border: '1px solid var(--ep-border)',
                  padding: '24px 28px',
                  boxShadow: 'var(--ep-shadow-card)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  flexWrap: 'wrap',
                  gap: '16px',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
                  <div
                    style={{
                      width: '42px',
                      height: '42px',
                      borderRadius: '10px',
                      backgroundColor: 'var(--ep-soft-accent)',
                      color: 'var(--ep-primary)',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                    }}
                  >
                    <Ticket size={22} />
                  </div>
                  <div>
                    <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                      My Ticket Purchases
                    </h3>
                    <p style={{ margin: '2px 0 0', fontSize: '13px', color: 'var(--ep-text-secondary)' }}>
                      View all ticket bookings, payment status, and digital admission passes.
                    </p>
                  </div>
                </div>

                <button
                  type="button"
                  data-testid="view-all-tickets-btn"
                  onClick={() => navigate('/my-bookings')}
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '6px',
                    padding: '9px 18px',
                    backgroundColor: 'var(--ep-primary)',
                    color: '#ffffff',
                    border: 'none',
                    borderRadius: 'var(--ep-radius-btn)',
                    fontSize: '13px',
                    fontWeight: 600,
                    cursor: 'pointer',
                    boxShadow: '0 2px 6px rgba(255, 91, 0, 0.25)',
                    transition: 'var(--ep-transition)',
                  }}
                  onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = 'var(--ep-primary-hover)')}
                  onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = 'var(--ep-primary)')}
                >
                  <span>View Booked Tickets</span>
                  <ChevronRight size={15} />
                </button>
              </div>
            </div>
          )}
        </div>

        {/* ==================== EDIT EMAIL MODAL ==================== */}
        {emailModalOpen && (
          <div
            style={{
              position: 'fixed',
              top: 0,
              left: 0,
              right: 0,
              bottom: 0,
              backgroundColor: 'rgba(0, 0, 0, 0.5)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              zIndex: 9999,
              padding: '16px',
            }}
          >
            <div
              data-testid="edit-email-modal"
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card)',
                width: '100%',
                maxWidth: '460px',
                padding: '24px',
                boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1)',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '16px' }}>
                <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                  Edit Email Address
                </h3>
                <button
                  type="button"
                  onClick={handleCloseEmailModal}
                  style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'var(--ep-text-secondary)' }}
                >
                  <X size={18} />
                </button>
              </div>

              {emailError && (
                <div
                  data-testid="edit-email-error"
                  style={{
                    padding: '10px 12px',
                    backgroundColor: '#FEF2F2',
                    border: '1px solid #FECACA',
                    borderRadius: '6px',
                    color: '#991B1B',
                    fontSize: '13px',
                    marginBottom: '16px',
                  }}
                >
                  {emailError}
                </div>
              )}

              <form onSubmit={handleSubmitEmail}>
                <div style={{ marginBottom: '20px' }}>
                  <label
                    htmlFor="profile-new-email-input"
                    style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}
                  >
                    New Email Address
                  </label>
                  <input
                    id="profile-new-email-input"
                    data-testid="profile-new-email-input"
                    type="email"
                    value={newEmail}
                    onChange={(e) => setNewEmail(e.target.value)}
                    required
                    style={{
                      width: '100%',
                      padding: '10px 12px',
                      borderRadius: '8px',
                      border: '1px solid var(--ep-border)',
                      fontSize: '14px',
                      boxSizing: 'border-box',
                    }}
                  />
                </div>

                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                  <button
                    type="button"
                    onClick={handleCloseEmailModal}
                    disabled={emailSubmitting}
                    style={{
                      padding: '8px 16px',
                      backgroundColor: '#ffffff',
                      border: '1px solid var(--ep-border)',
                      borderRadius: 'var(--ep-radius-btn)',
                      fontSize: '13px',
                      fontWeight: 600,
                      cursor: 'pointer',
                      color: 'var(--ep-text-primary)',
                    }}
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    data-testid="save-email-btn"
                    disabled={emailSubmitting}
                    style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '6px',
                      padding: '8px 18px',
                      backgroundColor: 'var(--ep-primary)',
                      border: 'none',
                      borderRadius: 'var(--ep-radius-btn)',
                      fontSize: '13px',
                      fontWeight: 600,
                      cursor: emailSubmitting ? 'not-allowed' : 'pointer',
                      color: '#ffffff',
                      opacity: emailSubmitting ? 0.75 : 1,
                    }}
                  >
                    {emailSubmitting ? (
                      <>
                        <Loader2 size={14} style={{ animation: 'spin 1s linear infinite' }} />
                        <span>Saving...</span>
                      </>
                    ) : (
                      <span>Save Email</span>
                    )}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* ==================== EDIT PHONE MODAL ==================== */}
        {phoneModalOpen && (
          <div
            style={{
              position: 'fixed',
              top: 0,
              left: 0,
              right: 0,
              bottom: 0,
              backgroundColor: 'rgba(0, 0, 0, 0.5)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              zIndex: 9999,
              padding: '16px',
            }}
          >
            <div
              data-testid="edit-phone-modal"
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card)',
                width: '100%',
                maxWidth: '460px',
                padding: '24px',
                boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1)',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '16px' }}>
                <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                  Edit Phone Number
                </h3>
                <button
                  type="button"
                  onClick={handleClosePhoneModal}
                  style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'var(--ep-text-secondary)' }}
                >
                  <X size={18} />
                </button>
              </div>

              {phoneError && (
                <div
                  data-testid="edit-phone-error"
                  style={{
                    padding: '10px 12px',
                    backgroundColor: '#FEF2F2',
                    border: '1px solid #FECACA',
                    borderRadius: '6px',
                    color: '#991B1B',
                    fontSize: '13px',
                    marginBottom: '16px',
                  }}
                >
                  {phoneError}
                </div>
              )}

              <form onSubmit={handleSubmitPhone}>
                <div style={{ marginBottom: '20px' }}>
                  <label
                    htmlFor="profile-new-phone-input"
                    style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}
                  >
                    Phone Number
                  </label>
                  <input
                    id="profile-new-phone-input"
                    data-testid="profile-new-phone-input"
                    type="tel"
                    placeholder="e.g. +94771234567"
                    value={newPhone}
                    onChange={(e) => setNewPhone(e.target.value)}
                    required
                    style={{
                      width: '100%',
                      padding: '10px 12px',
                      borderRadius: '8px',
                      border: '1px solid var(--ep-border)',
                      fontSize: '14px',
                      boxSizing: 'border-box',
                    }}
                  />
                </div>

                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                  <button
                    type="button"
                    onClick={handleClosePhoneModal}
                    disabled={phoneSubmitting}
                    style={{
                      padding: '8px 16px',
                      backgroundColor: '#ffffff',
                      border: '1px solid var(--ep-border)',
                      borderRadius: 'var(--ep-radius-btn)',
                      fontSize: '13px',
                      fontWeight: 600,
                      cursor: 'pointer',
                      color: 'var(--ep-text-primary)',
                    }}
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    data-testid="save-phone-btn"
                    disabled={phoneSubmitting}
                    style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '6px',
                      padding: '8px 18px',
                      backgroundColor: 'var(--ep-primary)',
                      border: 'none',
                      borderRadius: 'var(--ep-radius-btn)',
                      fontSize: '13px',
                      fontWeight: 600,
                      cursor: phoneSubmitting ? 'not-allowed' : 'pointer',
                      color: '#ffffff',
                      opacity: phoneSubmitting ? 0.75 : 1,
                    }}
                  >
                    {phoneSubmitting ? (
                      <>
                        <Loader2 size={14} style={{ animation: 'spin 1s linear infinite' }} />
                        <span>Saving...</span>
                      </>
                    ) : (
                      <span>Save Phone</span>
                    )}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}
      </div>
    </Layout>
  );
}

export default ProfilePage;
