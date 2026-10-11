import React from 'react';
import { X, User, Shield, CheckCircle2, XCircle, Briefcase } from 'lucide-react';
import { formatAdminDate, getUserStatusBadgeConfig, getUserRoleBadgeConfig } from '../../utils/adminDashboardUtils';

export function UserDetailsModal({ user, onClose }) {
  if (!user) return null;

  const statusBadge = getUserStatusBadgeConfig(user.isActive);
  const roleBadge = getUserRoleBadgeConfig(user.role);

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="user-details-title"
      style={{
        position: 'fixed',
        inset: 0,
        backgroundColor: 'rgba(0, 0, 0, 0.5)',
        backdropFilter: 'blur(3px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '16px',
        zIndex: 1050,
      }}
      onClick={onClose}
    >
      <div
        style={{
          backgroundColor: '#ffffff',
          borderRadius: 'var(--ep-radius-card)',
          boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04)',
          width: '100%',
          maxWidth: '600px',
          maxHeight: '90vh',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
          border: '1px solid var(--ep-border)',
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
            backgroundColor: 'var(--ep-canvas)',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <div
              style={{
                width: '40px',
                height: '40px',
                borderRadius: '50%',
                backgroundColor: '#ffffff',
                border: '1px solid var(--ep-border)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: 'var(--ep-primary)',
              }}
            >
              <User size={20} />
            </div>
            <div>
              <h2
                id="user-details-title"
                style={{ fontSize: '18px', fontWeight: 700, margin: 0, color: 'var(--ep-text-primary)' }}
              >
                {user.fullName || `${user.firstName} ${user.lastName}`.trim() || 'User Profile'}
              </h2>
              <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                User ID: {user.id}
              </span>
            </div>
          </div>

          <button
            type="button"
            onClick={onClose}
            aria-label="Close user details"
            style={{
              background: 'none',
              border: 'none',
              color: 'var(--ep-text-secondary)',
              cursor: 'pointer',
              padding: '6px',
              borderRadius: 'var(--ep-radius-pill)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <X size={20} />
          </button>
        </div>

        {/* Modal Body */}
        <div style={{ padding: '24px', overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: '20px' }}>
          {/* Status & Role Row */}
          <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap' }}>
            <div
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                backgroundColor: statusBadge.bg,
                color: statusBadge.color,
                border: `1px solid ${statusBadge.border}`,
                borderRadius: 'var(--ep-radius-pill)',
                padding: '4px 12px',
                fontSize: '12px',
                fontWeight: 600,
              }}
            >
              {user.isActive ? <CheckCircle2 size={13} /> : <XCircle size={13} />}
              <span>Account {statusBadge.label}</span>
            </div>

            <div
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                backgroundColor: roleBadge.bg,
                color: roleBadge.color,
                border: `1px solid ${roleBadge.border}`,
                borderRadius: 'var(--ep-radius-pill)',
                padding: '4px 12px',
                fontSize: '12px',
                fontWeight: 600,
              }}
            >
              <Shield size={13} />
              <span>Role: {roleBadge.label}</span>
            </div>

            <div
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                backgroundColor: user.emailConfirmed ? '#E8F5E9' : '#FFF9C4',
                color: user.emailConfirmed ? '#2E7D32' : '#F57F17',
                border: `1px solid ${user.emailConfirmed ? '#C8E6C9' : '#FFF59D'}`,
                borderRadius: 'var(--ep-radius-pill)',
                padding: '4px 12px',
                fontSize: '12px',
                fontWeight: 600,
              }}
            >
              <span>Email {user.emailConfirmed ? 'Verified' : 'Unverified'}</span>
            </div>
          </div>

          {/* Details Grid */}
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
              gap: '16px',
              backgroundColor: 'var(--ep-canvas)',
              borderRadius: 'var(--ep-radius-card)',
              padding: '16px',
              border: '1px solid var(--ep-border)',
            }}
          >
            <div>
              <span style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', textTransform: 'uppercase', fontWeight: 600 }}>
                Email Address
              </span>
              <p style={{ margin: '4px 0 0 0', fontSize: '14px', fontWeight: 500, color: 'var(--ep-text-primary)' }}>
                {user.email || '—'}
              </p>
            </div>

            <div>
              <span style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', textTransform: 'uppercase', fontWeight: 600 }}>
                Phone Number
              </span>
              <p style={{ margin: '4px 0 0 0', fontSize: '14px', fontWeight: 500, color: 'var(--ep-text-primary)' }}>
                {user.phoneNumber ? `${user.countryCode || ''} ${user.phoneNumber}`.trim() : 'Not provided'}
              </p>
            </div>

            <div>
              <span style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', textTransform: 'uppercase', fontWeight: 600 }}>
                Registration Date
              </span>
              <p style={{ margin: '4px 0 0 0', fontSize: '14px', fontWeight: 500, color: 'var(--ep-text-primary)' }}>
                {formatAdminDate(user.createdAt)}
              </p>
            </div>

            <div>
              <span style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', textTransform: 'uppercase', fontWeight: 600 }}>
                Assigned Roles
              </span>
              <p style={{ margin: '4px 0 0 0', fontSize: '14px', fontWeight: 500, color: 'var(--ep-text-primary)' }}>
                {user.roles && user.roles.length > 0 ? user.roles.join(', ') : user.role || 'Customer'}
              </p>
            </div>

            <div>
              <span style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', textTransform: 'uppercase', fontWeight: 600 }}>
                Account Lockout Status
              </span>
              <p style={{ margin: '4px 0 0 0', fontSize: '14px', fontWeight: 500, color: 'var(--ep-text-primary)' }}>
                {user.lockoutEnd ? `Locked until ${formatAdminDate(user.lockoutEnd)}` : 'Not locked'}
              </p>
            </div>

            <div>
              <span style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', textTransform: 'uppercase', fontWeight: 600 }}>
                Failed Access Count
              </span>
              <p style={{ margin: '4px 0 0 0', fontSize: '14px', fontWeight: 500, color: 'var(--ep-text-primary)' }}>
                {user.accessFailedCount ?? 0}
              </p>
            </div>
          </div>

          {/* Organizer Application Details (if present) */}
          {user.organizerApplication && (
            <div
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card)',
                border: '1px solid var(--ep-border)',
                padding: '16px',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '10px' }}>
                <Briefcase size={16} color="var(--ep-primary)" />
                <h4 style={{ fontSize: '13px', fontWeight: 700, margin: 0, color: 'var(--ep-text-primary)' }}>
                  Organizer Application Record
                </h4>
              </div>
              <div style={{ fontSize: '13px', display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '10px' }}>
                <div>
                  <span style={{ color: 'var(--ep-text-secondary)' }}>Organization: </span>
                  <strong style={{ color: 'var(--ep-text-primary)' }}>{user.organizerApplication.organizerName || '—'}</strong>
                </div>
                <div>
                  <span style={{ color: 'var(--ep-text-secondary)' }}>Type: </span>
                  <strong style={{ color: 'var(--ep-text-primary)' }}>{user.organizerApplication.organizerType || '—'}</strong>
                </div>
                <div>
                  <span style={{ color: 'var(--ep-text-secondary)' }}>Application Status: </span>
                  <strong style={{ color: 'var(--ep-text-primary)' }}>{user.organizerApplication.status || '—'}</strong>
                </div>
                <div>
                  <span style={{ color: 'var(--ep-text-secondary)' }}>Submitted: </span>
                  <strong style={{ color: 'var(--ep-text-primary)' }}>{formatAdminDate(user.organizerApplication.submittedAt)}</strong>
                </div>
              </div>
              {user.organizerApplication.reviewComment && (
                <p style={{ margin: '8px 0 0 0', fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                  Review comment: <em>{user.organizerApplication.reviewComment}</em>
                </p>
              )}
            </div>
          )}
        </div>

        {/* Modal Footer */}
        <div
          style={{
            padding: '16px 24px',
            borderTop: '1px solid var(--ep-border)',
            display: 'flex',
            justifyContent: 'flex-end',
            backgroundColor: '#ffffff',
          }}
        >
          <button
            type="button"
            className="ep-btn-secondary"
            onClick={onClose}
            style={{ padding: '8px 18px', fontSize: '13px' }}
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
}
