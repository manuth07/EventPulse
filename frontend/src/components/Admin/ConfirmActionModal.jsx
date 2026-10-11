import React, { useState } from 'react';
import { X, AlertTriangle, ShieldAlert, Lock, Unlock, UserCog } from 'lucide-react';
import { canAdminDeactivateUser, canAdminDemoteUser, getAdminErrorMessage } from '../../utils/adminDashboardUtils';

export function ConfirmActionModal({
  action, // { type: 'activate' | 'deactivate' | 'role', user }
  currentAdminId,
  onConfirm,
  onClose,
}) {
  const type = action?.type;
  const user = action?.user;

  const [selectedRole, setSelectedRole] = useState(
    type === 'role' ? (user?.role || 'Customer') : ''
  );
  const [reason, setReason] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState(null);

  if (!action || !action.user) return null;

  // Safety checks
  const deactCheck = type === 'deactivate'
    ? canAdminDeactivateUser(user, currentAdminId)
    : { allowed: true };

  const roleCheck = type === 'role'
    ? canAdminDemoteUser(user, selectedRole, currentAdminId)
    : { allowed: true };

  const isBlocked = (type === 'deactivate' && !deactCheck.allowed) ||
                    (type === 'role' && !roleCheck.allowed);
  const blockedMessage = (type === 'deactivate' && !deactCheck.allowed)
    ? deactCheck.message
    : (type === 'role' && !roleCheck.allowed)
    ? roleCheck.message
    : null;

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (isBlocked || isSubmitting) return;

    setIsSubmitting(true);
    setErrorMessage(null);

    try {
      if (type === 'activate') {
        await onConfirm({ type: 'activate', userId: user.id, reason: reason.trim() });
      } else if (type === 'deactivate') {
        await onConfirm({ type: 'deactivate', userId: user.id, reason: reason.trim() });
      } else if (type === 'role') {
        await onConfirm({ type: 'role', userId: user.id, role: selectedRole, reason: reason.trim() });
      }
      onClose();
    } catch (err) {
      setErrorMessage(getAdminErrorMessage(err));
    } finally {
      setIsSubmitting(false);
    }
  };

  const getModalConfig = () => {
    if (type === 'activate') {
      return {
        title: 'Activate Account',
        icon: <Unlock size={22} color="#2E7D32" />,
        submitText: 'Confirm Activation',
        submitClass: 'ep-btn-primary',
        prompt: `Are you sure you want to activate the account for ${user.fullName || user.email}?`,
        description: 'The user will be able to log in and access platform features immediately.',
      };
    }
    if (type === 'deactivate') {
      return {
        title: 'Deactivate Account',
        icon: <Lock size={22} color="var(--ep-danger)" />,
        submitText: 'Confirm Deactivation',
        submitClass: 'ep-btn-danger',
        prompt: `Are you sure you want to deactivate the account for ${user.fullName || user.email}?`,
        description: 'Active sessions will be immediately terminated. The user will be blocked from logging in until reactivated.',
      };
    }
    return {
      title: 'Change User Role',
      icon: <UserCog size={22} color="var(--ep-primary)" />,
      submitText: 'Save Role Change',
      submitClass: 'ep-btn-primary',
      prompt: `Update role permissions for ${user.fullName || user.email}`,
      description: 'Changes apply immediately. If removing administrator privileges, active admin sessions are revoked.',
    };
  };

  const config = getModalConfig();

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="confirm-action-title"
      style={{
        position: 'fixed',
        inset: 0,
        backgroundColor: 'rgba(0, 0, 0, 0.5)',
        backdropFilter: 'blur(3px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '16px',
        zIndex: 1060,
      }}
      onClick={!isSubmitting ? onClose : undefined}
    >
      <div
        style={{
          backgroundColor: '#ffffff',
          borderRadius: 'var(--ep-radius-card)',
          boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04)',
          width: '100%',
          maxWidth: '520px',
          overflow: 'hidden',
          border: '1px solid var(--ep-border)',
        }}
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div
          style={{
            padding: '18px 24px',
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
                width: '36px',
                height: '36px',
                borderRadius: '50%',
                backgroundColor: '#ffffff',
                border: '1px solid var(--ep-border)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              {config.icon}
            </div>
            <h3
              id="confirm-action-title"
              style={{ fontSize: '17px', fontWeight: 700, margin: 0, color: 'var(--ep-text-primary)' }}
            >
              {config.title}
            </h3>
          </div>

          <button
            type="button"
            onClick={onClose}
            disabled={isSubmitting}
            aria-label="Close dialog"
            style={{
              background: 'none',
              border: 'none',
              color: 'var(--ep-text-secondary)',
              cursor: isSubmitting ? 'not-allowed' : 'pointer',
              padding: '6px',
              borderRadius: 'var(--ep-radius-pill)',
            }}
          >
            <X size={20} />
          </button>
        </div>

        {/* Body Form */}
        <form onSubmit={handleSubmit} style={{ padding: '24px' }}>
          {/* Error Banner */}
          {errorMessage && (
            <div
              data-testid="action-error-banner"
              style={{
                display: 'flex',
                alignItems: 'flex-start',
                gap: '10px',
                backgroundColor: '#FFF0EF',
                color: 'var(--ep-danger)',
                border: '1px solid #FFCDD2',
                borderRadius: 'var(--ep-radius-card)',
                padding: '12px 14px',
                fontSize: '13px',
                marginBottom: '18px',
              }}
            >
              <AlertTriangle size={18} style={{ flexShrink: 0, marginTop: '1px' }} />
              <div>{errorMessage}</div>
            </div>
          )}

          {/* Blocked Safety Warning */}
          {isBlocked && (
            <div
              data-testid="action-safety-blocked-banner"
              style={{
                display: 'flex',
                alignItems: 'flex-start',
                gap: '10px',
                backgroundColor: '#FFF8E1',
                color: '#B78103',
                border: '1px solid #FFE082',
                borderRadius: 'var(--ep-radius-card)',
                padding: '12px 14px',
                fontSize: '13px',
                marginBottom: '18px',
                lineHeight: 1.4,
              }}
            >
              <ShieldAlert size={18} style={{ flexShrink: 0, marginTop: '2px' }} />
              <div>
                <strong>Safety Guard Active:</strong> {blockedMessage}
              </div>
            </div>
          )}

          <p style={{ margin: '0 0 8px 0', fontSize: '15px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>
            {config.prompt}
          </p>

          <p style={{ margin: '0 0 20px 0', fontSize: '13px', color: 'var(--ep-text-secondary)', lineHeight: 1.5 }}>
            {config.description}
          </p>

          {/* Role selection dropdown (for role action) */}
          {type === 'role' && (
            <div style={{ marginBottom: '18px' }}>
              <label
                htmlFor="target-role-select"
                style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '6px', color: 'var(--ep-text-primary)' }}
              >
                Assign New Role
              </label>
              <select
                id="target-role-select"
                data-testid="target-role-select"
                className="form-select"
                value={selectedRole}
                onChange={(e) => setSelectedRole(e.target.value)}
                disabled={isSubmitting}
                style={{
                  width: '100%',
                  padding: '10px 14px',
                  borderRadius: 'var(--ep-radius-btn)',
                  border: '1px solid var(--ep-border)',
                  fontSize: '14px',
                  backgroundColor: '#ffffff',
                }}
              >
                <option value="Customer">Customer</option>
                <option value="Organizer">Organizer</option>
                <option value="Administrator">Administrator</option>
              </select>
            </div>
          )}

          {/* Reason input */}
          <div style={{ marginBottom: '24px' }}>
            <label
              htmlFor="action-reason-input"
              style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '6px', color: 'var(--ep-text-primary)' }}
            >
              Reason / Audit Notes (Optional)
            </label>
            <input
              id="action-reason-input"
              data-testid="action-reason-input"
              type="text"
              placeholder="e.g. Account verification, Terms of Service compliance"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              disabled={isSubmitting || isBlocked}
              maxLength={250}
              style={{
                width: '100%',
                padding: '10px 14px',
                borderRadius: 'var(--ep-radius-btn)',
                border: '1px solid var(--ep-border)',
                fontSize: '14px',
                boxSizing: 'border-box',
              }}
            />
          </div>

          {/* Action buttons */}
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
            <button
              type="button"
              className="ep-btn-secondary"
              onClick={onClose}
              disabled={isSubmitting}
              style={{ padding: '10px 18px', fontSize: '14px' }}
            >
              Cancel
            </button>

            <button
              type="submit"
              data-testid="confirm-action-submit-btn"
              className={config.submitClass}
              disabled={isSubmitting || isBlocked}
              style={{
                padding: '10px 22px',
                fontSize: '14px',
                display: 'inline-flex',
                alignItems: 'center',
                gap: '8px',
                cursor: (isSubmitting || isBlocked) ? 'not-allowed' : 'pointer',
                opacity: (isSubmitting || isBlocked) ? 0.65 : 1,
              }}
            >
              {isSubmitting && (
                <span className="spinner-border spinner-border-sm" role="status" aria-hidden="true" />
              )}
              <span>{config.submitText}</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
