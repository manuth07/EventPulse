/**
 * Report Filtering Frontend Utilities (EP-396 / EP-393)
 * 
 * Centralized specifications, presets, validation rules, active-filter badge builders,
 * and role-based permissions matrix for reporting and analytics filters.
 */

import { EVENT_CATEGORIES, normalizeCategory } from '../data/eventConstants.js';

export const CANONICAL_CATEGORIES = [...EVENT_CATEGORIES];

export const CATEGORY_OPTIONS = [
  { value: 'All', label: 'All Categories' },
  ...EVENT_CATEGORIES.map((cat) => ({ value: cat, label: cat })),
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

export const DATE_PRESET_OPTIONS = [
  { key: 'allTime', label: 'All Time' },
  { key: 'today', label: 'Today' },
  { key: 'last7Days', label: 'Last 7 Days' },
  { key: 'last30Days', label: 'Last 30 Days' },
  { key: 'thisMonth', label: 'This Month' },
  { key: 'lastQuarter', label: 'Last Quarter' },
];

/**
 * Checks whether the current user role is authorized to view and supply the Organizer filter.
 * Strictly Administrator only per EP-393 Section 4.
 * 
 * @param {string|null} role
 * @returns {boolean}
 */
export function canFilterOrganizer(role) {
  if (!role) return false;
  const normalized = String(role).trim().toLowerCase();
  return normalized === 'administrator' || normalized === 'admin';
}

/**
 * Generates default filter criteria according to EP-393 Section 6.1.
 * 
 * @param {string} [role='Administrator']
 * @returns {Object} Default filter state
 */
export function getDefaultFilters(role = 'Administrator') {
  return {
    from: '',
    to: '',
    activePreset: 'allTime',
    eventId: '',
    ticketTypeId: '',
    category: 'All',
    bookingStatus: 'All',
    currency: 'ALL',
    organizerId: '',
  };
}

/**
 * Calculates date range ({ from, to }) for common reporting presets.
 * 
 * @param {string} presetKey - 'allTime' | 'today' | 'last7Days' | 'last30Days' | 'thisMonth' | 'lastQuarter'
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
 * Formats a reporting period into a human-readable string.
 * 
 * @param {string|null} from
 * @param {string|null} to
 * @param {string} [presetKey]
 * @returns {string}
 */
export function formatPeriodDisplay(from, to, presetKey) {
  if (presetKey && presetKey !== 'custom' && presetKey !== 'allTime') {
    const opt = DATE_PRESET_OPTIONS.find((p) => p.key === presetKey);
    if (opt) return opt.label;
  }

  if (!from && !to) {
    return 'All Time (Lifetime)';
  }

  const formatDate = (iso) => {
    if (!iso) return '';
    const d = new Date(iso);
    if (isNaN(d.getTime())) return '';
    return d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
  };

  const fromStr = formatDate(from);
  const toStr = formatDate(to);

  if (fromStr && toStr) {
    return `${fromStr} – ${toStr}`;
  }
  if (fromStr) {
    return `From ${fromStr} onwards`;
  }
  return `Up to ${toStr}`;
}

/**
 * Validates report criteria and filter inputs before executing queries (EP-393 / EP-396).
 * 
 * @param {Object} filters
 * @param {Object} [options]
 * @param {string} [options.role='Administrator']
 * @param {boolean} [options.requireReportType=false]
 * @param {string} [options.reportType]
 * @returns {{ isValid: boolean, error: string | null }}
 */
export function validateFilterInputs(filters, options = {}) {
  if (!filters) {
    return { isValid: false, error: 'Filter criteria cannot be empty.' };
  }

  const role = options.role || 'Administrator';

  // Optional Report Type validation if requested
  if (options.requireReportType) {
    const rType = filters.reportType || options.reportType;
    if (!rType) {
      return { isValid: false, error: 'Please select a report type.' };
    }
    const validReportTypes = ['platform-summary', 'bookings', 'revenue'];
    if (!validReportTypes.includes(rType)) {
      return { isValid: false, error: `Unsupported report type '${rType}'.` };
    }
  }

  // 1. Date Range Validation
  if (filters.from && filters.to) {
    const fromDate = new Date(filters.from);
    const toDate = new Date(filters.to);

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
  } else if (filters.from) {
    const fromDate = new Date(filters.from);
    if (isNaN(fromDate.getTime())) {
      return { isValid: false, error: 'Invalid date format selected.' };
    }
  } else if (filters.to) {
    const toDate = new Date(filters.to);
    if (isNaN(toDate.getTime())) {
      return { isValid: false, error: 'Invalid date format selected.' };
    }
  }

  // 2. Ticket Type Dependency: Ticket Type requires an Event
  const hasEvent = Boolean(
    filters.eventId &&
    typeof filters.eventId === 'string' &&
    filters.eventId.trim() &&
    filters.eventId !== 'ALL'
  );

  const hasTicketType = Boolean(
    filters.ticketTypeId &&
    typeof filters.ticketTypeId === 'string' &&
    filters.ticketTypeId.trim() &&
    filters.ticketTypeId !== 'ALL'
  );

  if (hasTicketType && !hasEvent) {
    return {
      isValid: false,
      error: 'An event must be selected when filtering by ticket type.',
    };
  }

  const UUID_REGEX = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

  if (hasEvent && !UUID_REGEX.test(filters.eventId.trim())) {
    return {
      isValid: false,
      error: "Event must be selected from the list or provided as a valid UUID (e.g. '12345678-1234-1234-1234-123456789abc').",
    };
  }

  if (hasTicketType && !UUID_REGEX.test(filters.ticketTypeId.trim())) {
    return {
      isValid: false,
      error: "Ticket Tier must be selected from the list or provided as a valid UUID (e.g. '12345678-1234-1234-1234-123456789abc').",
    };
  }

  // 3. Category Validation
  if (filters.category && filters.category !== 'All' && typeof filters.category === 'string') {
    const norm = normalizeCategory(filters.category.trim());
    if (!EVENT_CATEGORIES.includes(norm)) {
      return {
        isValid: false,
        error: `Unsupported event category '${filters.category}'. Valid categories are Music, Sports, Conference, Workshop, Festival, Arts & Theatre, Community, Other.`,
      };
    }
  }

  // 4. Role Authorization: organizerId is strictly Administrator only
  if (filters.organizerId && typeof filters.organizerId === 'string' && filters.organizerId.trim()) {
    if (!canFilterOrganizer(role)) {
      return {
        isValid: false,
        error: 'You do not have permission to filter by organizer.',
      };
    }
    if (!UUID_REGEX.test(filters.organizerId.trim())) {
      return {
        isValid: false,
        error: "Organizer ID must be a valid UUID format (e.g. '12345678-1234-1234-1234-123456789abc').",
      };
    }
  }

  // 5. Currency format validation
  if (filters.currency && filters.currency !== 'ALL' && typeof filters.currency === 'string') {
    const trimmed = filters.currency.trim().toUpperCase();
    if (trimmed.length !== 3 || !/^[A-Z]{3}$/.test(trimmed)) {
      return { isValid: false, error: "Currency must be a valid 3-letter ISO code (e.g. 'USD', 'LKR')." };
    }
  }

  // 6. Booking Status validation
  if (filters.bookingStatus && filters.bookingStatus !== 'All' && typeof filters.bookingStatus === 'string') {
    const validStatuses = ['Confirmed', 'Cancelled', 'PendingPayment', 'PaymentFailed'];
    if (!validStatuses.includes(filters.bookingStatus.trim())) {
      return {
        isValid: false,
        error: 'Invalid booking status filter. Permitted values are All, Confirmed, Cancelled, PendingPayment, PaymentFailed.',
      };
    }
  }

  return { isValid: true, error: null };
}

/**
 * Checks whether any filter criteria is active (diverges from defaults).
 * 
 * @param {Object} filters
 * @param {string} [role='Administrator']
 * @returns {boolean}
 */
export function hasActiveFilters(filters, role = 'Administrator') {
  if (!filters) return false;

  const hasDate = Boolean(
    (filters.from && filters.from.trim()) ||
    (filters.to && filters.to.trim()) ||
    (filters.activePreset && filters.activePreset !== 'allTime')
  );

  const hasEvent = Boolean(
    filters.eventId &&
    filters.eventId !== 'ALL' &&
    typeof filters.eventId === 'string' &&
    filters.eventId.trim()
  );

  const hasTicketType = Boolean(
    filters.ticketTypeId &&
    filters.ticketTypeId !== 'ALL' &&
    typeof filters.ticketTypeId === 'string' &&
    filters.ticketTypeId.trim()
  );

  const hasCategory = Boolean(
    filters.category &&
    filters.category !== 'All' &&
    typeof filters.category === 'string' &&
    filters.category.trim()
  );

  const hasStatus = Boolean(
    filters.bookingStatus &&
    filters.bookingStatus !== 'All' &&
    typeof filters.bookingStatus === 'string' &&
    filters.bookingStatus.trim()
  );

  const hasCurrency = Boolean(
    filters.currency &&
    filters.currency !== 'ALL' &&
    typeof filters.currency === 'string' &&
    filters.currency.trim()
  );

  const hasOrganizer = Boolean(
    canFilterOrganizer(role) &&
    filters.organizerId &&
    typeof filters.organizerId === 'string' &&
    filters.organizerId.trim()
  );

  return hasDate || hasEvent || hasTicketType || hasCategory || hasStatus || hasCurrency || hasOrganizer;
}

/**
 * Generates structured active filter badges/chips with labels and dismiss callbacks.
 * 
 * @param {Object} filters
 * @param {Object} options
 * @param {string} [options.role='Administrator']
 * @param {Function} options.onRemove - Callback (fieldKey) => void
 * @param {Object} [options.eventLabels={}] - Map of eventId to display name
 * @param {Object} [options.ticketTypeLabels={}] - Map of ticketTypeId to display name
 * @returns {Array<{ id: string, label: string, onRemove: Function }>}
 */
export function getActiveFilterChips(filters, options = {}) {
  if (!filters) return [];

  const { role = 'Administrator', onRemove, eventLabels = {}, ticketTypeLabels = {} } = options;
  const chips = [];

  // Date Range Chip
  const hasDate = Boolean(
    (filters.from && filters.from.trim()) ||
    (filters.to && filters.to.trim()) ||
    (filters.activePreset && filters.activePreset !== 'allTime')
  );

  if (hasDate) {
    chips.push({
      id: 'dateRange',
      label: `Period: ${formatPeriodDisplay(filters.from, filters.to, filters.activePreset)}`,
      onRemove: () => onRemove && onRemove('dateRange'),
    });
  }

  // Category Chip
  if (filters.category && filters.category !== 'All' && filters.category.trim()) {
    chips.push({
      id: 'category',
      label: `Category: ${filters.category}`,
      onRemove: () => onRemove && onRemove('category'),
    });
  }

  // Event Chip
  if (filters.eventId && filters.eventId !== 'ALL' && filters.eventId.trim()) {
    const eventName = eventLabels[filters.eventId] || filters.eventId;
    chips.push({
      id: 'eventId',
      label: `Event: ${eventName}`,
      onRemove: () => onRemove && onRemove('eventId'),
    });
  }

  // Ticket Type Chip
  if (filters.ticketTypeId && filters.ticketTypeId !== 'ALL' && filters.ticketTypeId.trim()) {
    const ticketName = ticketTypeLabels[filters.ticketTypeId] || filters.ticketTypeId;
    chips.push({
      id: 'ticketTypeId',
      label: `Ticket Tier: ${ticketName}`,
      onRemove: () => onRemove && onRemove('ticketTypeId'),
    });
  }

  // Booking Status Chip
  if (filters.bookingStatus && filters.bookingStatus !== 'All' && filters.bookingStatus.trim()) {
    chips.push({
      id: 'bookingStatus',
      label: `Status: ${filters.bookingStatus}`,
      onRemove: () => onRemove && onRemove('bookingStatus'),
    });
  }

  // Currency Chip
  if (filters.currency && filters.currency !== 'ALL' && filters.currency.trim()) {
    chips.push({
      id: 'currency',
      label: `Currency: ${filters.currency}`,
      onRemove: () => onRemove && onRemove('currency'),
    });
  }

  // Organizer Chip (Admin only)
  if (canFilterOrganizer(role) && filters.organizerId && filters.organizerId.trim()) {
    chips.push({
      id: 'organizerId',
      label: `Organizer: ${filters.organizerId}`,
      onRemove: () => onRemove && onRemove('organizerId'),
    });
  }

  return chips;
}
