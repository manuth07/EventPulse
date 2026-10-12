/**
 * Booking Analytics Dashboard Utilities (EP-390)
 * 
 * Provides pure formatting, metric calculation, capacity scaling,
 * and filter helpers for the Organizer Booking Analytics Dashboard.
 */

/**
 * Formats integer count values (bookings, tickets, capacity).
 * Example: formatCount(1250) -> "1,250"
 * 
 * @param {number|string} count
 * @returns {string}
 */
export function formatCount(count) {
  const num = Number(count);
  if (isNaN(num)) return '0';
  return num.toLocaleString('en-US');
}

/**
 * Formats percentage rates (cancellation rate, ticket cancellation rate).
 * Handles backend numbers (which are pre-multiplied by 100, e.g. 12.5 -> "12.5%").
 * 
 * @param {number|string} rate
 * @returns {string}
 */
export function formatRate(rate) {
  const num = Number(rate);
  if (isNaN(num)) return '0.0%';
  return `${num.toFixed(1)}%`;
}

/**
 * Formats capacity utilization rate.
 * Example: formatCapacityUtilization(85.4) -> "85.4%"
 * 
 * @param {number|string} rate
 * @returns {string}
 */
export function formatCapacityUtilization(rate) {
  const num = Number(rate);
  if (isNaN(num)) return '0.0%';
  return `${num.toFixed(1)}%`;
}

/**
 * Formats average tickets per booking.
 * Example: formatAverageTickets(2.35) -> "2.4"
 * 
 * @param {number|string} avg
 * @returns {string}
 */
export function formatAverageTickets(avg) {
  const num = Number(avg);
  if (isNaN(num)) return '0.0';
  return num.toFixed(1);
}

/**
 * Computes proportional booking distribution across events for chart visualization.
 * 
 * @param {Array<Object>} items - Array of event items with totalBookings
 * @returns {Array<Object>} Items augmented with percentage of total
 */
export function calculateBookingPercentages(items) {
  if (!Array.isArray(items) || items.length === 0) {
    return [];
  }

  const total = items.reduce((sum, item) => sum + (Number(item.totalBookings) || 0), 0);

  return items.map((item) => {
    const bookings = Number(item.totalBookings) || 0;
    const percentage = total > 0 ? Math.round((bookings / total) * 100) : 0;
    return {
      ...item,
      percentage: Math.min(100, Math.max(0, percentage)),
    };
  });
}

/**
 * Computes capacity utilization percentages for ticket tiers in an event.
 * 
 * @param {Array<Object>} ticketTypes - Array of TicketTypeBookingSummaryDto
 * @returns {Array<Object>} Ticket types with activePercentage, remainingPercentage, cancelledPercentage
 */
export function calculateTierCapacityBreakdown(ticketTypes) {
  if (!Array.isArray(ticketTypes) || ticketTypes.length === 0) {
    return [];
  }

  return ticketTypes.map((tier) => {
    const capacity = Number(tier.capacity) || (Number(tier.activeTickets) + Number(tier.remainingCapacity)) || 0;
    const active = Number(tier.activeTickets) || 0;
    const cancelled = Number(tier.cancelledTickets) || 0;
    const remaining = Number(tier.remainingCapacity) || 0;

    const activePct = capacity > 0 ? Math.round((active / capacity) * 100) : 0;
    const cancelledPct = capacity > 0 ? Math.round((cancelled / capacity) * 100) : 0;
    const remainingPct = capacity > 0 ? Math.max(0, 100 - activePct - cancelledPct) : 0;

    return {
      ...tier,
      computedCapacity: capacity,
      activePercentage: Math.min(100, Math.max(0, activePct)),
      cancelledPercentage: Math.min(100, Math.max(0, cancelledPct)),
      remainingPercentage: Math.min(100, Math.max(0, remainingPct)),
    };
  });
}

/**
 * Checks whether an organizer or event has zero bookings across their portfolio/timeline.
 * 
 * @param {Object|null} summary - OrganizerBookingSummaryDto or EventBookingAnalyticsDto
 * @returns {boolean} True if totalBookings and activeTicketsBooked are both zero
 */
export function isZeroBookings(summary) {
  if (!summary) return true;
  const total = Number(summary.totalBookings) || 0;
  const activeTickets = Number(summary.activeTicketsBooked) || 0;
  const confirmed = Number(summary.confirmedBookings) || 0;
  return total === 0 && activeTickets === 0 && confirmed === 0;
}

/**
 * Formats a timestamp into human-readable date for table and reporting headers.
 * 
 * @param {string|null} dateString
 * @returns {string} Formatted date string or '—'
 */
export function formatBookingDate(dateString) {
  if (!dateString) return '—';
  try {
    const date = new Date(dateString);
    if (isNaN(date.getTime())) return '—';
    return date.toLocaleDateString('en-GB', {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
    });
  } catch {
    return '—';
  }
}
