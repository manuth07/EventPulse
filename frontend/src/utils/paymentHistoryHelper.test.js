import test from 'node:test';
import assert from 'node:assert/strict';
import {
  formatPaymentDate,
  formatCurrency,
  getPaymentStatusBadgeConfig,
} from './paymentHistoryHelper.js';

test('Payment History Helpers (EP-358 / EP-54)', async (t) => {
  await t.test('formatPaymentDate formats valid ISO string nicely without time', () => {
    const formatted = formatPaymentDate('2026-10-10T14:30:00Z', false);
    assert.match(formatted, /10 October 2026/);
  });

  await t.test('formatPaymentDate formats valid ISO string with time', () => {
    const formatted = formatPaymentDate('2026-10-10T14:30:00Z', true);
    assert.match(formatted, /10 October 2026 ·/);
  });

  await t.test('formatPaymentDate handles null and undefined gracefully', () => {
    assert.equal(formatPaymentDate(null), 'Date unavailable');
    assert.equal(formatPaymentDate(undefined), 'Date unavailable');
    assert.equal(formatPaymentDate(''), 'Date unavailable');
  });

  await t.test('formatCurrency formats valid numbers and strings', () => {
    assert.equal(formatCurrency(5000, 'LKR'), 'LKR 5,000.00');
    assert.equal(formatCurrency('125.5', 'usd'), 'USD 125.50');
    assert.equal(formatCurrency(0, 'lkr'), 'LKR 0.00');
    assert.equal(formatCurrency('invalid'), 'LKR 0.00');
  });

  await t.test('formatCurrency defaults to LKR when currency omitted', () => {
    assert.equal(formatCurrency(2500), 'LKR 2,500.00');
  });

  await t.test('getPaymentStatusBadgeConfig returns appropriate styles for all supported statuses', () => {
    assert.equal(getPaymentStatusBadgeConfig('Succeeded').label, 'Successful');
    assert.equal(getPaymentStatusBadgeConfig('Pending').label, 'Pending');
    assert.equal(getPaymentStatusBadgeConfig('Failed').label, 'Failed');
    assert.equal(getPaymentStatusBadgeConfig('Refunded').label, 'Refunded');
    assert.equal(getPaymentStatusBadgeConfig('UnknownStatus').label, 'UnknownStatus');
  });

  await t.test('getPaymentStatusBadgeConfig handles lowercase and null status strings', () => {
    assert.equal(getPaymentStatusBadgeConfig('succeeded').label, 'Successful');
    assert.equal(getPaymentStatusBadgeConfig('failed').label, 'Failed');
    assert.equal(getPaymentStatusBadgeConfig(null).label, 'Unknown');
  });
});
