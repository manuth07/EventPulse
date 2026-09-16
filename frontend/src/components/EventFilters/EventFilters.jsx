import React from 'react';
import { X } from 'lucide-react';
import { EVENT_CATEGORIES, VENUE_TYPES } from '../../data/eventConstants';

const DATE_OPTIONS = [
  { value: '', label: 'Any Date' },
  { value: 'today', label: 'Today' },
  { value: 'this-week', label: 'This Week' },
  { value: 'this-month', label: 'This Month' },
];

export function EventFilters({
  category = '',
  venueType = '',
  date = '',
  onCategoryChange,
  onVenueTypeChange,
  onDateChange,
  onClearFilters,
}) {
  const hasActiveFilters = Boolean(category || venueType || date);

  const getDateLabel = (val) => {
    const opt = DATE_OPTIONS.find((d) => d.value === val);
    return opt ? opt.label : val;
  };

  const selectStyle = {
    padding: '8px 14px',
    fontSize: '13px',
    fontWeight: 500,
    color: 'var(--ep-text-primary, #1D1D1F)',
    backgroundColor: '#ffffff',
    border: '1px solid var(--ep-border, #E5E5EA)',
    borderRadius: 'var(--ep-radius-btn, 12px)',
    outline: 'none',
    cursor: 'pointer',
    transition: 'border-color 0.15s ease, box-shadow 0.15s ease',
    appearance: 'none',
    WebkitAppearance: 'none',
    backgroundImage: `url("data:image/svg+xml;charset=UTF-8,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='%2386868B' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'%3e%3cpolyline points='6 9 12 15 18 9'%3e%3c/polyline%3e%3c/svg%3e")`,
    backgroundRepeat: 'no-repeat',
    backgroundPosition: 'right 10px center',
    backgroundSize: '14px',
    paddingRight: '32px',
    minHeight: '38px',
  };

  const chipStyle = {
    display: 'inline-flex',
    alignItems: 'center',
    gap: '6px',
    padding: '4px 10px',
    borderRadius: 'var(--ep-radius-badge, 8px)',
    backgroundColor: 'var(--ep-soft-accent, #FFF0E6)',
    color: 'var(--ep-primary, #FF5B00)',
    fontSize: '12px',
    fontWeight: 600,
    border: '1px solid rgba(255, 91, 0, 0.2)',
  };

  const chipCloseBtnStyle = {
    background: 'none',
    border: 'none',
    padding: 0,
    margin: 0,
    cursor: 'pointer',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    color: 'inherit',
    lineHeight: 1,
  };

  return (
    <div style={{ marginBottom: '20px' }}>
      {/* Controls row */}
      <div style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        flexWrap: 'wrap',
        gap: '10px',
      }}>
        <div style={{
          display: 'flex',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '10px',
        }}>
          {/* Category Dropdown */}
          <div style={{ position: 'relative' }}>
            <select
              id="event-category-filter"
              aria-label="Filter events by category"
              value={category}
              onChange={(e) => onCategoryChange(e.target.value)}
              style={{
                ...selectStyle,
                borderColor: category ? 'var(--ep-primary, #FF5B00)' : 'var(--ep-border, #E5E5EA)',
              }}
            >
              <option value="">All Categories</option>
              {EVENT_CATEGORIES.map((cat) => (
                <option key={cat} value={cat}>
                  {cat}
                </option>
              ))}
            </select>
          </div>

          {/* Venue Type Dropdown */}
          <div style={{ position: 'relative' }}>
            <select
              id="event-venue-type-filter"
              aria-label="Filter events by venue type"
              value={venueType}
              onChange={(e) => onVenueTypeChange(e.target.value)}
              style={{
                ...selectStyle,
                borderColor: venueType ? 'var(--ep-primary, #FF5B00)' : 'var(--ep-border, #E5E5EA)',
              }}
            >
              <option value="">All Venue Types</option>
              {VENUE_TYPES.map((vt) => (
                <option key={vt} value={vt}>
                  {vt}
                </option>
              ))}
            </select>
          </div>

          {/* Date Range Dropdown */}
          <div style={{ position: 'relative' }}>
            <select
              id="event-date-filter"
              aria-label="Filter events by date"
              value={date}
              onChange={(e) => onDateChange(e.target.value)}
              style={{
                ...selectStyle,
                borderColor: date ? 'var(--ep-primary, #FF5B00)' : 'var(--ep-border, #E5E5EA)',
              }}
            >
              {DATE_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>
        </div>

        {/* Clear filters link */}
        {hasActiveFilters && (
          <button
            type="button"
            onClick={onClearFilters}
            className="ep-btn-secondary"
            style={{
              padding: '6px 14px',
              fontSize: '12px',
              fontWeight: 600,
              minHeight: '34px',
              color: 'var(--ep-text-secondary, #86868B)',
            }}
          >
            Clear filters
          </button>
        )}
      </div>

      {/* Active Filter Chips */}
      {hasActiveFilters && (
        <div style={{
          display: 'flex',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '8px',
          marginTop: '12px',
        }}>
          {category && (
            <span style={chipStyle}>
              {category}
              <button
                type="button"
                style={chipCloseBtnStyle}
                onClick={() => onCategoryChange('')}
                aria-label={`Remove ${category} category filter`}
              >
                <X size={13} strokeWidth={2.5} />
              </button>
            </span>
          )}

          {venueType && (
            <span style={chipStyle}>
              {venueType}
              <button
                type="button"
                style={chipCloseBtnStyle}
                onClick={() => onVenueTypeChange('')}
                aria-label={`Remove ${venueType} venue type filter`}
              >
                <X size={13} strokeWidth={2.5} />
              </button>
            </span>
          )}

          {date && (
            <span style={chipStyle}>
              {getDateLabel(date)}
              <button
                type="button"
                style={chipCloseBtnStyle}
                onClick={() => onDateChange('')}
                aria-label={`Remove ${getDateLabel(date)} date filter`}
              >
                <X size={13} strokeWidth={2.5} />
              </button>
            </span>
          )}
        </div>
      )}
    </div>
  );
}
