import test, { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import {
  formatCurrencyAmount,
  formatCount,
  formatMultiCurrencyBreakdown,
  calculateRevenuePercentages,
  isZeroSales,
  formatSaleDate,
} from './salesAnalyticsDashboardUtils.js';

describe('Sales Analytics Dashboard Utilities (EP-385)', () => {
  // 1. Currency Formatting
  it('formatCurrencyAmount formats standard currency amounts correctly', () => {
    assert.equal(formatCurrencyAmount(1500, 'USD'), 'USD 1,500.00');
    assert.equal(formatCurrencyAmount(250000, 'LKR'), 'LKR 250,000.00');
    assert.equal(formatCurrencyAmount(0, 'USD'), 'USD 0.00');
    assert.equal(formatCurrencyAmount(null, 'USD'), 'USD 0.00');
    assert.equal(formatCurrencyAmount(120, 'MULTIPLE'), 'Multi-Currency');
  });

  // 2. Count Formatting
  it('formatCount formats integer counts with commas', () => {
    assert.equal(formatCount(1234), '1,234');
    assert.equal(formatCount(0), '0');
    assert.equal(formatCount(null), '0');
  });

  // 3. Multi-Currency Breakdown
  it('formatMultiCurrencyBreakdown creates separate breakdown items without merging currencies', () => {
    const currencies = [
      { currency: 'USD', grossRevenue: 1000, netRevenue: 900, ticketsSold: 10 },
      { currency: 'EUR', grossRevenue: 500, netRevenue: 450, ticketsSold: 5 },
    ];

    const result = formatMultiCurrencyBreakdown(currencies);
    assert.equal(result.length, 2);
    assert.equal(result[0].currency, 'USD');
    assert.equal(result[0].gross, 'USD 1,000.00');
    assert.equal(result[1].currency, 'EUR');
    assert.equal(result[1].gross, 'EUR 500.00');
  });

  // 4. Revenue Percentages Calculation
  it('calculateRevenuePercentages calculates proportional distribution for charts', () => {
    const items = [
      { id: '1', grossRevenue: 300 },
      { id: '2', grossRevenue: 700 },
    ];

    const result = calculateRevenuePercentages(items);
    assert.equal(result.length, 2);
    assert.equal(result[0].percentage, 30);
    assert.equal(result[1].percentage, 70);
  });

  it('calculateRevenuePercentages handles empty or zero total safely', () => {
    assert.deepEqual(calculateRevenuePercentages([]), []);

    const zeroItems = [
      { id: '1', grossRevenue: 0 },
      { id: '2', grossRevenue: 0 },
    ];
    const zeroResult = calculateRevenuePercentages(zeroItems);
    assert.equal(zeroResult[0].percentage, 0);
    assert.equal(zeroResult[1].percentage, 0);
  });

  // 5. Zero Sales Detection
  it('isZeroSales detects whether an organizer has no sales', () => {
    assert.equal(isZeroSales(null), true);
    assert.equal(
      isZeroSales({ grossRevenue: 0, ticketsSold: 0, successfulPurchaseCount: 0 }),
      true
    );
    assert.equal(
      isZeroSales({ grossRevenue: 100, ticketsSold: 2, successfulPurchaseCount: 1 }),
      false
    );
  });

  // 6. Sale Date Formatting
  it('formatSaleDate formats valid timestamps and falls back for null/invalid', () => {
    assert.equal(formatSaleDate(null), 'No sales yet');
    assert.equal(formatSaleDate('invalid-date'), 'No sales yet');

    const formatted = formatSaleDate('2026-10-11T12:00:00Z');
    assert.match(formatted, /Oct/);
    assert.match(formatted, /2026/);
  });
});
