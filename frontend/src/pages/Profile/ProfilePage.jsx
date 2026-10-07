import React, { useState, useEffect, useCallback, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { Layout } from '../../components/layout/Layout';
import { useAuth } from '../../context/AuthContext';
import {
  getCurrentUserProfile,
  updateUserEmail,
  updateUserPhone,
  changeUserPassword,
  uploadUserAvatar,
  removeUserAvatar,
} from '../../services/userService';
import { getMyBookings } from '../../services/bookingService';
import { buildApiUrl } from '../../services/apiConfig';
import { checkPasswordRules } from '../../utils/resetPasswordValidation';
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
  Trash2,
  KeyRound,
  Ticket,
  ChevronRight,
  ShieldCheck,
  Check,
  X,
  Edit2,
  Eye,
  EyeOff,
  Upload,
  Clock,
  CheckCircle2,
  XCircle,
  ArrowRight,
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

function formatEventDate(dateString) {
  if (!dateString) return null;
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

function getBookingStatusBadgeConfig(status) {
  switch (status?.toLowerCase()) {
    case 'confirmed':
      return {
        label: 'Confirmed',
        bg: '#ECFDF5',
        color: '#065F46',
        border: '#A7F3D0',
        icon: CheckCircle2,
      };
    case 'pendingpayment':
      return {
        label: 'Pending Payment',
        bg: '#FFFBEB',
        color: '#92400E',
        border: '#FDE68A',
        icon: Clock,
      };
    case 'cancelled':
      return {
        label: 'Cancelled',
        bg: '#FEF2F2',
        color: '#991B1B',
        border: '#FECACA',
        icon: XCircle,
      };
    default:
      return {
        label: status || 'Unknown',
        bg: '#F3F4F6',
        color: '#374151',
        border: '#E5E7EB',
        icon: Clock,
      };
  }
}

export function ProfilePage() {
  const { accessToken, updateCurrentUser } = useAuth();
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

  // Profile Picture (Phase 3) State
  const fileInputRef = useRef(null);
  const [avatarUploading, setAvatarUploading] = useState(false);
  const [avatarRemoving, setAvatarRemoving] = useState(false);
  const [avatarError, setAvatarError] = useState(null);

  // Change Password Modal (Phase 4) State
  const [passwordModalOpen, setPasswordModalOpen] = useState(false);
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showCurrentPassword, setShowCurrentPassword] = useState(false);
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [passwordSubmitting, setPasswordSubmitting] = useState(false);
  const [passwordError, setPasswordError] = useState(null);

  // Recent Bookings (Phase 5/6) State
  const [bookingsLoading, setBookingsLoading] = useState(false);
  const [bookingsError, setBookingsError] = useState(null);
  const [recentBookings, setRecentBookings] = useState([]);

  const fetchProfile = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getCurrentUserProfile(accessToken);
      setProfile(data);
      if (typeof updateCurrentUser === 'function' && data?.profilePictureUrl !== undefined) {
        updateCurrentUser({ profilePictureUrl: data.profilePictureUrl });
      }
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

  const fetchRecentBookings = useCallback(async () => {
    setBookingsLoading(true);
    setBookingsError(null);
    try {
      // Fetch the customer's top 5 most recent bookings via existing Booking Service endpoint
      const res = await getMyBookings(1, 5, 'All', accessToken);
      setRecentBookings(res?.items || []);
    } catch (err) {
      console.error('Failed to load recent bookings:', err);
      const msg =
        err?.response?.data?.message ||
        err?.data?.message ||
        err?.message ||
        'Unable to load recent bookings.';
      setBookingsError(msg);
    } finally {
      setBookingsLoading(false);
    }
  }, [accessToken]);

  useEffect(() => {
    fetchProfile();
    fetchRecentBookings();
  }, [fetchProfile, fetchRecentBookings]);

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

  // Avatar Upload / Remove Handlers (Phase 3)
  const handleTriggerAvatarUpload = () => {
    setAvatarError(null);
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
      fileInputRef.current.click();
    }
  };

  const handleAvatarFileChange = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    // Validate client-side
    const validTypes = ['image/jpeg', 'image/jpg', 'image/png', 'image/webp'];
    if (!validTypes.includes(file.type.toLowerCase())) {
      setAvatarError('Only JPEG, PNG, or WebP image files are allowed.');
      return;
    }

    const maxBytes = 2 * 1024 * 1024;
    if (file.size > maxBytes) {
      setAvatarError('Image file size must not exceed 2MB.');
      return;
    }

    setAvatarUploading(true);
    setAvatarError(null);

    try {
      const res = await uploadUserAvatar(file, accessToken);
      setProfile((prev) => (prev ? { ...prev, profilePictureUrl: res.profilePictureUrl } : null));
      if (typeof updateCurrentUser === 'function') {
        updateCurrentUser({ profilePictureUrl: res.profilePictureUrl });
      }
      setToastMessage(res.message || 'Profile picture updated successfully.');
    } catch (err) {
      console.error('Failed to upload avatar:', err);
      const msg =
        err?.response?.data?.message ||
        err?.data?.message ||
        err?.message ||
        'Failed to upload profile picture. Please try again.';
      setAvatarError(msg);
    } finally {
      setAvatarUploading(false);
    }
  };

  const handleRemoveAvatar = async () => {
    if (avatarRemoving || !profile?.profilePictureUrl) return;

    setAvatarRemoving(true);
    setAvatarError(null);

    try {
      const res = await removeUserAvatar(accessToken);
      setProfile((prev) => (prev ? { ...prev, profilePictureUrl: null } : null));
      if (typeof updateCurrentUser === 'function') {
        updateCurrentUser({ profilePictureUrl: null });
      }
      setToastMessage(res.message || 'Profile picture removed.');
    } catch (err) {
      console.error('Failed to remove avatar:', err);
      const msg =
        err?.response?.data?.message ||
        err?.data?.message ||
        err?.message ||
        'Failed to remove profile picture.';
      setAvatarError(msg);
    } finally {
      setAvatarRemoving(false);
    }
  };

  // Change Password Handlers (Phase 4)
  const handleOpenPasswordModal = () => {
    setCurrentPassword('');
    setNewPassword('');
    setConfirmPassword('');
    setShowCurrentPassword(false);
    setShowNewPassword(false);
    setShowConfirmPassword(false);
    setPasswordError(null);
    setPasswordModalOpen(true);
  };

  const handleClosePasswordModal = () => {
    if (passwordSubmitting) return;
    setPasswordModalOpen(false);
    setPasswordError(null);
  };

  const handleSubmitPassword = async (e) => {
    e.preventDefault();

    if (profile?.hasPassword && !currentPassword) {
      setPasswordError('Please provide your current password.');
      return;
    }

    if (!newPassword) {
      setPasswordError('Please enter a new password.');
      return;
    }

    const rules = checkPasswordRules(newPassword);
    if (!rules.allMet) {
      setPasswordError('Your new password does not meet the complexity requirements.');
      return;
    }

    if (newPassword !== confirmPassword) {
      setPasswordError('New password and confirm password do not match.');
      return;
    }

    setPasswordSubmitting(true);
    setPasswordError(null);

    try {
      const res = await changeUserPassword(
        {
          currentPassword: profile?.hasPassword ? currentPassword : null,
          newPassword,
          confirmPassword,
        },
        accessToken
      );

      // Once successfully set, user now has password
      setProfile((prev) => (prev ? { ...prev, hasPassword: true } : null));
      setToastMessage(res.message || 'Password updated successfully.');
      setPasswordModalOpen(false);
    } catch (err) {
      console.error('Failed to change password:', err);
      const msg =
        err?.response?.data?.message ||
        err?.data?.message ||
        err?.message ||
        'Failed to update password. Please check your current password.';
      setPasswordError(msg);
    } finally {
      setPasswordSubmitting(false);
    }
  };

  const roleConfig = profile ? getRoleBadgeConfig(profile.role) : null;
  const passwordRules = checkPasswordRules(newPassword);

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

          {/* Avatar Error Banner */}
          {avatarError && (
            <div
              data-testid="profile-avatar-error"
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                padding: '12px 16px',
                marginBottom: '20px',
                backgroundColor: '#FEF2F2',
                border: '1px solid #FECACA',
                borderRadius: '8px',
                color: '#991B1B',
                fontSize: '14px',
                fontWeight: 500,
                boxShadow: '0 2px 4px rgba(0, 0, 0, 0.05)',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <AlertTriangle size={18} color="#DC2626" />
                <span>{avatarError}</span>
              </div>
              <button
                type="button"
                onClick={() => setAvatarError(null)}
                style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#991B1B', padding: '2px' }}
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
                {/* Avatar Display & Actions (Phase 3) */}
                <div style={{ position: 'relative' }}>
                  <input
                    ref={fileInputRef}
                    type="file"
                    accept="image/png,image/jpeg,image/jpg,image/webp"
                    data-testid="profile-avatar-file-input"
                    style={{ display: 'none' }}
                    onChange={handleAvatarFileChange}
                  />

                  {profile.profilePictureUrl ? (
                    <img
                      data-testid="profile-avatar-img"
                      src={
                        profile.profilePictureUrl.startsWith('http://') || profile.profilePictureUrl.startsWith('https://')
                          ? profile.profilePictureUrl
                          : buildApiUrl(profile.profilePictureUrl)
                      }
                      alt={`${profile.firstName} ${profile.lastName}`}
                      style={{
                        width: '96px',
                        height: '96px',
                        borderRadius: '50%',
                        objectFit: 'cover',
                        border: '3px solid #ffffff',
                        boxShadow: '0 4px 12px rgba(0, 0, 0, 0.08)',
                        display: 'block',
                      }}
                      onError={(e) => {
                        console.warn('Avatar image failed to load, falling back to initials:', e.target.src);
                        // Temporarily set display none on failure so fallback renders
                        e.target.style.display = 'none';
                        const fallbackElem = document.getElementById('profile-avatar-fallback');
                        if (fallbackElem) fallbackElem.style.display = 'flex';
                      }}
                    />
                  ) : null}

                  {/* Fallback Initials (Rendered when no picture exists or image load fails) */}
                  <div
                    id="profile-avatar-fallback"
                    data-testid="profile-avatar"
                    style={{
                      width: '96px',
                      height: '96px',
                      borderRadius: '50%',
                      backgroundColor: 'var(--ep-soft-accent)',
                      color: 'var(--ep-primary)',
                      display: profile.profilePictureUrl ? 'none' : 'flex',
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

                  {/* Avatar Upload Button */}
                  <button
                    type="button"
                    data-testid="upload-avatar-btn"
                    onClick={handleTriggerAvatarUpload}
                    disabled={avatarUploading || avatarRemoving}
                    title="Change profile picture"
                    aria-label="Change profile picture"
                    style={{
                      position: 'absolute',
                      bottom: 0,
                      right: 0,
                      width: '32px',
                      height: '32px',
                      borderRadius: '50%',
                      backgroundColor: 'var(--ep-primary)',
                      color: '#ffffff',
                      border: '2px solid #ffffff',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      cursor: avatarUploading || avatarRemoving ? 'not-allowed' : 'pointer',
                      boxShadow: '0 2px 6px rgba(0, 0, 0, 0.15)',
                      transition: 'var(--ep-transition)',
                    }}
                    onMouseEnter={(e) => {
                      if (!avatarUploading && !avatarRemoving) e.currentTarget.style.backgroundColor = 'var(--ep-primary-hover)';
                    }}
                    onMouseLeave={(e) => {
                      if (!avatarUploading && !avatarRemoving) e.currentTarget.style.backgroundColor = 'var(--ep-primary)';
                    }}
                  >
                    {avatarUploading ? (
                      <Loader2 size={15} style={{ animation: 'spin 1s linear infinite' }} />
                    ) : (
                      <Camera size={15} />
                    )}
                  </button>

                  {/* Avatar Remove Button (if picture exists) */}
                  {profile.profilePictureUrl && (
                    <button
                      type="button"
                      data-testid="remove-avatar-btn"
                      onClick={handleRemoveAvatar}
                      disabled={avatarUploading || avatarRemoving}
                      title="Remove profile picture"
                      aria-label="Remove profile picture"
                      style={{
                        position: 'absolute',
                        top: 0,
                        right: 0,
                        width: '28px',
                        height: '28px',
                        borderRadius: '50%',
                        backgroundColor: '#FEF2F2',
                        color: 'var(--ep-danger)',
                        border: '2px solid #ffffff',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        cursor: avatarUploading || avatarRemoving ? 'not-allowed' : 'pointer',
                        boxShadow: '0 2px 6px rgba(0, 0, 0, 0.15)',
                        transition: 'var(--ep-transition)',
                      }}
                    >
                      {avatarRemoving ? (
                        <Loader2 size={13} style={{ animation: 'spin 1s linear infinite' }} />
                      ) : (
                        <Trash2 size={13} />
                      )}
                    </button>
                  )}
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
                      onClick={handleOpenPasswordModal}
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '6px',
                        padding: '8px 16px',
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
                      <KeyRound size={14} />
                      <span>{profile.hasPassword ? 'Change Password' : 'Set Password'}</span>
                    </button>
                  </div>
                </div>
              </div>

              {/* Section 3: Ticket Purchases & History (EP-26 Phase 5 & 6) */}
              <div
                data-testid="profile-ticket-purchases-card"
                style={{
                  backgroundColor: '#ffffff',
                  borderRadius: 'var(--ep-radius-card)',
                  border: '1px solid var(--ep-border)',
                  padding: '24px 28px',
                  boxShadow: 'var(--ep-shadow-card)',
                }}
              >
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    flexWrap: 'wrap',
                    gap: '12px',
                    marginBottom: '20px',
                    paddingBottom: '14px',
                    borderBottom: '1px solid var(--ep-border)',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                    <div
                      style={{
                        width: '38px',
                        height: '38px',
                        borderRadius: '10px',
                        backgroundColor: 'var(--ep-soft-accent)',
                        color: 'var(--ep-primary)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                      }}
                    >
                      <Ticket size={20} />
                    </div>
                    <div>
                      <h3
                        style={{
                          margin: 0,
                          fontSize: '18px',
                          fontWeight: 700,
                          color: 'var(--ep-text-primary)',
                          fontFamily: 'var(--ep-font-heading)',
                        }}
                      >
                        Recent Ticket Purchases
                      </h3>
                      <p style={{ margin: '2px 0 0', fontSize: '13px', color: 'var(--ep-text-secondary)' }}>
                        Review your recent bookings and tickets (owned authoritatively by Booking Service).
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
                      padding: '8px 16px',
                      backgroundColor: '#ffffff',
                      color: 'var(--ep-primary)',
                      border: '1px solid var(--ep-border)',
                      borderRadius: 'var(--ep-radius-btn)',
                      fontSize: '13px',
                      fontWeight: 600,
                      cursor: 'pointer',
                      transition: 'var(--ep-transition)',
                    }}
                    onMouseEnter={(e) => (e.currentTarget.style.borderColor = 'var(--ep-primary)')}
                    onMouseLeave={(e) => (e.currentTarget.style.borderColor = 'var(--ep-border)')}
                  >
                    <span>View All Booked Tickets</span>
                    <ChevronRight size={14} />
                  </button>
                </div>

                {/* Sub-State: Loading Bookings */}
                {bookingsLoading && (
                  <div
                    data-testid="profile-bookings-loading"
                    style={{
                      padding: '32px 16px',
                      textAlign: 'center',
                      color: 'var(--ep-text-secondary)',
                      fontSize: '14px',
                    }}
                  >
                    <Loader2 size={22} style={{ animation: 'spin 1s linear infinite', color: 'var(--ep-primary)', margin: '0 auto 8px' }} />
                    <div>Loading ticket purchase history...</div>
                  </div>
                )}

                {/* Sub-State: Error Bookings */}
                {!bookingsLoading && bookingsError && (
                  <div
                    data-testid="profile-bookings-error"
                    style={{
                      padding: '16px',
                      backgroundColor: '#FEF2F2',
                      border: '1px solid #FECACA',
                      borderRadius: '8px',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      gap: '12px',
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#991B1B', fontSize: '13px' }}>
                      <AlertTriangle size={16} />
                      <span>{bookingsError}</span>
                    </div>
                    <button
                      type="button"
                      onClick={fetchRecentBookings}
                      style={{
                        padding: '6px 12px',
                        backgroundColor: '#ffffff',
                        border: '1px solid #FECACA',
                        borderRadius: '6px',
                        fontSize: '12px',
                        fontWeight: 600,
                        color: '#991B1B',
                        cursor: 'pointer',
                      }}
                    >
                      Retry
                    </button>
                  </div>
                )}

                {/* Sub-State: Empty Bookings */}
                {!bookingsLoading && !bookingsError && recentBookings.length === 0 && (
                  <div
                    data-testid="profile-bookings-empty"
                    style={{
                      padding: '36px 16px',
                      textAlign: 'center',
                      backgroundColor: 'var(--ep-canvas)',
                      borderRadius: '8px',
                    }}
                  >
                    <Ticket size={28} style={{ color: 'var(--ep-text-secondary)', margin: '0 auto 10px', opacity: 0.6 }} />
                    <div style={{ fontSize: '15px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '4px' }}>
                      You haven&apos;t purchased any tickets yet.
                    </div>
                    <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: '0 0 16px' }}>
                      Browse popular events and secure your admission passes today.
                    </p>
                    <button
                      type="button"
                      data-testid="explore-events-btn"
                      onClick={() => navigate('/')}
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '6px',
                        padding: '8px 18px',
                        backgroundColor: 'var(--ep-primary)',
                        color: '#ffffff',
                        border: 'none',
                        borderRadius: 'var(--ep-radius-btn)',
                        fontSize: '13px',
                        fontWeight: 600,
                        cursor: 'pointer',
                        boxShadow: '0 2px 6px rgba(255, 91, 0, 0.25)',
                      }}
                    >
                      <span>Explore Events</span>
                      <ArrowRight size={14} />
                    </button>
                  </div>
                )}

                {/* Sub-State: Recent Bookings List */}
                {!bookingsLoading && !bookingsError && recentBookings.length > 0 && (
                  <div data-testid="profile-recent-bookings-list" style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                    {recentBookings.map((booking) => {
                      const statusCfg = getBookingStatusBadgeConfig(booking.status);
                      const StatusIcon = statusCfg.icon;

                      return (
                        <div
                          key={booking.id}
                          data-testid={`profile-booking-item-${booking.bookingReference}`}
                          onClick={() => navigate(`/my-bookings/${booking.id}`)}
                          style={{
                            padding: '14px 16px',
                            backgroundColor: 'var(--ep-canvas)',
                            borderRadius: '8px',
                            border: '1px solid var(--ep-border)',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'space-between',
                            flexWrap: 'wrap',
                            gap: '12px',
                            cursor: 'pointer',
                            transition: 'var(--ep-transition)',
                          }}
                          onMouseEnter={(e) => (e.currentTarget.style.borderColor = 'var(--ep-primary)')}
                          onMouseLeave={(e) => (e.currentTarget.style.borderColor = 'var(--ep-border)')}
                        >
                          <div style={{ flex: 1, minWidth: '220px' }}>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '4px' }}>
                              <span style={{ fontSize: '15px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                                {booking.eventName || booking.eventTitle || 'Event Admission'}
                              </span>
                              <span
                                style={{
                                  fontSize: '11px',
                                  fontFamily: 'monospace',
                                  padding: '2px 6px',
                                  backgroundColor: '#ffffff',
                                  border: '1px solid var(--ep-border)',
                                  borderRadius: '4px',
                                  color: 'var(--ep-text-secondary)',
                                }}
                              >
                                {booking.bookingReference}
                              </span>
                            </div>

                            <div style={{ display: 'flex', alignItems: 'center', gap: '14px', flexWrap: 'wrap', fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                              {booking.eventDate && (
                                <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                                  <Calendar size={12} color="var(--ep-primary)" />
                                  <span>{formatEventDate(booking.eventDate)}</span>
                                </div>
                              )}
                              <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                                <Ticket size={12} />
                                <span>
                                  {booking.items && booking.items.length > 0
                                    ? booking.items.map((i) => `${i.ticketName} (${i.quantity})`).join(', ')
                                    : `${booking.totalTickets} tickets`}
                                </span>
                              </div>
                            </div>
                          </div>

                          <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
                            <div
                              style={{
                                display: 'inline-flex',
                                alignItems: 'center',
                                gap: '5px',
                                padding: '4px 10px',
                                borderRadius: 'var(--ep-radius-pill)',
                                fontSize: '12px',
                                fontWeight: 600,
                                backgroundColor: statusCfg.bg,
                                color: statusCfg.color,
                                border: `1px solid ${statusCfg.border}`,
                              }}
                            >
                              <StatusIcon size={13} />
                              <span>{statusCfg.label}</span>
                            </div>

                            <ChevronRight size={16} color="var(--ep-text-secondary)" />
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
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

        {/* ==================== CHANGE PASSWORD MODAL ==================== */}
        {passwordModalOpen && (
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
              data-testid="change-password-modal"
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card)',
                width: '100%',
                maxWidth: '480px',
                padding: '28px',
                boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1)',
                maxHeight: '90vh',
                overflowY: 'auto',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '18px' }}>
                <div>
                  <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                    {profile?.hasPassword ? 'Change Password' : 'Set Account Password'}
                  </h3>
                  <p style={{ margin: '4px 0 0', fontSize: '13px', color: 'var(--ep-text-secondary)' }}>
                    {profile?.hasPassword
                      ? 'Enter your current password and choose a new secure password.'
                      : 'Create a password for your account to enable standard email/password login.'}
                  </p>
                </div>
                <button
                  type="button"
                  onClick={handleClosePasswordModal}
                  style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'var(--ep-text-secondary)' }}
                >
                  <X size={18} />
                </button>
              </div>

              {passwordError && (
                <div
                  data-testid="change-password-error"
                  style={{
                    padding: '10px 14px',
                    backgroundColor: '#FEF2F2',
                    border: '1px solid #FECACA',
                    borderRadius: '6px',
                    color: '#991B1B',
                    fontSize: '13px',
                    marginBottom: '18px',
                  }}
                >
                  {passwordError}
                </div>
              )}

              <form onSubmit={handleSubmitPassword}>
                {/* Current Password Field (Only if user already has a password) */}
                {profile?.hasPassword && (
                  <div style={{ marginBottom: '18px' }}>
                    <label
                      htmlFor="current-password-input"
                      style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}
                    >
                      Current Password
                    </label>
                    <div style={{ position: 'relative' }}>
                      <input
                        id="current-password-input"
                        data-testid="current-password-input"
                        type={showCurrentPassword ? 'text' : 'password'}
                        value={currentPassword}
                        onChange={(e) => setCurrentPassword(e.target.value)}
                        required
                        style={{
                          width: '100%',
                          padding: '10px 40px 10px 12px',
                          borderRadius: '8px',
                          border: '1px solid var(--ep-border)',
                          fontSize: '14px',
                          boxSizing: 'border-box',
                        }}
                      />
                      <button
                        type="button"
                        onClick={() => setShowCurrentPassword((prev) => !prev)}
                        style={{
                          position: 'absolute',
                          right: '10px',
                          top: '50%',
                          transform: 'translateY(-50%)',
                          background: 'none',
                          border: 'none',
                          cursor: 'pointer',
                          color: 'var(--ep-text-secondary)',
                          padding: '4px',
                        }}
                      >
                        {showCurrentPassword ? <EyeOff size={16} /> : <Eye size={16} />}
                      </button>
                    </div>
                  </div>
                )}

                {/* New Password Field */}
                <div style={{ marginBottom: '18px' }}>
                  <label
                    htmlFor="new-password-input"
                    style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}
                  >
                    New Password
                  </label>
                  <div style={{ position: 'relative' }}>
                    <input
                      id="new-password-input"
                      data-testid="new-password-input"
                      type={showNewPassword ? 'text' : 'password'}
                      value={newPassword}
                      onChange={(e) => setNewPassword(e.target.value)}
                      required
                      style={{
                        width: '100%',
                        padding: '10px 40px 10px 12px',
                        borderRadius: '8px',
                        border: '1px solid var(--ep-border)',
                        fontSize: '14px',
                        boxSizing: 'border-box',
                      }}
                    />
                    <button
                      type="button"
                      onClick={() => setShowNewPassword((prev) => !prev)}
                      style={{
                        position: 'absolute',
                        right: '10px',
                        top: '50%',
                        transform: 'translateY(-50%)',
                        background: 'none',
                        border: 'none',
                        cursor: 'pointer',
                        color: 'var(--ep-text-secondary)',
                        padding: '4px',
                      }}
                    >
                      {showNewPassword ? <EyeOff size={16} /> : <Eye size={16} />}
                    </button>
                  </div>

                  {/* Password Requirements Checklist */}
                  <div
                    data-testid="password-rules-checklist"
                    style={{
                      marginTop: '10px',
                      padding: '10px 12px',
                      backgroundColor: 'var(--ep-canvas)',
                      borderRadius: '6px',
                      fontSize: '12px',
                    }}
                  >
                    <div style={{ fontWeight: 600, marginBottom: '6px', color: 'var(--ep-text-secondary)' }}>
                      Password must meet:
                    </div>
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '4px' }}>
                      <div style={{ color: passwordRules.minLength ? '#059669' : 'var(--ep-text-secondary)', display: 'flex', alignItems: 'center', gap: '4px' }}>
                        <span>{passwordRules.minLength ? '✓' : '•'}</span> At least 8 characters
                      </div>
                      <div style={{ color: passwordRules.uppercase ? '#059669' : 'var(--ep-text-secondary)', display: 'flex', alignItems: 'center', gap: '4px' }}>
                        <span>{passwordRules.uppercase ? '✓' : '•'}</span> One uppercase letter
                      </div>
                      <div style={{ color: passwordRules.lowercase ? '#059669' : 'var(--ep-text-secondary)', display: 'flex', alignItems: 'center', gap: '4px' }}>
                        <span>{passwordRules.lowercase ? '✓' : '•'}</span> One lowercase letter
                      </div>
                      <div style={{ color: passwordRules.number ? '#059669' : 'var(--ep-text-secondary)', display: 'flex', alignItems: 'center', gap: '4px' }}>
                        <span>{passwordRules.number ? '✓' : '•'}</span> One number
                      </div>
                    </div>
                  </div>
                </div>

                {/* Confirm Password Field */}
                <div style={{ marginBottom: '24px' }}>
                  <label
                    htmlFor="confirm-password-input"
                    style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}
                  >
                    Confirm New Password
                  </label>
                  <div style={{ position: 'relative' }}>
                    <input
                      id="confirm-password-input"
                      data-testid="confirm-password-input"
                      type={showConfirmPassword ? 'text' : 'password'}
                      value={confirmPassword}
                      onChange={(e) => setConfirmPassword(e.target.value)}
                      required
                      style={{
                        width: '100%',
                        padding: '10px 40px 10px 12px',
                        borderRadius: '8px',
                        border: '1px solid var(--ep-border)',
                        fontSize: '14px',
                        boxSizing: 'border-box',
                      }}
                    />
                    <button
                      type="button"
                      onClick={() => setShowConfirmPassword((prev) => !prev)}
                      style={{
                        position: 'absolute',
                        right: '10px',
                        top: '50%',
                        transform: 'translateY(-50%)',
                        background: 'none',
                        border: 'none',
                        cursor: 'pointer',
                        color: 'var(--ep-text-secondary)',
                        padding: '4px',
                      }}
                    >
                      {showConfirmPassword ? <EyeOff size={16} /> : <Eye size={16} />}
                    </button>
                  </div>
                  {confirmPassword && newPassword !== confirmPassword && (
                    <div style={{ fontSize: '12px', color: '#DC2626', marginTop: '4px' }}>
                      Passwords do not match.
                    </div>
                  )}
                </div>

                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                  <button
                    type="button"
                    onClick={handleClosePasswordModal}
                    disabled={passwordSubmitting}
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
                    data-testid="save-password-btn"
                    disabled={passwordSubmitting || !passwordRules.allMet || newPassword !== confirmPassword}
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
                      cursor: passwordSubmitting || !passwordRules.allMet || newPassword !== confirmPassword ? 'not-allowed' : 'pointer',
                      color: '#ffffff',
                      opacity: passwordSubmitting || !passwordRules.allMet || newPassword !== confirmPassword ? 0.6 : 1,
                    }}
                  >
                    {passwordSubmitting ? (
                      <>
                        <Loader2 size={14} style={{ animation: 'spin 1s linear infinite' }} />
                        <span>Updating...</span>
                      </>
                    ) : (
                      <span>Update Password</span>
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
