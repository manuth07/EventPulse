/**
 * Sales Analytics Dashboard Utilities (EP-385)
 * 
 * Provides pure formatting, multi-currency presentation, chart scaling calculations,
 * and filter helpers for the Organizer Sales Analytics Dashboard.
 */

/**
 * Formats a monetary amount with currency code.
 * Example: formatCurrencyAmount(1250.5, 'LKR') -> "LKR 1,250.50"
 * 
 * @param {number|string} amount - Monetary amount
 * @param {string} [currency='LKR'] - ISO currency code
 * @returns {string} Formatted currency string
 */
export function formatCurrencyAmount(amount, currency = 'LKR') {
  if (currency === 'MULTIPLE') {
    return 'Multi-Currency';
  }
  const curr = (!currency || currency.toUpperCase() === 'USD') ? 'LKR' : currency.toUpperCase();
  const num = Number(amount);
  if (isNaN(num)) {
    return `${curr} 0.00`;
  }
  return `${curr} ${num.toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;
}

/**
 * Formats integer count values (tickets, orders).
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
 * Formats multi-currency breakdowns for display without combining currencies.
 * Returns an array of human-readable breakdown items.
 * 
 * @param {Array<Object>} currencies - List of CurrencySalesSummaryDto objects
 * @returns {Array<{ currency: string, label: string, gross: string, net: string }>}
 */
export function formatMultiCurrencyBreakdown(currencies) {
  if (!Array.isArray(currencies) || currencies.length === 0) {
    return [];
  }

  return currencies.map((c) => {
    const curr = (c.currency && c.currency.toUpperCase() !== 'USD') ? c.currency.toUpperCase() : 'LKR';
    return {
      currency: curr,
      label: `${curr}: Gross ${formatCurrencyAmount(c.grossRevenue, curr)} | Net ${formatCurrencyAmount(c.netRevenue, curr)}`,
      gross: formatCurrencyAmount(c.grossRevenue, curr),
      net: formatCurrencyAmount(c.netRevenue, curr),
      ticketsSold: c.ticketsSold,
    };
  });
}

/**
 * Computes proportional revenue distribution for charts.
 * Clamps percentages between 0 and 100%.
 * 
 * @param {Array<Object>} items - Array of event or ticket type objects with grossRevenue
 * @returns {Array<Object>} Items augmented with percentage of total
 */
export function calculateRevenuePercentages(items) {
  if (!Array.isArray(items) || items.length === 0) {
    return [];
  }

  const totalRevenue = items.reduce((sum, item) => sum + (Number(item.grossRevenue) || 0), 0);

  return items.map((item) => {
    const revenue = Number(item.grossRevenue) || 0;
    const percentage = totalRevenue > 0 ? Math.round((revenue / totalRevenue) * 100) : 0;
    return {
      ...item,
      percentage: Math.min(100, Math.max(0, percentage)),
    };
  });
}

/**
 * Checks whether an organizer has zero sales across their portfolio.
 * 
 * @param {Object|null} summary - OrganizerSalesSummaryDto
 * @returns {boolean} True if gross revenue and tickets sold are both zero
 */
export function isZeroSales(summary) {
  if (!summary) return true;
  const gross = Number(summary.grossRevenue) || 0;
  const tickets = Number(summary.ticketsSold) || 0;
  const purchases = Number(summary.successfulPurchaseCount) || 0;
  return gross === 0 && tickets === 0 && purchases === 0;
}

/**
 * Formats ISO date timestamps to localized date/time.
 * 
 * @param {string|null} dateString
 * @returns {string}
 */
export function formatSaleDate(dateString) {
  if (!dateString) return 'No sales yet';
  try {
    const date = new Date(dateString);
    if (isNaN(date.getTime())) return 'No sales yet';
    return date.toLocaleDateString('en-GB', {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return 'No sales yet';
  }
}
