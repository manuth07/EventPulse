/**
 * Formatting and derivation helpers for booked ticket history & detail view (EP-354).
 */

export function formatTicketHistoryDate(dateString) {
  if (!dateString) return 'Date unavailable';
  try {
    const d = new Date(dateString);
    if (isNaN(d.getTime())) return String(dateString);
    const day = d.getDate();
    const month = d.toLocaleDateString('en-US', { month: 'short' });
    const year = d.getFullYear();
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

export function formatCurrency(amount) {
  const num = Number(amount);
  if (isNaN(num)) return 'LKR 0.00';
  return `LKR ${num.toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;
}

export function formatTicketTypesSummary(items = [], totalTickets = 0) {
  if (Array.isArray(items) && items.length > 0) {
    const breakdown = items.map((i) => `${i.ticketName || 'Ticket'} (${i.quantity || 1})`).join(', ');
    return `${breakdown} · ${totalTickets} total`;
  }
  return `${totalTickets} ${totalTickets === 1 ? 'ticket' : 'tickets'}`;
}

export function getStatusBadgeConfig(status) {
  switch (status?.toLowerCase()) {
    case 'confirmed':
      return {
        label: 'Confirmed',
        bg: '#ECFDF5',
        color: '#065F46',
        border: '#A7F3D0',
      };
    case 'pendingpayment':
      return {
        label: 'Pending Payment',
        bg: '#FFFBEB',
        color: '#92400E',
        border: '#FDE68A',
      };
    case 'cancelled':
      return {
        label: 'Cancelled',
        bg: '#FEF2F2',
        color: '#991B1B',
        border: '#FECACA',
      };
    case 'expired':
      return {
        label: 'Expired',
        bg: '#F1F5F9',
        color: '#475569',
        border: '#E2E8F0',
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
