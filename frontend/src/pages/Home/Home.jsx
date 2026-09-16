import React, { useState, useEffect, useRef, useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { Hero } from '../../components/Hero/Hero';
import { EventFilters } from '../../components/EventFilters/EventFilters';
import { EventCard } from '../../components/EventCard/EventCard';
import { EventSkeleton } from '../../components/EventSkeleton/EventSkeleton';
import { EmptyState } from '../../components/EmptyState/EmptyState';
import { ErrorState } from '../../components/ErrorState/ErrorState';
import { fetchPublishedEvents } from '../../services/eventService';

export function Home() {
  const [searchParams, setSearchParams] = useSearchParams();

  // Committed discovery criteria from URL parameters
  const searchQuery = searchParams.get('search') || '';
  const category = searchParams.get('category') || '';
  const venueType = searchParams.get('venueType') || '';
  const date = searchParams.get('date') || '';

  // Local hero input state (supports autocomplete typing without mutating grid)
  const [searchInputValue, setSearchInputValue] = useState(searchQuery);

  // Discovery results state
  const [events, setEvents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const abortControllerRef = useRef(null);

  // Sync hero input value if URL search param changes (e.g. Back/Forward button)
  useEffect(() => {
    setSearchInputValue(searchQuery);
  }, [searchQuery]);

  // Unified discovery fetch triggered by URL param changes
  useEffect(() => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }
    const controller = new AbortController();
    abortControllerRef.current = controller;

    setLoading(true);
    setError(null);

    const queryParams = {};
    if (searchQuery.trim()) queryParams.search = searchQuery.trim();
    if (category.trim()) queryParams.category = category.trim();
    if (venueType.trim()) queryParams.venueType = venueType.trim();
    if (date.trim()) queryParams.date = date.trim();

    fetchPublishedEvents(queryParams, { signal: controller.signal })
      .then((data) => {
        setEvents(Array.isArray(data) ? data : (data.value || []));
      })
      .catch((err) => {
        if (err.name !== 'AbortError') {
          console.error('Error fetching discovery events:', err);
          setError(err.message || 'Failed to load events');
          setEvents([]);
        }
      })
      .finally(() => {
        setLoading(false);
      });

    return () => {
      controller.abort();
    };
  }, [searchQuery, category, venueType, date]);

  const updateDiscoveryParams = useCallback((newParams) => {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      Object.entries(newParams).forEach(([key, val]) => {
        if (val && typeof val === 'string' && val.trim()) {
          next.set(key, val.trim());
        } else {
          next.delete(key);
        }
      });
      return next;
    });
  }, [setSearchParams]);

  const handleSearchSubmit = useCallback((query) => {
    const trimmed = (query || '').trim();
    updateDiscoveryParams({ search: trimmed });
  }, [updateDiscoveryParams]);

  const handleClearSearch = useCallback(() => {
    setSearchInputValue('');
    updateDiscoveryParams({ search: '' });
  }, [updateDiscoveryParams]);

  const handleClearFilters = useCallback(() => {
    updateDiscoveryParams({ category: '', venueType: '', date: '' });
  }, [updateDiscoveryParams]);

  const isSearchActive = Boolean(searchQuery.trim());
  const hasActiveFilters = Boolean(category || venueType || date);

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
            marginBottom: '16px',
            flexWrap: 'wrap',
            gap: '12px',
          }}>
            <div>
              <h2 className="ep-h2">
                {isSearchActive ? `Search results for "${searchQuery}"` : 'Upcoming Events'}
              </h2>
              <p className="ep-body" style={{ margin: 0 }}>
                {loading
                  ? 'Fetching available experiences...'
                  : `${events.length} ${events.length === 1 ? 'event' : 'events'} ${isSearchActive || hasActiveFilters ? 'found' : 'available'}`}
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

          {/* Filter Controls */}
          <EventFilters
            category={category}
            venueType={venueType}
            date={date}
            onCategoryChange={(val) => updateDiscoveryParams({ category: val })}
            onVenueTypeChange={(val) => updateDiscoveryParams({ venueType: val })}
            onDateChange={(val) => updateDiscoveryParams({ date: val })}
            onClearFilters={handleClearFilters}
          />

          {/* Main Grid / States */}
          {loading && (
            <div className="row g-4">
              {[1, 2, 3, 4, 5, 6].map((key) => (
                <div key={key} className="col-12 col-md-6 col-lg-4">
                  <EventSkeleton />
                </div>
              ))}
            </div>
          )}

          {!loading && error && (
            <ErrorState
              message={error}
              onRetry={() => {
                const queryParams = {};
                if (searchQuery.trim()) queryParams.search = searchQuery.trim();
                if (category.trim()) queryParams.category = category.trim();
                if (venueType.trim()) queryParams.venueType = venueType.trim();
                if (date.trim()) queryParams.date = date.trim();
                fetchPublishedEvents(queryParams).then((d) => setEvents(Array.isArray(d) ? d : (d.value || [])));
              }}
            />
          )}

          {!loading && !error && events.length === 0 && (
            <EmptyState
              isSearchResults={isSearchActive}
              searchQuery={searchQuery}
              hasActiveFilters={hasActiveFilters}
              onResetSearch={handleClearSearch}
              onClearFilters={handleClearFilters}
            />
          )}

          {!loading && !error && events.length > 0 && (
            <div className="row g-4">
              {events.map((event) => (
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
