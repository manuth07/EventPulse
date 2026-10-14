/**
 * Dynamic Reports Frontend Utilities (EP-379)
 * 
 * Provides pure formatting, criteria validation, date preset calculations,
 * and empty-state detection for the Administrator Reports page.
 */

export const SUPPORTED_REPORT_TYPES = [
  {
    id: 'platform-summary',
    label: 'Platform Activity Summary',
    description: 'High-level operational throughput, gross & net sales, and customer/event engagement metrics.',
  },
  {
    id: 'bookings',
    label: 'Bookings Report',
    description: 'Detailed order conversions, valid vs cancelled tickets, churn rates, and event/tier breakdowns.',
  },
  {
    id: 'revenue',
    label: 'Revenue Report',
    description: 'Financial sales reconciliation, refund deductions, net proceeds, and multi-currency analysis.',
  },
];

export const BOOKING_STATUS_OPTIONS = [
  { value: 'All', label: 'All Booking Statuses' },
  { value: 'Confirmed', label: 'Confirmed Only' },
  { value: 'Cancelled', label: 'Cancelled Only' },
  { value: 'PendingPayment', label: 'Pending Payment (Holds)' },
  { value: 'PaymentFailed', label: 'Payment Failed (Abandoned)' },
];

export const CURRENCY_OPTIONS = [
  { value: 'ALL', label: 'All Currencies (Segregated)' },
  { value: 'USD', label: 'USD ($)' },
  { value: 'LKR', label: 'LKR (Rs.)' },
];

/**
 * Validates report criteria before triggering generation.
 * 
 * @param {Object} criteria
 * @param {string} criteria.reportType
 * @param {string} [criteria.from] - YYYY-MM-DD or ISO string
 * @param {string} [criteria.to] - YYYY-MM-DD or ISO string
 * @param {string} [criteria.currency]
 * @returns {{ isValid: boolean, error: string | null }}
 */
export function validateReportCriteria(criteria) {
  if (!criteria || !criteria.reportType) {
    return { isValid: false, error: 'Please select a report type.' };
  }

  const validTypes = ['platform-summary', 'bookings', 'revenue'];
  if (!validTypes.includes(criteria.reportType)) {
    return { isValid: false, error: `Unsupported report type '${criteria.reportType}'.` };
  }

  if (criteria.from && criteria.to) {
    const fromDate = new Date(criteria.from);
    const toDate = new Date(criteria.to);

    if (isNaN(fromDate.getTime()) || isNaN(toDate.getTime())) {
      return { isValid: false, error: 'Invalid date format selected.' };
    }

    if (fromDate > toDate) {
      return { isValid: false, error: "The start date ('from') must be earlier than or equal to the end date ('to')." };
    }

    const diffDays = Math.ceil((toDate.getTime() - fromDate.getTime()) / (1000 * 60 * 60 * 24));
    if (diffDays > 730) {
      return { isValid: false, error: 'The selected date range exceeds the maximum allowed period of 730 days (2 years).' };
    }
  }

  if (criteria.currency && criteria.currency !== 'ALL') {
    const trimmed = criteria.currency.trim().toUpperCase();
    if (trimmed.length !== 3 || !/^[A-Z]{3}$/.test(trimmed)) {
      return { isValid: false, error: "Currency must be a valid 3-letter ISO code (e.g. 'USD', 'LKR')." };
    }
  }

  return { isValid: true, error: null };
}

/**
 * Formats an integer count with thousands separators.
 * Example: 15420 -> "15,420"
 * 
 * @param {number|string} count
 * @returns {string}
 */
export function formatReportCount(count) {
  const num = Number(count);
  if (isNaN(num)) return '0';
  return num.toLocaleString('en-US');
}

/**
 * Formats percentage rates.
 * Backend numbers are pre-multiplied by 100 (e.g., 8.33 -> "8.3%").
 * 
 * @param {number|string} rate
 * @returns {string}
 */
export function formatReportRate(rate) {
  const num = Number(rate);
  if (isNaN(num)) return '0.0%';
  return `${num.toFixed(1)}%`;
}

/**
 * Formats monetary amounts with currency symbol/code.
 * 
 * @param {number|string} amount
 * @param {string} [currency='USD']
 * @returns {string}
 */
export function formatReportCurrency(amount, currency = 'USD') {
  const num = Number(amount);
  if (isNaN(num)) return '$0.00';

  const code = (currency || 'USD').toUpperCase();
  const formattedNumber = num.toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });

  if (code === 'USD') return `$${formattedNumber}`;
  if (code === 'LKR') return `LKR ${formattedNumber}`;
  return `${code} ${formattedNumber}`;
}

/**
 * Formats a reporting period into a human-readable banner string.
 * 
 * @param {Object} [period]
 * @param {string|null} [period.from]
 * @param {string|null} [period.to]
 * @returns {string}
 */
export function formatReportingPeriodLabel(period) {
  if (!period || (!period.from && !period.to)) {
    return 'All Time (Lifetime Platform Data)';
  }

  const formatDate = (iso) => {
    if (!iso) return '';
    const d = new Date(iso);
    if (isNaN(d.getTime())) return '';
    return d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
  };

  const fromStr = formatDate(period.from);
  const toStr = formatDate(period.to);

  if (fromStr && toStr) {
    return `${fromStr} – ${toStr}`;
  }
  if (fromStr) {
    return `From ${fromStr} onwards`;
  }
  return `Up to ${toStr}`;
}

/**
 * Returns date range object ({ from, to }) for common reporting presets.
 * 
 * @param {string} presetKey - 'today' | 'last7Days' | 'last30Days' | 'thisMonth' | 'allTime'
 * @returns {{ from: string, to: string }} ISO date strings (YYYY-MM-DD)
 */
export function getDatePresetRange(presetKey) {
  const now = new Date();
  const formatYmd = (date) => {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  };

  const to = formatYmd(now);

  switch (presetKey) {
    case 'today':
      return { from: to, to };
    case 'last7Days': {
      const start = new Date(now);
      start.setDate(start.getDate() - 7);
      return { from: formatYmd(start), to };
    }
    case 'last30Days': {
      const start = new Date(now);
      start.setDate(start.getDate() - 30);
      return { from: formatYmd(start), to };
    }
    case 'thisMonth': {
      const start = new Date(now.getFullYear(), now.getMonth(), 1);
      return { from: formatYmd(start), to };
    }
    case 'lastQuarter': {
      const start = new Date(now);
      start.setDate(start.getDate() - 90);
      return { from: formatYmd(start), to };
    }
    case 'allTime':
    default:
      return { from: '', to: '' };
  }
}

/**
 * Determines if returned report data contains only zeros / empty values.
 * 
 * @param {string} reportType
 * @param {Object|null} data
 * @returns {boolean}
 */
export function isReportDataEmpty(reportType, data) {
  if (!data) return true;

  if (reportType === 'bookings') {
    return (
      (Number(data.totalBookings) || 0) === 0 &&
      (Number(data.grossTicketsSold) || 0) === 0 &&
      (Number(data.pendingPaymentBookings) || 0) === 0 &&
      (!Array.isArray(data.eventsBreakdown) || data.eventsBreakdown.length === 0)
    );
  }

  if (reportType === 'revenue') {
    return (
      (Number(data.grossRevenue) || 0) === 0 &&
      (Number(data.successfulPurchaseCount) || 0) === 0 &&
      (!Array.isArray(data.currencyBreakdown) || data.currencyBreakdown.length === 0)
    );
  }

  if (reportType === 'platform-summary') {
    return (
      (Number(data.totalBookings) || 0) === 0 &&
      (Number(data.primaryGrossRevenue) || 0) === 0 &&
      (Number(data.totalUniqueCustomers) || 0) === 0 &&
      (Number(data.totalActiveEvents) || 0) === 0
    );
  }

  return false;
}
