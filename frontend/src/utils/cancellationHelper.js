/**
 * Cancellation reason codes, policy feedback, and refund outcome derivation helpers (EP-366 / EP-55).
 */

export const CANCELLATION_REASON_CODES = {
  ALREADY_CANCELLED: 'ALREADY_CANCELLED',
  CANCELLATION_WINDOW_CLOSED: 'CANCELLATION_WINDOW_CLOSED',
  EVENT_ALREADY_STARTED: 'EVENT_ALREADY_STARTED',
  INVALID_BOOKING_STATUS: 'INVALID_BOOKING_STATUS',
  PAYMENT_NOT_ELIGIBLE: 'PAYMENT_NOT_ELIGIBLE',
  UNAUTHORIZED: 'UNAUTHORIZED',
  BOOKING_NOT_FOUND: 'BOOKING_NOT_FOUND',
};

/**
 * Returns a human-friendly error message based on backend reason codes (EP-362 / EP-366).
 * 
 * @param {string} [reasonCode] - Backend reason code
 * @param {string} [fallbackMessage] - Optional fallback error message from API response
 * @returns {string} User-facing descriptive message
 */
export function getCancellationReasonMessage(reasonCode, fallbackMessage) {
  switch (reasonCode) {
    case CANCELLATION_REASON_CODES.ALREADY_CANCELLED:
      return 'This booking has already been cancelled.';
    case CANCELLATION_REASON_CODES.CANCELLATION_WINDOW_CLOSED:
      return 'The cancellation window has closed for this event. Cancellations are no longer permitted.';
    case CANCELLATION_REASON_CODES.EVENT_ALREADY_STARTED:
      return 'This event has already started or completed and can no longer be cancelled.';
    case CANCELLATION_REASON_CODES.INVALID_BOOKING_STATUS:
      return 'This booking cannot be cancelled because its current status does not permit cancellation.';
    case CANCELLATION_REASON_CODES.PAYMENT_NOT_ELIGIBLE:
      return 'Payment status is not eligible for cancellation or refund.';
    case CANCELLATION_REASON_CODES.UNAUTHORIZED:
      return 'You are not authorized to cancel this booking.';
    case CANCELLATION_REASON_CODES.BOOKING_NOT_FOUND:
      return 'Booking not found or has been removed.';
    default:
      return fallbackMessage || 'An error occurred while processing your cancellation. Please try again.';
  }
}

/**
 * Derives expected or final refund outcome information for a booking (EP-365 / EP-366).
 * Crucial: Never claims a refund is completed before authoritative backend confirmation.
 * 
 * @param {object} booking - Booking summary or detail object
 * @param {object} [cancellationResult] - Result payload returned from cancellation API
 * @returns {{
 *   statusLabel: string,
 *   badge: { label: string, bg: string, color: string, border: string },
 *   explanation: string,
 *   isRefundPending: boolean,
 *   isRefundApplicable: boolean,
 *   amount: number
 * }}
 */
export function getRefundOutcomeInfo(booking, cancellationResult) {
  const status = (booking?.status || '').toLowerCase();
  const previousStatus = (cancellationResult?.previousStatus || status).toLowerCase();
  const totalAmount = Number(booking?.totalAmount || cancellationResult?.totalAmount || 0);

  // Confirmed booking with monetary payment -> refund requested and pending
  if (previousStatus === 'confirmed' && totalAmount > 0) {
    return {
      statusLabel: 'Refund Pending',
      badge: {
        label: 'Refund Pending',
        bg: '#FFFBEB',
        color: '#B45309',
        border: '#FDE68A',
      },
      explanation:
        'A full refund request has been initiated to your original payment method. Please allow 5–10 business days for your payment provider to process the transaction.',
      isRefundPending: true,
      isRefundApplicable: true,
      amount: totalAmount,
    };
  }

  // Pending payment or zero-cost booking -> no charge, no refund required
  if (previousStatus === 'pendingpayment' || totalAmount <= 0) {
    return {
      statusLabel: 'No Refund Required',
      badge: {
        label: 'No Refund Required',
        bg: '#F1F5F9',
        color: '#475569',
        border: '#CBD5E1',
      },
      explanation:
        'No payment was captured for this booking. Your temporary ticket reservations have been released back to event capacity.',
      isRefundPending: false,
      isRefundApplicable: false,
      amount: 0,
    };
  }

  // Already cancelled or other status fallback
  return {
    statusLabel: 'No Refund Action',
    badge: {
      label: 'No Refund Action',
      bg: '#F3F4F6',
      color: '#4B5563',
      border: '#E5E7EB',
    },
    explanation: 'No further refund processing is required for this booking.',
    isRefundPending: false,
    isRefundApplicable: false,
    amount: 0,
  };
}

/**
 * Formats cancellation timestamp for user feedback (EP-366).
 * 
 * @param {string|Date} dateString - ISO timestamp
 * @returns {string} Formatted cancellation timestamp
 */
export function formatCancellationTime(dateString) {
  if (!dateString) return 'Just now';
  try {
    const d = new Date(dateString);
    if (isNaN(d.getTime())) return 'Just now';
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
    return 'Just now';
  }
}
