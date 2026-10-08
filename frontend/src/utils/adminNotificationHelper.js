/**
 * Formats a notification submission date for display in the administrator UI (EP-151 / EP-32).
 * @param {string|Date} dateValue
 * @returns {string}
 */
export function formatNotificationDate(dateValue) {
  if (!dateValue) return 'Recently';
  try {
    const d = new Date(dateValue);
    if (isNaN(d.getTime())) return 'Recently';
    return d.toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return 'Recently';
  }
}

/**
 * Returns badge configuration (colors and label) for an administrator notification review status.
 * @param {string} status
 * @returns {{ label: string, bg: string, color: string, border: string, isPending: boolean }}
 */
export function getNotificationBadgeConfig(status) {
  const norm = (status || '').toLowerCase();
  if (norm === 'pending') {
    return {
      label: 'Pending Review',
      bg: '#FFF0E6',
      color: '#FF5B00',
      border: '#FFE0CC',
      isPending: true,
    };
  }
  if (norm === 'approved' || norm === 'published') {
    return {
      label: norm === 'published' ? 'Published' : 'Approved',
      bg: '#E8F5E9',
      color: '#2E7D32',
      border: '#C8E6C9',
      isPending: false,
    };
  }
  if (norm === 'rejected') {
    return {
      label: 'Rejected',
      bg: '#FFF5F5',
      color: '#FF3B30',
      border: '#FED7D7',
      isPending: false,
    };
  }
  return {
    label: status || 'Unknown',
    bg: '#F5F5F7',
    color: '#86868B',
    border: '#E5E5EA',
    isPending: false,
  };
}
