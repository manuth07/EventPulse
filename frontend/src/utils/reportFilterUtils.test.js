import test, { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import {
  CANONICAL_CATEGORIES,
  CATEGORY_OPTIONS,
  BOOKING_STATUS_OPTIONS,
  CURRENCY_OPTIONS,
  DATE_PRESET_OPTIONS,
  canFilterOrganizer,
  getDefaultFilters,
  getDatePresetRange,
  formatPeriodDisplay,
  validateFilterInputs,
  hasActiveFilters,
  getActiveFilterChips,
} from './reportFilterUtils.js';

describe('Report Filter Controls Utilities (EP-396 / EP-393)', () => {
  describe('Constants and Permissions Matrix', () => {
    it('defines canonical categories according to EP-393', () => {
      assert.deepEqual(CANONICAL_CATEGORIES, [
        'Music',
        'Sports',
        'Conference',
        'Workshop',
        'Festival',
        'Arts & Theatre',
        'Community',
        'Other',
      ]);
      assert.equal(CATEGORY_OPTIONS.length, 9);
      assert.equal(CATEGORY_OPTIONS[0].value, 'All');
    });

    it('defines supported booking statuses and currencies', () => {
      const statuses = BOOKING_STATUS_OPTIONS.map((s) => s.value);
      assert.ok(statuses.includes('All'));
      assert.ok(statuses.includes('Confirmed'));
      assert.ok(statuses.includes('Cancelled'));
      assert.ok(statuses.includes('PendingPayment'));
      assert.ok(statuses.includes('PaymentFailed'));

      const currencies = CURRENCY_OPTIONS.map((c) => c.value);
      assert.ok(currencies.includes('ALL'));
      assert.ok(currencies.includes('USD'));
      assert.ok(currencies.includes('LKR'));
    });

    it('enforces role-based permissions: only Administrator can filter by organizerId', () => {
      assert.equal(canFilterOrganizer('Administrator'), true);
      assert.equal(canFilterOrganizer('Admin'), true);
      assert.equal(canFilterOrganizer('administrator'), true);
      assert.equal(canFilterOrganizer('Organizer'), false);
      assert.equal(canFilterOrganizer('Customer'), false);
      assert.equal(canFilterOrganizer(null), false);
      assert.equal(canFilterOrganizer(undefined), false);
    });

    it('provides correct default filter states', () => {
      const defaults = getDefaultFilters('Administrator');
      assert.deepEqual(defaults, {
        from: '',
        to: '',
        activePreset: 'allTime',
        eventId: '',
        ticketTypeId: '',
        category: 'All',
        bookingStatus: 'All',
        currency: 'ALL',
        organizerId: '',
      });
    });
  });

  describe('Date Range Presets & Formatter', () => {
    it('calculates date ranges for presets correctly', () => {
      const allTime = getDatePresetRange('allTime');
      assert.deepEqual(allTime, { from: '', to: '' });

      const today = getDatePresetRange('today');
      assert.ok(today.from && today.to);
      assert.equal(today.from, today.to);

      const last7 = getDatePresetRange('last7Days');
      assert.ok(last7.from < last7.to);

      const last30 = getDatePresetRange('last30Days');
      assert.ok(last30.from < last30.to);

      const thisMonth = getDatePresetRange('thisMonth');
      assert.ok(thisMonth.from.endsWith('-01'));

      const lastQuarter = getDatePresetRange('lastQuarter');
      assert.ok(lastQuarter.from < lastQuarter.to);
    });

    it('formats period display nicely', () => {
      assert.equal(formatPeriodDisplay('', ''), 'All Time (Lifetime)');
      assert.equal(formatPeriodDisplay('', '', 'last7Days'), 'Last 7 Days');
      const formatted = formatPeriodDisplay('2026-10-01', '2026-10-14');
      assert.ok(formatted.includes('2026'));
      assert.ok(formatted.includes('–'));
    });
  });

  describe('Client-Side Filter Validation (EP-396 Requirement 9)', () => {
    it('accepts clean default filters', () => {
      const res = validateFilterInputs(getDefaultFilters());
      assert.equal(res.isValid, true);
      assert.equal(res.error, null);
    });

    it('rejects invalid date strings', () => {
      const res = validateFilterInputs({ from: 'not-a-date', to: '2026-10-10' });
      assert.equal(res.isValid, false);
      assert.equal(res.error, 'Invalid date format selected.');
    });

    it('rejects when start date is later than end date (from > to)', () => {
      const res = validateFilterInputs({
        from: '2026-10-20',
        to: '2026-10-10',
      });
      assert.equal(res.isValid, false);
      assert.equal(
        res.error,
        "The start date ('from') must be earlier than or equal to the end date ('to')."
      );
    });

    it('rejects date range exceeding 730 days (2 years)', () => {
      const res = validateFilterInputs({
        from: '2020-01-01',
        to: '2023-01-01',
      });
      assert.equal(res.isValid, false);
      assert.ok(res.error.includes('exceeds the maximum allowed period of 730 days'));
    });

    it('enforces ticket type dependency: requires event selection', () => {
      // Missing eventId
      const resNoEvent = validateFilterInputs({
        ticketTypeId: 'e14dc9bc-8ff2-49d2-b05f-2ec828bb77aa',
        eventId: '',
      });
      assert.equal(resNoEvent.isValid, false);
      assert.equal(resNoEvent.error, 'An event must be selected when filtering by ticket type.');

      // Event is 'ALL'
      const resAllEvent = validateFilterInputs({
        ticketTypeId: 'e14dc9bc-8ff2-49d2-b05f-2ec828bb77aa',
        eventId: 'ALL',
      });
      assert.equal(resAllEvent.isValid, false);
      assert.equal(resAllEvent.error, 'An event must be selected when filtering by ticket type.');

      // Valid with eventId
      const resValid = validateFilterInputs({
        ticketTypeId: 'e14dc9bc-8ff2-49d2-b05f-2ec828bb77aa',
        eventId: 'a12bc3de-4f56-7890-abcd-ef1234567890',
      });
      assert.equal(resValid.isValid, true);
    });

    it('validates categories against canonical list and normalized aliases', () => {
      const validCanonical = validateFilterInputs({ category: 'Music' });
      assert.equal(validCanonical.isValid, true);

      const validLegacy = validateFilterInputs({ category: 'Musical Concert' });
      assert.equal(validLegacy.isValid, true);

      const invalidCategory = validateFilterInputs({ category: 'UnknownSciFiGenre' });
      assert.equal(invalidCategory.isValid, false);
      assert.ok(invalidCategory.error.includes("Unsupported event category 'UnknownSciFiGenre'"));
    });

    it('rejects organizerId when supplied by non-administrator (Organizer / Customer)', () => {
      const resOrganizer = validateFilterInputs(
        { organizerId: '00000000-0000-0000-0000-000000000001' },
        { role: 'Organizer' }
      );
      assert.equal(resOrganizer.isValid, false);
      assert.equal(resOrganizer.error, 'You do not have permission to filter by organizer.');

      const resCustomer = validateFilterInputs(
        { organizerId: '00000000-0000-0000-0000-000000000001' },
        { role: 'Customer' }
      );
      assert.equal(resCustomer.isValid, false);

      const resAdmin = validateFilterInputs(
        { organizerId: '00000000-0000-0000-0000-000000000001' },
        { role: 'Administrator' }
      );
      assert.equal(resAdmin.isValid, true);
    });

    it('validates currency format (3 uppercase letters)', () => {
      const resInvalid = validateFilterInputs({ currency: 'INVALID' });
      assert.equal(resInvalid.isValid, false);
      assert.ok(resInvalid.error.includes('3-letter ISO code'));

      const resValid = validateFilterInputs({ currency: 'USD' });
      assert.equal(resValid.isValid, true);
    });

    it('validates booking status values', () => {
      const resInvalid = validateFilterInputs({ bookingStatus: 'FakeStatus' });
      assert.equal(resInvalid.isValid, false);
      assert.ok(resInvalid.error.includes('Invalid booking status filter'));

      const resValid = validateFilterInputs({ bookingStatus: 'Confirmed' });
      assert.equal(resValid.isValid, true);
    });
  });

  describe('Active Filters & Badge/Chip Generation (EP-396 Requirement 7)', () => {
    it('detects when filters are active vs default', () => {
      const defaults = getDefaultFilters();
      assert.equal(hasActiveFilters(defaults), false);

      assert.equal(hasActiveFilters({ ...defaults, category: 'Sports' }), true);
      assert.equal(hasActiveFilters({ ...defaults, from: '2026-10-01' }), true);
      assert.equal(hasActiveFilters({ ...defaults, bookingStatus: 'Confirmed' }), true);
      assert.equal(hasActiveFilters({ ...defaults, eventId: 'e-1' }), true);
      assert.equal(hasActiveFilters({ ...defaults, ticketTypeId: 't-1' }), true);
      assert.equal(hasActiveFilters({ ...defaults, currency: 'USD' }), true);
      assert.equal(hasActiveFilters({ ...defaults, organizerId: 'org-1' }, 'Administrator'), true);
      assert.equal(hasActiveFilters({ ...defaults, organizerId: 'org-1' }, 'Organizer'), false);
    });

    it('builds active filter chips with callbacks and display names', () => {
      const removedFields = [];
      const onRemove = (field) => removedFields.push(field);

      const filters = {
        from: '2026-10-01',
        to: '2026-10-10',
        activePreset: 'custom',
        category: 'Music',
        eventId: 'evt-100',
        ticketTypeId: 'tkt-200',
        bookingStatus: 'Confirmed',
        currency: 'USD',
        organizerId: 'org-300',
      };

      const eventLabels = { 'evt-100': 'Rock Fest 2026' };
      const ticketTypeLabels = { 'tkt-200': 'VIP Pass' };

      const chips = getActiveFilterChips(filters, {
        role: 'Administrator',
        onRemove,
        eventLabels,
        ticketTypeLabels,
      });

      assert.equal(chips.length, 7);
      assert.ok(chips.some((c) => c.label.includes('Period:')));
      assert.ok(chips.some((c) => c.label === 'Category: Music'));
      assert.ok(chips.some((c) => c.label === 'Event: Rock Fest 2026'));
      assert.ok(chips.some((c) => c.label === 'Ticket Tier: VIP Pass'));
      assert.ok(chips.some((c) => c.label === 'Status: Confirmed'));
      assert.ok(chips.some((c) => c.label === 'Currency: USD'));
      assert.ok(chips.some((c) => c.label === 'Organizer: org-300'));

      // Test callbacks
      chips.find((c) => c.id === 'category').onRemove();
      assert.ok(removedFields.includes('category'));

      chips.find((c) => c.id === 'eventId').onRemove();
      assert.ok(removedFields.includes('eventId'));
    });

    it('omits organizer chip for Organizer role', () => {
      const filters = {
        category: 'Conference',
        organizerId: 'org-300',
      };

      const chips = getActiveFilterChips(filters, {
        role: 'Organizer',
        onRemove: () => {},
      });

      assert.equal(chips.length, 1);
      assert.equal(chips[0].id, 'category');
    });
  });
});
