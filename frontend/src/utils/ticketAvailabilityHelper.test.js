import { test, describe } from 'node:test';
import assert from 'node:assert/strict';
import {
  calculateRemainingQuantity,
  getTotalQuantity,
  isTicketSoldOut,
} from './ticketAvailabilityHelper.js';

describe('Ticket Availability Helpers (EP-402)', () => {
  test('calculateRemainingQuantity uses remainingQuantity when present', () => {
    const ticket = {
      name: 'General Admission',
      totalQuantity: 100,
      remainingQuantity: 72,
      soldQuantity: 28,
    };
    assert.equal(calculateRemainingQuantity(ticket), 72);
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

  test('isTicketSoldOut returns true when remaining quantity is 0', () => {
    const soldOutTicket = {
      totalQuantity: 50,
      remainingQuantity: 0,
      soldQuantity: 50,
    };
    assert.equal(isTicketSoldOut(soldOutTicket), true);
  });

  test('isTicketSoldOut returns false when remaining quantity > 0', () => {
    const availableTicket = {
      totalQuantity: 50,
      remainingQuantity: 1,
      soldQuantity: 49,
    };
    assert.equal(isTicketSoldOut(availableTicket), false);
  });
});
