import React, { useState, useEffect, useMemo } from 'react';
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
  const [searchQuery, setSearchQuery] = useState('');

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
  }, []);

  const filteredEvents = useMemo(() => {
    if (!searchQuery.trim()) return events;
    const q = searchQuery.toLowerCase().trim();
    return events.filter((e) =>
      (e.title && e.title.toLowerCase().includes(q)) ||
      (e.venue && e.venue.toLowerCase().includes(q)) ||
      (e.description && e.description.toLowerCase().includes(q))
    );
  }, [events, searchQuery]);

  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column', backgroundColor: 'var(--ep-canvas)' }}>
      <Header />

      <main style={{ flex: 1, paddingBottom: '64px' }}>
        <Hero
          searchQuery={searchQuery}
          onSearchChange={setSearchQuery}
        />

        <div className="container" style={{ paddingLeft: '16px', paddingRight: '16px' }}>
          {/* Section Header */}
          <div style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            marginBottom: '24px'
          }}>
            <div>
              <h2 className="ep-h2">Upcoming Events</h2>
              <p className="ep-body" style={{ margin: 0 }}>
                {loading
                  ? 'Fetching available experiences...'
                  : `${filteredEvents.length} ${filteredEvents.length === 1 ? 'event' : 'events'} available`}
              </p>
            </div>

            {searchQuery && (
              <button
                type="button"
                className="ep-btn-secondary"
                style={{ fontSize: '13px', padding: '6px 12px' }}
                onClick={() => setSearchQuery('')}
              >
                Clear Search
              </button>
            )}
          </div>

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
            <ErrorState message={error} onRetry={loadEvents} />
          )}

          {!loading && !error && events.length === 0 && (
            <EmptyState />
          )}

          {!loading && !error && events.length > 0 && filteredEvents.length === 0 && (
            <EmptyState
              isSearchResults={true}
              onResetSearch={() => setSearchQuery('')}
            />
          )}

          {!loading && !error && filteredEvents.length > 0 && (
            <div className="row g-4">
              {filteredEvents.map((event) => (
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
        fontSize: '13px'
      }}>
        <div className="container">
          <p style={{ margin: 0 }}>© 2026 EventPulse. All rights reserved. Built with React & ASP.NET Core.</p>
        </div>
      </footer>
    </div>
  );
}
