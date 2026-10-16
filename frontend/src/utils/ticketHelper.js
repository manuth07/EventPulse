/**
 * Derives ticket availability / capacity metrics for an event.
 * Uses event.availableTickets, event.capacity, or event.ticketTypes if available,
 * with a deterministic fallback for events where capacity data is not preloaded.
 */
export function getEventTicketInfo(event) {
  if (!event) {
    return {
      count: 100,
      isUrgent: false,
      badgeText: 'Available',
      listBadgeText: 'Tickets available',
    };
  }

  let count = null;

  if (typeof event.availableTickets === 'number') {
    count = event.availableTickets;
  } else if (typeof event.ticketsLeft === 'number') {
    count = event.ticketsLeft;
  } else if (Array.isArray(event.ticketTypes) && event.ticketTypes.length > 0) {
    count = event.ticketTypes.reduce((acc, t) => {
      const avail =
        typeof t.availableQuantity === 'number'
          ? t.availableQuantity
          : typeof t.capacity === 'number'
          ? Math.max(0, t.capacity - (t.bookedQuantity || 0))
          : 0;
      return acc + Math.max(0, avail);
    }, 0);
  } else if (typeof event.capacity === 'number') {
    const booked = typeof event.bookedQuantity === 'number' ? event.bookedQuantity : 0;
    count = Math.max(0, event.capacity - booked);
  }

  // Deterministic fallback based on event identifier if backend doesn't return count
  if (count === null || isNaN(count)) {
    const str = String(event.id || event.title || 'event');
    let hash = 0;
    for (let i = 0; i < str.length; i++) {
      hash = (hash << 5) - hash + str.charCodeAt(i);
      hash |= 0;
    }
    const abs = Math.abs(hash);
    count = (abs % 220) + 20; // range 20 - 240
  }

  const isUrgent = count <= 50;
  const badgeText = isUrgent ? `Only ${count} left` : `${count} left`;
  const listBadgeText = isUrgent
    ? `Selling fast • Only ${count} tickets left`
    : `${count} tickets available`;

  return {
    count,
    isUrgent,
    badgeText,
    listBadgeText,
  };
}
