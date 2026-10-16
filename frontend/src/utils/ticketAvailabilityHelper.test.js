import { test, describe } from 'node:test';
import assert from 'node:assert/strict';
import {
  calculateRemainingQuantity,
  getTotalQuantity,
  isTicketSoldOut,
  formatAvailabilityText,
  isLowAvailability,
} from './ticketAvailabilityHelper.js';

describe('Ticket Availability Helpers (EP-402 & EP-403)', () => {
  test('calculateRemainingQuantity uses availableQuantity when present', () => {
    const ticket = {
      name: 'General Admission',
      totalQuantity: 100,
      remainingQuantity: 72,
      availableQuantity: 70, // 2 on hold
    };
    assert.equal(calculateRemainingQuantity(ticket), 70);
  });

  test('calculateRemainingQuantity uses remainingQuantity when availableQuantity is missing', () => {
    const ticket = {
      name: 'General Admission',
      totalQuantity: 100,
      remainingQuantity: 72,
      soldQuantity: 28,
    };
    assert.equal(calculateRemainingQuantity(ticket), 72);
  });

  test('calculateRemainingQuantity returns 0 if isSoldOut flag is true', () => {
    const ticket = {
      name: 'VIP',
      isSoldOut: true,
      remainingQuantity: 10,
      availableQuantity: 10,
    };
    assert.equal(calculateRemainingQuantity(ticket), 0);
  });

  test('calculateRemainingQuantity falls back to capacity minus bookedQuantity', () => {
    const ticket = {
      name: 'VIP',
      capacity: 50,
      bookedQuantity: 15,
    };
    assert.equal(calculateRemainingQuantity(ticket), 35);
  });

  test('calculateRemainingQuantity clamps negative values to 0', () => {
    const ticket = {
      name: 'Early Bird',
      totalQuantity: 10,
      remainingQuantity: -2,
    };
    assert.equal(calculateRemainingQuantity(ticket), 0);
  });

  test('calculateRemainingQuantity handles null or empty object gracefully', () => {
    assert.equal(calculateRemainingQuantity(null), 0);
    assert.equal(calculateRemainingQuantity({}), 0);
  });

  test('getTotalQuantity prioritizes totalQuantity over capacity', () => {
    const ticket1 = { totalQuantity: 100, capacity: 50 };
    assert.equal(getTotalQuantity(ticket1), 100);

    const ticket2 = { capacity: 75 };
    assert.equal(getTotalQuantity(ticket2), 75);

    assert.equal(getTotalQuantity(null), 0);
  });

  test('isTicketSoldOut returns true when remaining quantity is 0 or isSoldOut is true', () => {
    const ticket1 = {
      totalQuantity: 50,
      remainingQuantity: 0,
      soldQuantity: 50,
    };
    assert.equal(isTicketSoldOut(ticket1), true);

    const ticket2 = {
      isSoldOut: true,
      remainingQuantity: 5,
    };
    assert.equal(isTicketSoldOut(ticket2), true);
  });

  test('isTicketSoldOut returns false when remaining quantity > 0 and isSoldOut is false', () => {
    const availableTicket = {
      totalQuantity: 50,
      remainingQuantity: 1,
      availableQuantity: 1,
      isSoldOut: false,
    };
    assert.equal(isTicketSoldOut(availableTicket), false);
  });

  test('formatAvailabilityText formats remaining tickets according to prompt requirements', () => {
    assert.equal(formatAvailabilityText({ isSoldOut: true }), 'Sold Out');
    assert.equal(formatAvailabilityText({ availableQuantity: 0 }), 'Sold Out');
    assert.equal(formatAvailabilityText({ availableQuantity: 1 }), '1 ticket remaining');
    assert.equal(formatAvailabilityText({ availableQuantity: 15 }), '15 tickets remaining');
    assert.equal(formatAvailabilityText({ availableQuantity: 4 }), '4 tickets remaining');
    assert.equal(formatAvailabilityText(null), 'Availability unavailable');
  });

  test('isLowAvailability identifies <= 5 tickets remaining', () => {
    assert.equal(isLowAvailability({ availableQuantity: 4 }), true);
    assert.equal(isLowAvailability({ availableQuantity: 1 }), true);
    assert.equal(isLowAvailability({ availableQuantity: 5 }), true);
    assert.equal(isLowAvailability({ availableQuantity: 6 }), false);
    assert.equal(isLowAvailability({ availableQuantity: 0 }), false);
    assert.equal(isLowAvailability({ isSoldOut: true }), false);
  });
});
