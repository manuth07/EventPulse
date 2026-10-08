/**
 * Utility functions for ticket quantity and availability formatting (EP-402).
 */

export function calculateRemainingQuantity(ticketType) {
  if (!ticketType) return 0;
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
  const remaining = calculateRemainingQuantity(ticketType);
  return remaining <= 0;
}
