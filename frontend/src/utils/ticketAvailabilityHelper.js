/**
 * Utility functions for ticket quantity and availability formatting (EP-402 / EP-403).
 */

export function calculateRemainingQuantity(ticketType) {
  if (!ticketType) return 0;
  // If explicitly flagged as sold out by backend or holds
  if (ticketType.isSoldOut) {
    return 0;
  }
  // AvailableQuantity reflects remaining minus active temporary holds
  if (typeof ticketType.availableQuantity === 'number') {
    return Math.max(0, ticketType.availableQuantity);
  }
  if (typeof ticketType.remainingQuantity === 'number') {
    return Math.max(0, ticketType.remainingQuantity);
  }
  const total = ticketType.totalQuantity ?? ticketType.capacity ?? 0;
  const sold = ticketType.soldQuantity ?? ticketType.bookedQuantity ?? 0;
  return Math.max(0, total - sold);
}

export function getTotalQuantity(ticketType) {
  if (!ticketType) return 0;
  return ticketType.totalQuantity ?? ticketType.capacity ?? 0;
}

export function isTicketSoldOut(ticketType) {
  if (!ticketType) return false;
  if (ticketType.isSoldOut === true) return true;
  const remaining = calculateRemainingQuantity(ticketType);
  return remaining <= 0;
}

/**
 * Formats user-facing availability text.
 * e.g. "Sold Out", "1 ticket remaining", "15 tickets remaining"
 */
export function formatAvailabilityText(ticketType) {
  if (!ticketType) return 'Availability unavailable';
  if (isTicketSoldOut(ticketType)) {
    return 'Sold Out';
  }
  const remaining = calculateRemainingQuantity(ticketType);
  if (remaining === 1) {
    return '1 ticket remaining';
  }
  return `${remaining} tickets remaining`;
}

/**
 * Checks whether availability is considered low (e.g., <= 5 tickets remaining).
 */
export function isLowAvailability(ticketType) {
  if (!ticketType) return false;
  if (isTicketSoldOut(ticketType)) return false;
  const remaining = calculateRemainingQuantity(ticketType);
  return remaining > 0 && remaining <= 5;
}
