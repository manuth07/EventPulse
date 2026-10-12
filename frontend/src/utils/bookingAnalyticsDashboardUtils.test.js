import test, { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import {
  formatCount,
  formatRate,
  formatCapacityUtilization,
  formatAverageTickets,
  calculateBookingPercentages,
  calculateTierCapacityBreakdown,
  isZeroBookings,
  formatBookingDate,
} from './bookingAnalyticsDashboardUtils.js';

describe('Booking Analytics Dashboard Utilities (EP-390)', () => {
  it('formatCount formats integer counts with commas', () => {
    assert.equal(formatCount(0), '0');
    assert.equal(formatCount(1234), '1,234');
    assert.equal(formatCount('56789'), '56,789');
    assert.equal(formatCount(null), '0');
    assert.equal(formatCount(undefined), '0');
    assert.equal(formatCount('invalid'), '0');
  });

  it('formatRate formats percentage rates correctly', () => {
    assert.equal(formatRate(0), '0.0%');
    assert.equal(formatRate(12.5), '12.5%');
    assert.equal(formatRate('33.333'), '33.3%');
    assert.equal(formatRate(null), '0.0%');
    assert.equal(formatRate('abc'), '0.0%');
  });

  it('formatCapacityUtilization formats capacity utilization percentages', () => {
    assert.equal(formatCapacityUtilization(85.4), '85.4%');
    assert.equal(formatCapacityUtilization(100), '100.0%');
    assert.equal(formatCapacityUtilization(0), '0.0%');
  });

  it('formatAverageTickets formats average tickets per booking', () => {
    assert.equal(formatAverageTickets(2.35), '2.4');
    assert.equal(formatAverageTickets(1), '1.0');
    assert.equal(formatAverageTickets(null), '0.0');
  });

  it('calculateBookingPercentages computes proportional shares across events', () => {
    const events = [
      { eventId: '1', eventName: 'Event 1', totalBookings: 60 },
      { eventId: '2', eventName: 'Event 2', totalBookings: 40 },
    ];
    const result = calculateBookingPercentages(events);
    assert.equal(result.length, 2);
    assert.equal(result[0].percentage, 60);
    assert.equal(result[1].percentage, 40);

    // Empty or zero handling
    assert.deepEqual(calculateBookingPercentages([]), []);
    const zeroResult = calculateBookingPercentages([{ eventId: '1', totalBookings: 0 }]);
    assert.equal(zeroResult[0].percentage, 0);
  });

  it('calculateTierCapacityBreakdown computes active and remaining percentage breakdown', () => {
    const tiers = [
      { ticketTypeId: 't1', ticketTypeName: 'VIP', capacity: 100, activeTickets: 70, cancelledTickets: 10, remainingCapacity: 20 },
      { ticketTypeId: 't2', ticketTypeName: 'GA', capacity: 200, activeTickets: 100, cancelledTickets: 0, remainingCapacity: 100 },
    ];
    const result = calculateTierCapacityBreakdown(tiers);
    assert.equal(result.length, 2);
    assert.equal(result[0].activePercentage, 70);
    assert.equal(result[0].cancelledPercentage, 10);
    assert.equal(result[0].remainingPercentage, 20);

    assert.equal(result[1].activePercentage, 50);
    assert.equal(result[1].cancelledPercentage, 0);
    assert.equal(result[1].remainingPercentage, 50);
  });

  it('isZeroBookings identifies zero vs active portfolio/events', () => {
    assert.equal(isZeroBookings(null), true);
    assert.equal(isZeroBookings({ totalBookings: 0, activeTicketsBooked: 0, confirmedBookings: 0 }), true);
    assert.equal(isZeroBookings({ totalBookings: 5, activeTicketsBooked: 10, confirmedBookings: 5 }), false);
    assert.equal(isZeroBookings({ totalBookings: 1, activeTicketsBooked: 0, confirmedBookings: 0 }), false);
  });

  it('formatBookingDate formats ISO strings and handles invalid dates gracefully', () => {
    const valid = '2026-10-15T12:00:00Z';
    assert.match(formatBookingDate(valid), /15 Oct 2026|Oct 15, 2026/);
    assert.equal(formatBookingDate(null), '—');
    assert.equal(formatBookingDate('invalid-date'), '—');
  });
});
