import React from 'react';
import { Search, CalendarX } from 'lucide-react';

export function EmptyState({
  isSearchResults = false,
  searchQuery = '',
  hasActiveFilters = false,
  onResetSearch,
  onClearFilters,
}) {
  const getHeading = () => {
    if (isSearchResults && hasActiveFilters) {
      return searchQuery
        ? `No events found for "${searchQuery}" with these filters`
        : 'No events match this search and filter combination';
    }
    if (hasActiveFilters) {
      return 'No events match these filters';
    }
    if (isSearchResults) {
      return searchQuery ? `No events found for "${searchQuery}"` : 'No matching events found';
    }
    return 'No events available yet';
  };

  const getSubtext = () => {
    if (hasActiveFilters) {
      return 'Try changing category, venue type, or date range.';
    }
    if (isSearchResults) {
      return 'Try another event name, venue, or category.';
    }
    return 'New experiences are being prepared. Check back soon for upcoming events.';
  };

  return (
    <div style={{
      textAlign: 'center',
      padding: '56px 20px',
      backgroundColor: '#ffffff',
      borderRadius: 'var(--ep-radius-card)',
      border: '1px solid var(--ep-border)',
      maxWidth: '560px',
      margin: '0 auto'
    }}>
      <div style={{
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        marginBottom: '16px'
      }}>
        {isSearchResults || hasActiveFilters ? (
          <Search size={40} color="var(--ep-text-secondary)" />
        ) : (
          <CalendarX size={40} color="var(--ep-text-secondary)" />
        )}
      </div>

      <h3 className="ep-h3 mb-2">
        {getHeading()}
      </h3>
      <p className="ep-body mb-4" style={{ maxWidth: '420px', margin: '0 auto 24px' }}>
        {getSubtext()}
      </p>

      <div style={{ display: 'flex', justifyContent: 'center', gap: '10px', flexWrap: 'wrap' }}>
        {hasActiveFilters && onClearFilters && (
          <button
            type="button"
            className="ep-btn-secondary"
            onClick={onClearFilters}
          >
            Clear filters
          </button>
        )}

        {isSearchResults && onResetSearch && !hasActiveFilters && (
          <button
            type="button"
            className="ep-btn-secondary"
            onClick={onResetSearch}
          >
            Clear Search
          </button>
        )}
      </div>
    </div>
  );
}
