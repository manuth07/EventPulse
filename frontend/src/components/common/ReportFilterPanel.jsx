import React, { useState } from 'react';
import {
  Filter,
  Calendar,
  X,
  RotateCcw,
  AlertCircle,
  ChevronDown,
  ChevronUp,
  Tag,
  Briefcase,
  Layers,
  DollarSign,
  Ticket,
} from 'lucide-react';
import {
  CATEGORY_OPTIONS,
  BOOKING_STATUS_OPTIONS,
  CURRENCY_OPTIONS,
  DATE_PRESET_OPTIONS,
  canFilterOrganizer,
  getDatePresetRange,
  getActiveFilterChips,
  hasActiveFilters,
} from '../../utils/reportFilterUtils.js';

/**
 * Reusable Report & Analytics Filter Panel (EP-396)
 * 
 * Supports:
 * - Date ranges with quick presets and custom Pickers
 * - Event and dependent Ticket Type selectors
 * - Category filter (canonical + normalized)
 * - Booking status & Currency filters
 * - Role-governed Organizer filter (strictly Administrator only)
 * - Active filter badges/chips with individual dismissal
 * - Client-side validation display
 */
export function ReportFilterPanel({
  role = 'Administrator',
  filters = {},
  onFilterChange,
  onApply,
  onClear,
  loading = false,
  validationError = null,
  showCategory = true,
  showBookingStatus = true,
  showCurrency = false,
  showOrganizer = undefined,
  eventsList = [],
  ticketTypesList = [],
  eventLabels = {},
  ticketTypeLabels = {},
  title = 'Filter Criteria',
  applyButtonLabel = 'Apply Filters',
  applyButtonTestId = 'report-filter-apply-btn',
  className = '',
}) {
  const [showAdvanced, setShowAdvanced] = useState(false);

  // Check if organizer filter is allowed
  const isOrganizerAllowed = showOrganizer !== undefined ? showOrganizer : canFilterOrganizer(role);

  // Safe field accessor
  const currentFilters = {
    from: filters.from || '',
    to: filters.to || '',
    activePreset: filters.activePreset || 'allTime',
    eventId: filters.eventId || '',
    ticketTypeId: filters.ticketTypeId || '',
    category: filters.category || 'All',
    bookingStatus: filters.bookingStatus || 'All',
    currency: filters.currency || 'ALL',
    organizerId: filters.organizerId || '',
  };

  const hasEventSelected = Boolean(
    currentFilters.eventId &&
    currentFilters.eventId !== 'ALL' &&
    currentFilters.eventId.trim()
  );

  // Handler for preset clicks
  const handlePresetClick = (presetKey) => {
    const range = getDatePresetRange(presetKey);
    onFilterChange({
      ...currentFilters,
      activePreset: presetKey,
      from: range.from,
      to: range.to,
    });
  };

  // Handler for individual field changes
  const handleChange = (field, value) => {
    const next = { ...currentFilters, [field]: value };

    // If event is cleared or set to ALL, also clear dependent ticketTypeId
    if (field === 'eventId' && (!value || value === 'ALL')) {
      next.ticketTypeId = '';
    }

    // If custom date changed, mark preset as custom
    if (field === 'from' || field === 'to') {
      next.activePreset = 'custom';
    }

    onFilterChange(next);
  };

  // Handler for individual active chip removal
  const handleRemoveChip = (chipId) => {
    const next = { ...currentFilters };
    switch (chipId) {
      case 'dateRange':
        next.from = '';
        next.to = '';
        next.activePreset = 'allTime';
        break;
      case 'category':
        next.category = 'All';
        break;
      case 'eventId':
        next.eventId = '';
        next.ticketTypeId = '';
        break;
      case 'ticketTypeId':
        next.ticketTypeId = '';
        break;
      case 'bookingStatus':
        next.bookingStatus = 'All';
        break;
      case 'currency':
        next.currency = 'ALL';
        break;
      case 'organizerId':
        next.organizerId = '';
        break;
      default:
        break;
    }
    onFilterChange(next);
  };

  // Build active filter chips
  const activeChips = getActiveFilterChips(currentFilters, {
    role,
    onRemove: handleRemoveChip,
    eventLabels,
    ticketTypeLabels,
  });

  const isFilterActive = hasActiveFilters(currentFilters, role);

  return (
    <section
      aria-label={title}
      className={`ep-filter-panel ${className}`}
      style={{
        backgroundColor: '#ffffff',
        borderRadius: 'var(--ep-radius-card, 16px)',
        border: '1px solid var(--ep-border, #E5E5EA)',
        padding: '20px 24px',
        marginBottom: '24px',
        boxShadow: 'var(--ep-shadow-card, 0 2px 8px rgba(0,0,0,0.04))',
      }}
    >
      {/* Header Row */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: '12px',
          marginBottom: '16px',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <div
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              width: '32px',
              height: '32px',
              borderRadius: '8px',
              backgroundColor: 'rgba(255, 90, 39, 0.08)',
              color: 'var(--ep-primary, #FF5A27)',
            }}
          >
            <Filter size={18} />
          </div>
          <h2
            style={{
              fontSize: '16px',
              fontWeight: 700,
              color: 'var(--ep-text-primary, #1D1D1F)',
              margin: 0,
            }}
          >
            {title}
          </h2>
        </div>

        {/* Clear Filters Button */}
        {isFilterActive && (
          <button
            type="button"
            data-testid="report-filter-clear-btn"
            onClick={onClear}
            disabled={loading}
            style={{
              background: 'none',
              border: 'none',
              color: 'var(--ep-text-secondary, #86868B)',
              fontSize: '13px',
              fontWeight: 600,
              display: 'inline-flex',
              alignItems: 'center',
              gap: '6px',
              cursor: 'pointer',
              padding: '4px 8px',
              borderRadius: '6px',
              transition: 'color 0.15s ease',
            }}
            onMouseEnter={(e) => (e.currentTarget.style.color = 'var(--ep-primary, #FF5A27)')}
            onMouseLeave={(e) => (e.currentTarget.style.color = 'var(--ep-text-secondary, #86868B)')}
          >
            <RotateCcw size={14} />
            <span>Reset All Filters</span>
          </button>
        )}
      </div>

      {/* Date Range Presets & Inputs Row */}
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          gap: '14px',
          marginBottom: '16px',
        }}
      >
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            flexWrap: 'wrap',
            gap: '12px',
          }}
        >
          {/* Preset Buttons */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '6px', flexWrap: 'wrap' }}>
            <span
              style={{
                fontSize: '12px',
                fontWeight: 600,
                color: 'var(--ep-text-secondary, #86868B)',
                display: 'inline-flex',
                alignItems: 'center',
                gap: '4px',
                marginRight: '4px',
              }}
            >
              <Calendar size={13} />
              <span>Period:</span>
            </span>

            {DATE_PRESET_OPTIONS.map((preset) => {
              const isSelected = currentFilters.activePreset === preset.key;
              return (
                <button
                  key={preset.key}
                  type="button"
                  data-testid={`filter-preset-${preset.key}`}
                  onClick={() => handlePresetClick(preset.key)}
                  style={{
                    padding: '5px 12px',
                    fontSize: '12px',
                    fontWeight: isSelected ? 600 : 500,
                    borderRadius: 'var(--ep-radius-pill, 9999px)',
                    border: isSelected
                      ? '1px solid var(--ep-primary, #FF5A27)'
                      : '1px solid var(--ep-border, #E5E5EA)',
                    backgroundColor: isSelected ? 'var(--ep-primary, #FF5A27)' : '#ffffff',
                    color: isSelected ? '#ffffff' : 'var(--ep-text-secondary, #86868B)',
                    cursor: 'pointer',
                    transition: 'all 0.15s ease',
                  }}
                >
                  {preset.label}
                </button>
              );
            })}
          </div>

          {/* Custom Date Pickers */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <label
                htmlFor="filter-date-from"
                style={{ fontSize: '12px', fontWeight: 500, color: 'var(--ep-text-secondary, #86868B)' }}
              >
                From:
              </label>
              <input
                id="filter-date-from"
                type="date"
                data-testid="filter-date-from"
                value={currentFilters.from}
                onChange={(e) => handleChange('from', e.target.value)}
                style={{
                  padding: '6px 10px',
                  fontSize: '12px',
                  borderRadius: 'var(--ep-radius-btn, 8px)',
                  border: '1px solid var(--ep-border, #E5E5EA)',
                  color: 'var(--ep-text-primary, #1D1D1F)',
                  backgroundColor: '#ffffff',
                }}
              />
            </div>

            <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <label
                htmlFor="filter-date-to"
                style={{ fontSize: '12px', fontWeight: 500, color: 'var(--ep-text-secondary, #86868B)' }}
              >
                To:
              </label>
              <input
                id="filter-date-to"
                type="date"
                data-testid="filter-date-to"
                value={currentFilters.to}
                onChange={(e) => handleChange('to', e.target.value)}
                style={{
                  padding: '6px 10px',
                  fontSize: '12px',
                  borderRadius: 'var(--ep-radius-btn, 8px)',
                  border: '1px solid var(--ep-border, #E5E5EA)',
                  color: 'var(--ep-text-primary, #1D1D1F)',
                  backgroundColor: '#ffffff',
                }}
              />
            </div>
          </div>
        </div>
      </div>

      {/* Main Filter Controls Grid */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
          gap: '12px',
          padding: '16px',
          backgroundColor: 'var(--ep-canvas, #F5F5F7)',
          borderRadius: 'var(--ep-radius-btn, 12px)',
          marginBottom: '16px',
        }}
      >
        {/* Category Filter */}
        {showCategory && (
          <div>
            <label
              htmlFor="filter-category"
              style={{
                display: 'block',
                fontSize: '12px',
                fontWeight: 600,
                color: 'var(--ep-text-primary, #1D1D1F)',
                marginBottom: '6px',
              }}
            >
              Category:
            </label>
            <select
              id="filter-category"
              data-testid="filter-category"
              value={currentFilters.category}
              onChange={(e) => handleChange('category', e.target.value)}
              style={{
                width: '100%',
                padding: '7px 12px',
                fontSize: '13px',
                borderRadius: '8px',
                border: '1px solid var(--ep-border, #E5E5EA)',
                backgroundColor: '#ffffff',
                color: 'var(--ep-text-primary, #1D1D1F)',
              }}
            >
              {CATEGORY_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>
        )}

        {/* Event Selector */}
        <div>
          <label
            htmlFor="filter-event-id"
            style={{
              display: 'block',
              fontSize: '12px',
              fontWeight: 600,
              color: 'var(--ep-text-primary, #1D1D1F)',
              marginBottom: '6px',
            }}
          >
            Event:
          </label>
          {eventsList && eventsList.length > 0 ? (
            <select
              id="filter-event-id"
              data-testid="filter-event-id"
              value={currentFilters.eventId || 'ALL'}
              onChange={(e) => handleChange('eventId', e.target.value === 'ALL' ? '' : e.target.value)}
              style={{
                width: '100%',
                padding: '7px 12px',
                fontSize: '13px',
                borderRadius: '8px',
                border: '1px solid var(--ep-border, #E5E5EA)',
                backgroundColor: '#ffffff',
                color: 'var(--ep-text-primary, #1D1D1F)',
              }}
            >
              <option value="ALL">All Events</option>
              {eventsList.map((evt) => (
                <option key={evt.id || evt.eventId} value={evt.id || evt.eventId}>
                  {evt.title || evt.name || evt.id}
                </option>
              ))}
            </select>
          ) : (
            <input
              id="filter-event-id"
              type="text"
              data-testid="filter-event-id"
              placeholder="e.g. Event UUID or leave blank"
              value={currentFilters.eventId}
              onChange={(e) => handleChange('eventId', e.target.value)}
              style={{
                width: '100%',
                padding: '7px 12px',
                fontSize: '12px',
                borderRadius: '8px',
                border: '1px solid var(--ep-border, #E5E5EA)',
                backgroundColor: '#ffffff',
                color: 'var(--ep-text-primary, #1D1D1F)',
              }}
            />
          )}
        </div>

        {/* Ticket Type Selector (Dependent on Event Selection) */}
        <div>
          <label
            htmlFor="filter-ticket-type-id"
            style={{
              display: 'block',
              fontSize: '12px',
              fontWeight: 600,
              color: hasEventSelected ? 'var(--ep-text-primary, #1D1D1F)' : 'var(--ep-text-tertiary, #999)',
              marginBottom: '6px',
            }}
          >
            Ticket Tier:
          </label>
          {ticketTypesList && ticketTypesList.length > 0 ? (
            <select
              id="filter-ticket-type-id"
              data-testid="filter-ticket-type-id"
              disabled={!hasEventSelected}
              value={currentFilters.ticketTypeId || 'ALL'}
              onChange={(e) => handleChange('ticketTypeId', e.target.value === 'ALL' ? '' : e.target.value)}
              title={!hasEventSelected ? 'Please select an event first' : 'Filter by ticket tier'}
              style={{
                width: '100%',
                padding: '7px 12px',
                fontSize: '13px',
                borderRadius: '8px',
                border: '1px solid var(--ep-border, #E5E5EA)',
                backgroundColor: hasEventSelected ? '#ffffff' : '#f0f0f2',
                color: hasEventSelected ? 'var(--ep-text-primary, #1D1D1F)' : 'var(--ep-text-secondary, #86868B)',
                cursor: hasEventSelected ? 'pointer' : 'not-allowed',
                opacity: hasEventSelected ? 1 : 0.6,
              }}
            >
              <option value="ALL">All Ticket Tiers</option>
              {ticketTypesList.map((tier) => (
                <option key={tier.id || tier.ticketTypeId} value={tier.id || tier.ticketTypeId}>
                  {tier.name || tier.tierName || tier.id}
                </option>
              ))}
            </select>
          ) : (
            <input
              id="filter-ticket-type-id"
              type="text"
              data-testid="filter-ticket-type-id"
              disabled={!hasEventSelected}
              placeholder={hasEventSelected ? 'Ticket Type UUID' : 'Select event first'}
              value={currentFilters.ticketTypeId}
              onChange={(e) => handleChange('ticketTypeId', e.target.value)}
              title={!hasEventSelected ? 'Please select an event first' : 'Filter by ticket tier UUID'}
              style={{
                width: '100%',
                padding: '7px 12px',
                fontSize: '12px',
                borderRadius: '8px',
                border: '1px solid var(--ep-border, #E5E5EA)',
                backgroundColor: hasEventSelected ? '#ffffff' : '#f0f0f2',
                color: hasEventSelected ? 'var(--ep-text-primary, #1D1D1F)' : 'var(--ep-text-secondary, #86868B)',
                cursor: hasEventSelected ? 'text' : 'not-allowed',
                opacity: hasEventSelected ? 1 : 0.6,
              }}
            />
          )}
        </div>

        {/* Booking Status Selector */}
        {showBookingStatus && (
          <div>
            <label
              htmlFor="filter-booking-status"
              style={{
                display: 'block',
                fontSize: '12px',
                fontWeight: 600,
                color: 'var(--ep-text-primary, #1D1D1F)',
                marginBottom: '6px',
              }}
            >
              Booking Status:
            </label>
            <select
              id="filter-booking-status"
              data-testid="filter-booking-status"
              value={currentFilters.bookingStatus}
              onChange={(e) => handleChange('bookingStatus', e.target.value)}
              style={{
                width: '100%',
                padding: '7px 12px',
                fontSize: '13px',
                borderRadius: '8px',
                border: '1px solid var(--ep-border, #E5E5EA)',
                backgroundColor: '#ffffff',
                color: 'var(--ep-text-primary, #1D1D1F)',
              }}
            >
              {BOOKING_STATUS_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>
        )}

        {/* Currency Selector */}
        {showCurrency && (
          <div>
            <label
              htmlFor="filter-currency"
              style={{
                display: 'block',
                fontSize: '12px',
                fontWeight: 600,
                color: 'var(--ep-text-primary, #1D1D1F)',
                marginBottom: '6px',
              }}
            >
              Currency:
            </label>
            <select
              id="filter-currency"
              data-testid="filter-currency"
              value={currentFilters.currency}
              onChange={(e) => handleChange('currency', e.target.value)}
              style={{
                width: '100%',
                padding: '7px 12px',
                fontSize: '13px',
                borderRadius: '8px',
                border: '1px solid var(--ep-border, #E5E5EA)',
                backgroundColor: '#ffffff',
                color: 'var(--ep-text-primary, #1D1D1F)',
              }}
            >
              {CURRENCY_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>
        )}
      </div>

      {/* Advanced Drawer: Organizer Filter (Strictly Administrator Only) */}
      {isOrganizerAllowed && (
        <div style={{ marginBottom: '16px' }}>
          <button
            type="button"
            data-testid="toggle-advanced-filters-btn"
            onClick={() => setShowAdvanced(!showAdvanced)}
            style={{
              background: 'none',
              border: 'none',
              color: 'var(--ep-text-secondary, #86868B)',
              fontSize: '12px',
              fontWeight: 600,
              display: 'inline-flex',
              alignItems: 'center',
              gap: '4px',
              cursor: 'pointer',
              padding: 0,
            }}
          >
            <span>Platform Administrator Scoping (Organizer ID)</span>
            {showAdvanced ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
          </button>

          {showAdvanced && (
            <div
              style={{
                marginTop: '10px',
                padding: '12px 16px',
                backgroundColor: '#ffffff',
                borderRadius: '8px',
                border: '1px solid var(--ep-border, #E5E5EA)',
              }}
            >
              <label
                htmlFor="filter-organizer-id"
                style={{
                  display: 'block',
                  fontSize: '12px',
                  fontWeight: 600,
                  color: 'var(--ep-text-secondary, #86868B)',
                  marginBottom: '4px',
                }}
              >
                Organizer ID (Optional UUID):
              </label>
              <input
                id="filter-organizer-id"
                type="text"
                data-testid="filter-organizer-id"
                placeholder="e.g. 00000000-0000-0000-0000-000000000000"
                value={currentFilters.organizerId}
                onChange={(e) => handleChange('organizerId', e.target.value)}
                style={{
                  width: '100%',
                  maxWidth: '380px',
                  padding: '7px 10px',
                  fontSize: '12px',
                  borderRadius: '6px',
                  border: '1px solid var(--ep-border, #E5E5EA)',
                  color: 'var(--ep-text-primary, #1D1D1F)',
                }}
              />
            </div>
          )}
        </div>
      )}

      {/* Validation Error Banner */}
      {validationError && (
        <div
          role="alert"
          data-testid="filter-validation-alert"
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            padding: '10px 14px',
            backgroundColor: '#fef2f2',
            border: '1px solid #fecaca',
            borderRadius: '8px',
            color: '#991b1b',
            fontSize: '13px',
            marginBottom: '16px',
          }}
        >
          <AlertCircle size={16} />
          <span>{validationError}</span>
        </div>
      )}

      {/* Action Row */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: '12px',
          borderTop: '1px solid var(--ep-border, #E5E5EA)',
          paddingTop: '16px',
        }}
      >
        {/* Applied Filter Chips */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap', flex: 1 }}>
          {activeChips.length > 0 ? (
            activeChips.map((chip) => (
              <span
                key={chip.id}
                data-testid={`active-filter-chip-${chip.id}`}
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '6px',
                  padding: '4px 10px',
                  borderRadius: 'var(--ep-radius-pill, 9999px)',
                  backgroundColor: 'rgba(255, 90, 39, 0.08)',
                  color: 'var(--ep-primary, #FF5A27)',
                  fontSize: '12px',
                  fontWeight: 600,
                  border: '1px solid rgba(255, 90, 39, 0.2)',
                }}
              >
                <span>{chip.label}</span>
                <button
                  type="button"
                  data-testid={`remove-filter-chip-${chip.id}`}
                  onClick={chip.onRemove}
                  aria-label={`Remove filter: ${chip.label}`}
                  style={{
                    background: 'none',
                    border: 'none',
                    padding: 0,
                    margin: 0,
                    color: 'inherit',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}
                >
                  <X size={13} />
                </button>
              </span>
            ))
          ) : (
            <span style={{ fontSize: '12px', color: 'var(--ep-text-tertiary, #86868B)' }}>
              Showing baseline results without extra filters.
            </span>
          )}
        </div>

        {/* Apply Filters Button */}
        <button
          type="button"
          data-testid={applyButtonTestId}
          onClick={onApply}
          disabled={loading}
          className="ep-btn-primary"
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: '8px',
            padding: '9px 22px',
            fontSize: '13px',
            fontWeight: 600,
            cursor: loading ? 'not-allowed' : 'pointer',
            opacity: loading ? 0.7 : 1,
            borderRadius: 'var(--ep-radius-btn, 8px)',
          }}
        >
          <Filter size={15} />
          <span>{loading ? 'Applying Filters...' : applyButtonLabel}</span>
        </button>
      </div>
    </section>
  );
}
