/**
 * Helper utilities for Admin Dashboard & User Management (EP-372 / EP-57)
 */

/**
 * Formats ISO date string into human-readable date for administrative audit inspection.
 * @param {string|null} dateString
 * @param {boolean} [includeTime=true]
 * @returns {string}
 */
export function formatAdminDate(dateString, includeTime = true) {
  if (!dateString) return '—';
  try {
    const d = new Date(dateString);
    if (isNaN(d.getTime())) return '—';

    const options = {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      ...(includeTime ? { hour: '2-digit', minute: '2-digit' } : {}),
    };
    return d.toLocaleDateString('en-US', options);
  } catch {
    return '—';
  }
}

/**
 * Returns visual badge styling for user account status (Active / Inactive).
 * @param {boolean} isActive
 * @returns {{ label: string, bg: string, color: string, border: string }}
 */
export function getUserStatusBadgeConfig(isActive) {
  if (isActive) {
    return {
      label: 'Active',
      bg: '#E8F5E9',
      color: '#2E7D32',
      border: '#C8E6C9',
    };
  }
  return {
    label: 'Inactive',
    bg: '#FFF0EF',
    color: '#D32F2F',
    border: '#FFCDD2',
  };
}

/**
 * Returns visual badge styling for user role (Administrator, Organizer, Customer).
 * @param {string} role
 * @returns {{ label: string, bg: string, color: string, border: string }}
 */
export function getUserRoleBadgeConfig(role) {
  const r = (role || '').trim().toLowerCase();
  if (r === 'administrator' || r === 'admin') {
    return {
      label: 'Administrator',
      bg: '#EDE7F6',
      color: '#512DA8',
      border: '#D1C4E9',
    };
  }
  if (r === 'organizer') {
    return {
      label: 'Organizer',
      bg: '#E3F2FD',
      color: '#1565C0',
      border: '#BBDEFB',
    };
  }
  if (r === 'customer') {
    return {
      label: 'Customer',
      bg: '#F5F5F5',
      color: '#616161',
      border: '#E0E0E0',
    };
  }
  return {
    label: role ? role : 'Customer',
    bg: '#F5F5F5',
    color: '#616161',
    border: '#E0E0E0',
  };
}

/**
 * Evaluates whether an administrator is permitted to deactivate the target user.
 * Enforces frontend safety against self-deactivation.
 * 
 * @param {Object|null} targetUser - The account to deactivate
 * @param {string|null} currentAdminId - Caller administrator's user ID
 * @returns {{ allowed: boolean, reason?: string, message?: string }}
 */
export function canAdminDeactivateUser(targetUser, currentAdminId) {
  if (!targetUser) {
    return { allowed: false, reason: 'INVALID_TARGET', message: 'No user selected.' };
  }

  // Self-deactivation guard
  if (currentAdminId && String(targetUser.id).toLowerCase() === String(currentAdminId).toLowerCase()) {
    return {
      allowed: false,
      reason: 'SELF_DEACTIVATION_PROHIBITED',
      message: 'You cannot deactivate your own administrative account.',
    };
  }

  // Already inactive
  if (!targetUser.isActive) {
    return {
      allowed: false,
      reason: 'ALREADY_INACTIVE',
      message: 'This user account is already inactive.',
    };
  }

  return { allowed: true, message: null };
}

/**
 * Evaluates whether an administrator is permitted to change the role of the target user.
 * Enforces frontend safety against self-demotion.
 * 
 * @param {Object|null} targetUser - The account whose role is changing
 * @param {string} newRole - Desired new role
 * @param {string|null} currentAdminId - Caller administrator's user ID
 * @returns {{ allowed: boolean, reason?: string, message?: string }}
 */
export function canAdminDemoteUser(targetUser, newRole, currentAdminId) {
  if (!targetUser) {
    return { allowed: true, message: null };
  }

  const currentRole = targetUser.role || (targetUser.roles && targetUser.roles[0]) || 'Customer';

  // Self-demotion guard
  if (currentAdminId && String(targetUser.id).toLowerCase() === String(currentAdminId).toLowerCase()) {
    if (currentRole.toLowerCase() === 'administrator' && (newRole || '').toLowerCase() !== 'administrator') {
      return {
        allowed: false,
        reason: 'SELF_DEMOTION_PROHIBITED',
        message: 'You cannot remove your own Administrator role.',
      };
    }
  }

  return { allowed: true, message: null };
}

/**
 * Formats platform overview dependency metrics safely without fabricating numbers.
 * @param {Object|number|null} metric - Metric value or DTO ({ totalCount, status, message })
 * @returns {{ value: string, display: string, isAvailable: boolean, status: string, message?: string, subtext?: string, badgeColor: string }}
 */
export function formatPlatformMetric(metric) {
  if (typeof metric === 'number') {
    const formatted = metric.toLocaleString();
    return {
      value: formatted,
      display: formatted,
      isAvailable: true,
      status: 'Available',
      message: 'Available',
      subtext: null,
      badgeColor: '#2E7D32',
    };
  }

  if (metric && typeof metric === 'object') {
    const isAvailable = metric.status === 'Available' && metric.totalCount !== null && metric.totalCount !== undefined;
    const formattedCount = isAvailable ? Number(metric.totalCount).toLocaleString() : 'N/A';
    return {
      value: isAvailable ? formattedCount : '—',
      display: formattedCount,
      isAvailable,
      status: metric.status || (isAvailable ? 'Available' : 'Unavailable'),
      message: metric.message || (isAvailable ? 'Available' : 'Metric Unavailable'),
      subtext: metric.message || (isAvailable ? null : 'Service metrics temporarily unavailable'),
      badgeColor: isAvailable ? '#2E7D32' : '#E65100',
    };
  }

  return {
    value: '—',
    display: 'N/A',
    isAvailable: false,
    status: 'Unavailable',
    message: 'Metric not available',
    subtext: 'Data not available',
    badgeColor: '#9E9E9E',
  };
}

/**
 * Translates backend error codes into human-readable messages.
 * @param {Error|Object|string} error
 * @returns {string}
 */
export function getAdminErrorMessage(error) {
  if (!error) return 'An unexpected error occurred while processing the admin request.';

  if (typeof error === 'string') {
    const upper = error.toUpperCase();
    if (upper.includes('LAST_ADMIN_PROTECTION') || upper.includes('LAST_ADMINISTRATOR_PROTECTION')) {
      return 'Cannot perform this action: the platform must have at least one active Administrator.';
    }
    if (upper.includes('SELF_DEACTIVATION_PROHIBITED')) {
      return 'You cannot deactivate your own administrator account.';
    }
    if (upper.includes('SELF_DEMOTION_PROHIBITED')) {
      return 'You cannot demote or remove your own Administrator privileges.';
    }
    if (upper.includes('CANNOT_DEACTIVATE_ADMIN')) {
      return 'Active administrators cannot be deactivated directly. Change their role first.';
    }
    if (upper.includes('USER_NOT_FOUND')) {
      return 'The requested user account was not found in the directory.';
    }
    return error;
  }

  const code = error.code || error.data?.code || error.response?.data?.code || '';
  const message = error.message || error.data?.message || error.response?.data?.message || error.response?.data?.title || '';
  const combined = `${code} ${message}`.toUpperCase();

  if (combined.includes('LAST_ADMIN_PROTECTION') || combined.includes('LAST_ADMINISTRATOR_PROTECTION')) {
    return 'Cannot perform this action: the platform must have at least one active Administrator.';
  }
  if (combined.includes('SELF_DEACTIVATION_PROHIBITED')) {
    return 'You cannot deactivate your own administrator account.';
  }
  if (combined.includes('SELF_DEMOTION_PROHIBITED')) {
    return 'You cannot demote or remove your own Administrator privileges.';
  }
  if (combined.includes('CANNOT_DEACTIVATE_ADMIN')) {
    return 'Active administrators cannot be deactivated directly. Change their role first.';
  }
  if (combined.includes('USER_NOT_FOUND')) {
    return 'The requested user account was not found in the directory.';
  }
  if (combined.includes('INVALID_ROLE_SPECIFIED')) {
    return 'The specified role is invalid. Permitted roles: Customer, Organizer, Administrator.';
  }

  if (message && message !== 'Error' && message !== code) {
    return message;
  }

  return 'Operation failed. Please try again.';
}
