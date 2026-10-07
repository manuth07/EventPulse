import test from 'node:test';
import assert from 'node:assert/strict';
import {
  formatTicketHistoryDate,
  formatCurrency,
  formatTicketTypesSummary,
  getStatusBadgeConfig,
} from './ticketHistoryHelper.js';

test('Ticket History Helpers (EP-354)', async (t) => {
  await t.test('formatTicketHistoryDate formats valid ISO string nicely', () => {
    const formatted = formatTicketHistoryDate('2026-11-20T12:00:00Z');
    assert.match(formatted, /Nov 2026/);
  });

  await t.test('formatTicketHistoryDate handles missing or null date', () => {
    assert.equal(formatTicketHistoryDate(null), 'Date unavailable');
    assert.equal(formatTicketHistoryDate(undefined), 'Date unavailable');
  });

  await t.test('formatCurrency formats valid numbers and strings', () => {
    assert.equal(formatCurrency(2500), 'LKR 2,500.00');
    assert.equal(formatCurrency('1500.5'), 'LKR 1,500.50');
    assert.equal(formatCurrency(0), 'LKR 0.00');
    assert.equal(formatCurrency('invalid'), 'LKR 0.00');
  });

  await t.test('formatTicketTypesSummary summarizes items and total', () => {
    const items = [
      { ticketName: 'VIP', quantity: 2 },
      { ticketName: 'General Admission', quantity: 3 },
    ];
    const summary = formatTicketTypesSummary(items, 5);
    assert.equal(summary, 'VIP (2), General Admission (3) · 5 total');
  });

  await t.test('formatTicketTypesSummary falls back to total count when items empty', () => {
    assert.equal(formatTicketTypesSummary([], 1), '1 ticket');
    assert.equal(formatTicketTypesSummary([], 4), '4 tickets');
  });

  await t.test('getStatusBadgeConfig returns appropriate styles for all statuses', () => {
    assert.equal(getStatusBadgeConfig('Confirmed').label, 'Confirmed');
    assert.equal(getStatusBadgeConfig('PendingPayment').label, 'Pending Payment');
    assert.equal(getStatusBadgeConfig('Cancelled').label, 'Cancelled');
    assert.equal(getStatusBadgeConfig('Expired').label, 'Expired');
    assert.equal(getStatusBadgeConfig('SomethingElse').label, 'SomethingElse');
  });
});
