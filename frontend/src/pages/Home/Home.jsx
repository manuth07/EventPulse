import React, { useState, useEffect, useRef, useCallback } from 'react';
import { Header } from '../../components/Header/Header';
import { Hero } from '../../components/Hero/Hero';
import { EventCard } from '../../components/EventCard/EventCard';
import { EventSkeleton } from '../../components/EventSkeleton/EventSkeleton';
import { EmptyState } from '../../components/EmptyState/EmptyState';
import { ErrorState } from '../../components/ErrorState/ErrorState';
import { fetchPublishedEvents } from '../../services/eventService';

export function Home() {
  const [events, setEvents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Search state
  const [searchInputValue, setSearchInputValue] = useState('');
  const [executedSearchQuery, setExecutedSearchQuery] = useState('');
  const [searchEvents, setSearchEvents] = useState([]);
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchError, setSearchError] = useState(null);

  const searchAbortControllerRef = useRef(null);

  const loadEvents = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchPublishedEvents();
      setEvents(Array.isArray(data) ? data : (data.value || []));
    } catch (err) {
      console.error('Error fetching events:', err);
      setError(err.message || 'Failed to fetch events');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadEvents();
    return () => {
      if (searchAbortControllerRef.current) {
        searchAbortControllerRef.current.abort();
      }
    };
  }, []);

  const handleSearchSubmit = useCallback((query) => {
    const trimmed = (query || '').trim();
    if (!trimmed) {
      handleClearSearch();
      return;
    }

    if (searchAbortControllerRef.current) {
      searchAbortControllerRef.current.abort();
    }
    const controller = new AbortController();
    searchAbortControllerRef.current = controller;

    setExecutedSearchQuery(trimmed);
    setSearchLoading(true);
    setSearchError(null);

    fetchPublishedEvents({ search: trimmed }, { signal: controller.signal })
      .then((data) => {
        setSearchEvents(Array.isArray(data) ? data : (data.value || []));
      })
      .catch((err) => {
        if (err.name !== 'AbortError') {
          console.error('Search error:', err);
          setSearchError(err.message || 'Failed to search events');
          setSearchEvents([]);
        }
      })
      .finally(() => {
        setSearchLoading(false);
      });
  }, []);

  const handleClearSearch = useCallback(() => {
    if (searchAbortControllerRef.current) {
      searchAbortControllerRef.current.abort();
    }
    setSearchInputValue('');
    setExecutedSearchQuery('');
    setSearchEvents([]);
    setSearchError(null);
  }, []);

  const isSearchActive = Boolean(executedSearchQuery);
  const displayEvents = isSearchActive ? searchEvents : events;
  const currentLoading = isSearchActive ? searchLoading : loading;
  const currentError = isSearchActive ? searchError : error;

  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column', backgroundColor: 'var(--ep-canvas)' }}>
      <Header />

      <main style={{ flex: 1, paddingBottom: '64px' }}>
        <Hero
          searchQuery={searchInputValue}
          onSearchChange={setSearchInputValue}
          onSearchSubmit={handleSearchSubmit}
        />

        <div className="container" style={{ paddingLeft: '16px', paddingRight: '16px' }}>
          {/* Section Header */}
          <div style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            marginBottom: '24px',
            flexWrap: 'wrap',
            gap: '12px',
          }}>
            <div>
              <h2 className="ep-h2">
                {isSearchActive ? `Search results for "${executedSearchQuery}"` : 'Upcoming Events'}
              </h2>
              <p className="ep-body" style={{ margin: 0 }}>
                {currentLoading
                  ? 'Fetching available experiences...'
                  : `${displayEvents.length} ${displayEvents.length === 1 ? 'event' : 'events'} ${isSearchActive ? 'found' : 'available'}`}
              </p>
            </div>

            {isSearchActive && (
              <button
                type="button"
                className="ep-btn-secondary"
                style={{ fontSize: '13px', padding: '6px 14px' }}
                onClick={handleClearSearch}
              >
                Clear Search
              </button>
            )}
          </div>

          {/* Main Grid / States */}
          {currentLoading && (
            <div className="row g-4">
              {[1, 2, 3, 4, 5, 6].map((key) => (
                <div key={key} className="col-12 col-md-6 col-lg-4">
                  <EventSkeleton />
                </div>
              ))}
            </div>
          )}

          {!currentLoading && currentError && (
            <ErrorState
              message={currentError}
              onRetry={isSearchActive ? () => handleSearchSubmit(executedSearchQuery) : loadEvents}
            />
          )}

          {!currentLoading && !currentError && !isSearchActive && displayEvents.length === 0 && (
            <EmptyState />
          )}

          {!currentLoading && !currentError && isSearchActive && displayEvents.length === 0 && (
            <EmptyState
              isSearchResults={true}
              searchQuery={executedSearchQuery}
              onResetSearch={handleClearSearch}
            />
          )}

          {!currentLoading && !currentError && displayEvents.length > 0 && (
            <div className="row g-4">
              {displayEvents.map((event) => (
                <div key={event.id} className="col-12 col-md-6 col-lg-4">
                  <EventCard event={event} />
                </div>
              ))}
            </div>
          )}
        </div>
      </main>

      {/* Simple Footer */}
      <footer style={{
        backgroundColor: '#ffffff',
        borderTop: '1px solid var(--ep-border)',
        padding: '24px 0',
        textAlign: 'center',
        color: 'var(--ep-text-secondary)',
        fontSize: '13px',
      }}>
        <div className="container">
          <p style={{ margin: 0 }}>© 2026 EventPulse. All rights reserved.</p>
        </div>
      </footer>
    </div>
  );
}
