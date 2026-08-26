import React from 'react';
import { Search, CalendarX } from 'lucide-react';

export function EmptyState({ isSearchResults = false, onResetSearch }) {
  return (
    <div style={{
      textAlign: 'center',
      padding: '64px 20px',
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
        {isSearchResults ? (
          <Search size={40} color="var(--ep-text-secondary)" />
        ) : (
          <CalendarX size={40} color="var(--ep-text-secondary)" />
        )}
      </div>

      <h3 className="ep-h3 mb-2">
        {isSearchResults ? 'No matching events found' : 'No events available yet'}
      </h3>
      <p className="ep-body mb-4" style={{ maxWidth: '400px', margin: '0 auto 24px' }}>
        {isSearchResults
          ? 'Try tweaking your search keywords or venue name to find what you are looking for.'
          : 'New experiences are being prepared. Check back soon for upcoming events.'}
      </p>

      {isSearchResults && onResetSearch && (
        <button
          type="button"
          className="ep-btn-secondary"
          onClick={onResetSearch}
        >
          Clear Search Filter
        </button>
      )}
    </div>
  );
}
