/**
 * Helper utilities for Customer Payment History view (EP-358 / EP-54).
 */

/**
 * Formats a date string for payment history display.
 * Example output: "10 October 2026" or "10 October 2026 · 2:30 PM"
 * 
 * @param {string|Date} dateString 
 * @param {boolean} [includeTime=true]
 * @returns {string}
 */
export function formatPaymentDate(dateString, includeTime = false) {
  if (!dateString) return 'Date unavailable';
  try {
    const d = new Date(dateString);
    if (isNaN(d.getTime())) return String(dateString);

    const day = d.getDate();
    const month = d.toLocaleDateString('en-US', { month: 'long' });
    const year = d.getFullYear();

    if (!includeTime) {
      return `${day} ${month} ${year}`;
    }

    const time = d.toLocaleTimeString('en-US', {
      hour: 'numeric',
      minute: '2-digit',
      hour12: true,
    });

    return `${day} ${month} ${year} · ${time}`;
  } catch {
    return String(dateString);
  }
}

/**
 * Formats monetary amounts with currency prefix and proper decimal places.
 * Example output: "LKR 5,000.00"
 * 
 * @param {number|string} amount 
 * @param {string} [currency='LKR'] 
 * @returns {string}
 */
export function formatCurrency(amount, currency = 'LKR') {
  const num = Number(amount);
  const curr = (currency || 'LKR').toUpperCase();
  if (isNaN(num)) return `${curr} 0.00`;

  return `${curr} ${num.toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;
}

/**
 * Maps backend payment statuses to UI badge presentation properties.
 * 
 * @param {string} status - Succeeded, Pending, Failed, Refunded
 * @returns {{ label: string, bg: string, color: string, border: string }}
 */
export function getPaymentStatusBadgeConfig(status) {
  switch (status?.toLowerCase()) {
    case 'succeeded':
    case 'successful':
      return {
        label: 'Successful',
        bg: '#ECFDF5',
        color: '#065F46',
        border: '#A7F3D0',
      };
    case 'pending':
    case 'pendingpayment':
      return {
        label: 'Pending',
        bg: '#FFFBEB',
        color: '#92400E',
        border: '#FDE68A',
      };
    case 'failed':
      return {
        label: 'Failed',
        bg: '#FEF2F2',
        color: '#991B1B',
        border: '#FECACA',
      };
    case 'refunded':
      return {
        label: 'Refunded',
        bg: '#EFF6FF',
        color: '#1E40AF',
        border: '#BFDBFE',
      };
    default:
      return {
        label: status || 'Unknown',
        bg: '#F3F4F6',
        color: '#374151',
        border: '#E5E7EB',
      };
  }
}
