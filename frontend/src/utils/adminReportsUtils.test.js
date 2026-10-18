import test, { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import {
  SUPPORTED_REPORT_TYPES,
  BOOKING_STATUS_OPTIONS,
  CURRENCY_OPTIONS,
  validateReportCriteria,
  formatReportCount,
  formatReportRate,
  formatReportCurrency,
  formatReportingPeriodLabel,
  getDatePresetRange,
  isReportDataEmpty,
} from './adminReportsUtils.js';

describe('Admin Reports Utilities (EP-379)', () => {
  describe('Constants configuration', () => {
    it('defines supported report types', () => {
      assert.equal(SUPPORTED_REPORT_TYPES.length, 3);
      const ids = SUPPORTED_REPORT_TYPES.map(r => r.id);
      assert.deepEqual(ids, ['platform-summary', 'bookings', 'revenue']);
    });

    it('defines booking status options', () => {
      const values = BOOKING_STATUS_OPTIONS.map(o => o.value);
      assert.ok(values.includes('All'));
      assert.ok(values.includes('Confirmed'));
      assert.ok(values.includes('Cancelled'));
      assert.ok(values.includes('PendingPayment'));
      assert.ok(values.includes('PaymentFailed'));
    });

    it('defines currency options', () => {
      const values = CURRENCY_OPTIONS.map(c => c.value);
      assert.ok(values.includes('ALL'));
      assert.ok(values.includes('LKR'));
      assert.ok(!values.includes('USD'));
    });
  });

  describe('validateReportCriteria', () => {
    it('rejects missing or empty report criteria', () => {
      assert.deepEqual(validateReportCriteria(null), {
        isValid: false,
        error: 'Please select a report type.',
      });
      assert.deepEqual(validateReportCriteria({}), {
        isValid: false,
        error: 'Please select a report type.',
      });
    });

    it('rejects unsupported report types', () => {
      const res = validateReportCriteria({ reportType: 'unsupported-type' });
      assert.equal(res.isValid, false);
      assert.ok(res.error.includes("Unsupported report type 'unsupported-type'"));
    });

    it('accepts valid report types without dates (lifetime/all-time)', () => {
      const res1 = validateReportCriteria({ reportType: 'platform-summary' });
      assert.equal(res1.isValid, true);
      assert.equal(res1.error, null);

      const res2 = validateReportCriteria({ reportType: 'bookings' });
      assert.equal(res2.isValid, true);

      const res3 = validateReportCriteria({ reportType: 'revenue' });
      assert.equal(res3.isValid, true);
    });

    it('rejects invalid date strings', () => {
      const res = validateReportCriteria({
        reportType: 'bookings',
        from: 'invalid-date',
        to: '2026-10-10',
      });
      assert.equal(res.isValid, false);
      assert.equal(res.error, 'Invalid date format selected.');
    });

    it('rejects when start date is after end date', () => {
      const res = validateReportCriteria({
        reportType: 'bookings',
        from: '2026-10-15',
        to: '2026-10-10',
      });
      assert.equal(res.isValid, false);
      assert.ok(res.error.includes("The start date ('from') must be earlier than or equal to the end date ('to')"));
    });

    it('rejects ranges exceeding 730 days (2 years)', () => {
      const res = validateReportCriteria({
        reportType: 'revenue',
        from: '2020-01-01',
        to: '2023-01-01',
      });
      assert.equal(res.isValid, false);
      assert.ok(res.error.includes('exceeds the maximum allowed period of 730 days'));
    });

    it('rejects invalid currency codes', () => {
      const res = validateReportCriteria({
        reportType: 'revenue',
        currency: 'US', // only 2 letters
      });
      assert.equal(res.isValid, false);
      assert.ok(res.error.includes('Currency must be a valid 3-letter ISO code'));
    });

    it('accepts valid date ranges and currency', () => {
      const res = validateReportCriteria({
        reportType: 'revenue',
        from: '2026-09-01',
        to: '2026-10-01',
        currency: 'USD',
      });
      assert.equal(res.isValid, true);
      assert.equal(res.error, null);
    });
  });

  describe('Formatting functions', () => {
    it('formatReportCount formats numbers and handles invalid values', () => {
      assert.equal(formatReportCount(0), '0');
      assert.equal(formatReportCount(1234), '1,234');
      assert.equal(formatReportCount('500000'), '500,000');
      assert.equal(formatReportCount(null), '0');
      assert.equal(formatReportCount(undefined), '0');
      assert.equal(formatReportCount('invalid'), '0');
    });

    it('formatReportRate formats percentage with 1 decimal place', () => {
      assert.equal(formatReportRate(0), '0.0%');
      assert.equal(formatReportRate(12.34), '12.3%');
      assert.equal(formatReportRate('75.89'), '75.9%');
      assert.equal(formatReportRate(null), '0.0%');
      assert.equal(formatReportRate('not-a-number'), '0.0%');
    });

    it('formatReportCurrency formats always in LKR appropriately', () => {
      assert.equal(formatReportCurrency(0), 'LKR 0.00');
      assert.equal(formatReportCurrency(1500.5), 'LKR 1,500.50');
      assert.equal(formatReportCurrency(25000, 'LKR'), 'LKR 25,000.00');
      assert.equal(formatReportCurrency(null), 'LKR 0.00');
      assert.equal(formatReportCurrency(2500), 'LKR 2,500.00');
    });

    it('formatReportingPeriodLabel generates clear period labels', () => {
      assert.equal(formatReportingPeriodLabel(null), 'All Time (Lifetime Platform Data)');
      assert.equal(formatReportingPeriodLabel({}), 'All Time (Lifetime Platform Data)');
      assert.equal(formatReportingPeriodLabel({ from: null, to: null }), 'All Time (Lifetime Platform Data)');

      const labelBoth = formatReportingPeriodLabel({
        from: '2026-01-01T00:00:00Z',
        to: '2026-01-31T23:59:59Z',
      });
      assert.ok(labelBoth.includes('2026'));
      assert.ok(labelBoth.includes('Jan'));

      const labelFromOnly = formatReportingPeriodLabel({
        from: '2026-01-01T00:00:00Z',
        to: null,
      });
      assert.ok(labelFromOnly.startsWith('From '));

      const labelToOnly = formatReportingPeriodLabel({
        from: null,
        to: '2026-01-31T23:59:59Z',
      });
      assert.ok(labelToOnly.startsWith('Up to '));
    });
  });

  describe('getDatePresetRange', () => {
    it('returns empty strings for allTime preset', () => {
      assert.deepEqual(getDatePresetRange('allTime'), { from: '', to: '' });
    });

    it('returns valid dates for last7Days preset', () => {
      const range = getDatePresetRange('last7Days');
      assert.ok(range.from);
      assert.ok(range.to);
      assert.ok(new Date(range.from) <= new Date(range.to));
    });

    it('returns valid dates for last30Days preset', () => {
      const range = getDatePresetRange('last30Days');
      assert.ok(range.from);
      assert.ok(range.to);
      assert.ok(new Date(range.from) <= new Date(range.to));
    });

    it('returns first of month for thisMonth preset', () => {
      const range = getDatePresetRange('thisMonth');
      assert.ok(range.from.endsWith('-01'));
      assert.ok(range.to);
    });
  });

  describe('isReportDataEmpty', () => {
    it('identifies null or undefined data as empty', () => {
      assert.equal(isReportDataEmpty('bookings', null), true);
      assert.equal(isReportDataEmpty('revenue', undefined), true);
    });

    it('identifies empty bookings report', () => {
      assert.equal(
        isReportDataEmpty('bookings', {
          totalBookings: 0,
          grossTicketsSold: 0,
          pendingPaymentBookings: 0,
          eventsBreakdown: [],
        }),
        true
      );

      assert.equal(
        isReportDataEmpty('bookings', {
          totalBookings: 5,
          grossTicketsSold: 10,
          eventsBreakdown: [],
        }),
        false
      );
    });

    it('identifies empty revenue report', () => {
      assert.equal(
        isReportDataEmpty('revenue', {
          grossRevenue: 0,
          successfulPurchaseCount: 0,
          currencyBreakdown: [],
        }),
        true
      );

      assert.equal(
        isReportDataEmpty('revenue', {
          grossRevenue: 150.0,
          successfulPurchaseCount: 2,
          currencyBreakdown: [{ currency: 'USD', grossRevenue: 150.0 }],
        }),
        false
      );
    });

    it('identifies empty platform-summary report', () => {
      assert.equal(
        isReportDataEmpty('platform-summary', {
          totalBookings: 0,
          primaryGrossRevenue: 0,
          totalUniqueCustomers: 0,
          totalActiveEvents: 0,
        }),
        true
      );

      assert.equal(
        isReportDataEmpty('platform-summary', {
          totalBookings: 0,
          primaryGrossRevenue: 0,
          totalUniqueCustomers: 12,
          totalActiveEvents: 2,
        }),
        false
      );
    });
  });
});
