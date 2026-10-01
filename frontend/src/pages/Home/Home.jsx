import React, { useState, useEffect, useRef, useCallback } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import { Layout } from '../../components/layout/Layout';
import { Hero } from '../../components/Hero/Hero';
import { EventFilters } from '../../components/EventFilters/EventFilters';
import { EventCard } from '../../components/EventCard/EventCard';
import { EventSkeleton } from '../../components/EventSkeleton/EventSkeleton';
import { EmptyState } from '../../components/EmptyState/EmptyState';
import { ErrorState } from '../../components/ErrorState/ErrorState';
import { fetchPublishedEvents } from '../../services/eventService';
import { ChevronLeft, ChevronRight, Calendar } from 'lucide-react';

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

  // View mode and pagination state
  const [viewMode, setViewMode] = useState('grid');
  const [pageSize, setPageSize] = useState(6);
  const [currentPage, setCurrentPage] = useState(1);

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

  // Reset pagination when search or filters change
  useEffect(() => {
    setCurrentPage(1);
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

  // Pagination calculations
  const totalEvents = events.length;
  const totalPages = Math.ceil(totalEvents / pageSize) || 1;
  const safeCurrentPage = Math.min(currentPage, totalPages);
  const startIndex = (safeCurrentPage - 1) * pageSize;
  const paginatedEvents = events.slice(startIndex, startIndex + pageSize);

  return (
    <Layout style={{ backgroundColor: 'var(--ep-canvas)' }} mainStyle={{ paddingBottom: '64px' }}>
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
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap', marginTop: '4px' }}>
              <p className="ep-body" style={{ margin: 0 }}>
                {loading
                  ? 'Fetching available experiences...'
                  : `${events.length} ${events.length === 1 ? 'event' : 'events'} ${isSearchActive || hasActiveFilters ? 'found' : 'available'}`}
              </p>

              {/* Events per page selector next to the events counter */}
              {!loading && events.length > 0 && (
                <div
                  className="inline-flex items-center gap-1.5 text-xs text-slate-500"
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '6px',
                    fontSize: '12px',
                    color: '#64748b',
                  }}
                >
                  <span style={{ color: '#cbd5e1' }}>•</span>
                  <label htmlFor="events-per-page-select" style={{ fontWeight: 500, color: '#475569' }}>
                    Show:
                  </label>
                  <select
                    id="events-per-page-select"
                    aria-label="Events per page"
                    value={pageSize}
                    onChange={(e) => {
                      setPageSize(Number(e.target.value));
                      setCurrentPage(1);
                    }}
                    style={{
                      backgroundColor: '#ffffff',
                      border: '1px solid #e2e8f0',
                      color: '#334155',
                      fontSize: '12px',
                      fontWeight: 600,
                      borderRadius: '6px',
                      padding: '3px 8px',
                      cursor: 'pointer',
                      outline: 'none',
                    }}
                  >
                    <option value={6}>6 per page</option>
                    <option value={12}>12 per page</option>
                    <option value={24}>24 per page</option>
                  </select>
                </div>
              )}
            </div>
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

        {/* Filter Controls with Grid/List Toggle */}
        <EventFilters
          category={category}
          venueType={venueType}
          date={date}
          onCategoryChange={(val) => updateDiscoveryParams({ category: val })}
          onVenueTypeChange={(val) => updateDiscoveryParams({ venueType: val })}
          onDateChange={(val) => updateDiscoveryParams({ date: val })}
          onClearFilters={handleClearFilters}
          viewMode={viewMode}
          onViewModeChange={setViewMode}
        />

        {/* Loading Skeletons */}
        {loading && (
          viewMode === 'list' ? (
            <div className="w-full max-w-4xl mx-auto divide-y divide-slate-100 bg-white rounded-xl border border-slate-200/80 shadow-sm overflow-hidden animate-pulse">
              {[1, 2, 3, 4, 5, 6].map((key) => (
                <div key={key} className="flex items-center gap-3.5 sm:gap-4 p-3">
                  <div className="w-14 h-14 sm:w-16 sm:h-16 rounded-md bg-slate-200 flex-shrink-0" />
                  <div className="flex-1 min-w-0 pr-2 space-y-1.5">
                    <div className="h-4 bg-slate-200 rounded w-2/3" />
                    <div className="h-3 bg-slate-200 rounded w-1/3" />
                  </div>
                  <div className="flex items-center gap-3 flex-shrink-0">
                    <div className="w-16 h-3 bg-slate-200 rounded hidden sm:block" />
                    <div className="w-8 h-8 rounded-md bg-slate-200" />
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div className="row g-4">
              {[1, 2, 3, 4, 5, 6].map((key) => (
                <div key={key} className="col-12 col-md-6 col-lg-4">
                  <EventSkeleton />
                </div>
              ))}
            </div>
          )
        )}

        {/* Error State */}
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

        {/* Empty State */}
        {!loading && !error && events.length === 0 && (
          <EmptyState
            isSearchResults={isSearchActive}
            searchQuery={searchQuery}
            hasActiveFilters={hasActiveFilters}
            onResetSearch={handleClearSearch}
            onClearFilters={handleClearFilters}
          />
        )}

        {/* Render Events (Grid or List View) with Pagination */}
        {!loading && !error && events.length > 0 && (
          <>
            {/* Leave existing Grid View code 100% untouched */}
            {viewMode === 'grid' ? (
              <div className="row g-4">
                {paginatedEvents.map((event) => (
                  <div key={event.id} className="col-12 col-md-6 col-lg-4">
                    <EventCard event={event} />
                  </div>
                ))}
              </div>
            ) : (
              <div className="w-full max-w-4xl mx-auto divide-y divide-slate-100 bg-white rounded-xl border border-slate-200/80 shadow-sm overflow-hidden">
                {paginatedEvents.map((event) => (
                  <Link
                    key={event.id}
                    to={`/events/${event.id}`}
                    className="flex items-center gap-3.5 sm:gap-4 p-3 hover:bg-slate-50/80 transition-colors group text-decoration-none"
                    style={{ textDecoration: 'none' }}
                  >
                    {/* 1. COMPACT SQUARE THUMBNAIL */}
                    <div className="w-14 h-14 sm:w-16 sm:h-16 rounded-md overflow-hidden flex-shrink-0 bg-slate-100 border border-slate-200/70">
                      <img
                        src={event.imageUrl || event.imagePath || event.coverUrl || '/fallback-event-cover.jpg'}
                        alt={event.title}
                        onError={(e) => {
                          e.currentTarget.style.display = 'none';
                        }}
                        className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                      />
                    </div>

                    {/* 2. TITLE & DATE/TIME */}
                    <div className="flex-1 min-w-0 pr-2">
                      <h4 className="text-sm sm:text-base font-semibold text-slate-900 group-hover:text-orange-600 transition-colors truncate m-0">
                        {event.title}
                      </h4>
                      <p className="text-xs text-slate-500 mt-0.5 flex items-center gap-1.5 font-normal m-0">
                        {event.eventDate ? (
                          <>
                            <span>
                              {new Date(event.eventDate).toLocaleDateString('en-US', {
                                month: 'short',
                                day: 'numeric',
                              })}
                            </span>
                            <span>•</span>
                            <span>
                              {new Date(event.eventDate).toLocaleTimeString('en-US', {
                                hour: '2-digit',
                                minute: '2-digit',
                                hour12: false,
                              })}
                            </span>
                          </>
                        ) : (
                          <span>Date TBA</span>
                        )}
                        {event.venue && (
                          <>
                            <span className="hidden sm:inline">•</span>
                            <span className="hidden sm:inline truncate text-slate-400">
                              {event.venue}
                            </span>
                          </>
                        )}
                      </p>
                    </div>

                    {/* 3. RIGHT-SIDE CALENDAR/TICKET ACTION */}
                    <div className="flex items-center gap-3 flex-shrink-0">
                      {event.price !== undefined && (
                        <span className="text-xs font-semibold text-slate-700 hidden sm:block">
                          LKR {Number(event.price).toLocaleString()}
                        </span>
                      )}
                      <div className="w-8 h-8 rounded-md border border-slate-200 flex items-center justify-center text-slate-500 group-hover:text-orange-600 group-hover:border-orange-300 transition-colors bg-white">
                        <Calendar className="w-4 h-4" />
                      </div>
                    </div>
                  </Link>
                ))}
              </div>
            )}

            {/* Pagination Controls */}
            {totalPages > 1 && (
              <div
                className={`mt-10 flex flex-col sm:flex-row items-center justify-between gap-4 pt-6 border-t border-slate-200 ${
                  viewMode === 'list' ? 'max-w-4xl mx-auto w-full' : ''
                }`}
                style={{
                  marginTop: '2.5rem',
                  paddingTop: '1.5rem',
                  borderTop: '1px solid #e2e8f0',
                  display: 'flex',
                  flexWrap: 'wrap',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  gap: '12px',
                }}
              >
                <p className="text-xs text-slate-500 m-0" style={{ margin: 0, fontSize: '13px', color: '#64748b' }}>
                  Showing <span className="font-semibold text-slate-700">{startIndex + 1}</span> to{' '}
                  <span className="font-semibold text-slate-700">
                    {Math.min(startIndex + pageSize, totalEvents)}
                  </span>{' '}
                  of <span className="font-semibold text-slate-700">{totalEvents}</span> events
                </p>

                <div className="inline-flex items-center gap-1.5" style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
                  <button
                    type="button"
                    disabled={safeCurrentPage === 1}
                    onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
                    className="px-3 py-1.5 text-xs font-medium rounded-lg border border-slate-200 bg-white text-slate-700 hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition flex items-center gap-1"
                    style={{
                      padding: '6px 12px',
                      fontSize: '12px',
                      borderRadius: '8px',
                      border: '1px solid #e2e8f0',
                      backgroundColor: '#ffffff',
                      color: '#334155',
                      cursor: safeCurrentPage === 1 ? 'not-allowed' : 'pointer',
                      opacity: safeCurrentPage === 1 ? 0.45 : 1,
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '4px',
                    }}
                  >
                    <ChevronLeft size={14} />
                    <span>Previous</span>
                  </button>

                  {Array.from({ length: totalPages }, (_, i) => i + 1).map((pageNum) => (
                    <button
                      key={pageNum}
                      type="button"
                      onClick={() => setCurrentPage(pageNum)}
                      className={`w-8 h-8 text-xs font-semibold rounded-lg transition ${
                        safeCurrentPage === pageNum
                          ? 'bg-orange-600 text-white shadow-xs'
                          : 'bg-white border border-slate-200 text-slate-700 hover:bg-slate-50'
                      }`}
                      style={{
                        width: '32px',
                        height: '32px',
                        fontSize: '12px',
                        fontWeight: 600,
                        borderRadius: '8px',
                        border: safeCurrentPage === pageNum ? 'none' : '1px solid #e2e8f0',
                        backgroundColor: safeCurrentPage === pageNum ? '#ea580c' : '#ffffff',
                        color: safeCurrentPage === pageNum ? '#ffffff' : '#334155',
                        cursor: 'pointer',
                      }}
                    >
                      {pageNum}
                    </button>
                  ))}

                  <button
                    type="button"
                    disabled={safeCurrentPage === totalPages}
                    onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
                    className="px-3 py-1.5 text-xs font-medium rounded-lg border border-slate-200 bg-white text-slate-700 hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition flex items-center gap-1"
                    style={{
                      padding: '6px 12px',
                      fontSize: '12px',
                      borderRadius: '8px',
                      border: '1px solid #e2e8f0',
                      backgroundColor: '#ffffff',
                      color: '#334155',
                      cursor: safeCurrentPage === totalPages ? 'not-allowed' : 'pointer',
                      opacity: safeCurrentPage === totalPages ? 0.45 : 1,
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '4px',
                    }}
                  >
                    <span>Next</span>
                    <ChevronRight size={14} />
                  </button>
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </Layout>
  );
}
