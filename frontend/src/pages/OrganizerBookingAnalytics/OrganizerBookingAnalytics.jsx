import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { useAuth } from '../../context/AuthContext';
import {
  Ticket,
  CheckCircle2,
  XCircle,
  RotateCcw,
  RefreshCw,
  AlertCircle,
  Search,
  ChevronLeft,
  ChevronRight,
  TrendingUp,
  Layers,
  ArrowRight,
  Filter,
  X,
  PieChart,
  Activity,
  BarChart3,
} from 'lucide-react';
import {
  getOrganizerBookingSummary,
  getOrganizerEventsBookingList,
  getEventBookingAnalytics,
  formatBookingAnalyticsError,
} from '../../services/bookingAnalyticsService';
import {
  formatCount,
  formatRate,
  formatCapacityUtilization,
  formatAverageTickets,
  calculateBookingPercentages,
  calculateTierCapacityBreakdown,
  isZeroBookings,
  formatBookingDate,
} from '../../utils/bookingAnalyticsDashboardUtils';
import {
  CATEGORY_OPTIONS,
  validateFilterInputs,
  getActiveFilterChips,
  hasActiveFilters,
} from '../../utils/reportFilterUtils';

export function OrganizerBookingAnalytics() {
  const navigate = useNavigate();
  const { id: routeEventId } = useParams();
  const [searchParams] = useSearchParams();
  const { accessToken } = useAuth();

  // State: Overall Booking Summary
  const [summary, setSummary] = useState(null);
  const [summaryLoading, setSummaryLoading] = useState(true);
  const [summaryError, setSummaryError] = useState(null);

  // State: Events Booking List
  const [eventsData, setEventsData] = useState({ items: [], totalCount: 0, page: 1, pageSize: 10 });
  const [eventsLoading, setEventsLoading] = useState(true);
  const [eventsError, setEventsError] = useState(null);

  // State: Filters
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [selectedEventId, setSelectedEventId] = useState(routeEventId || 'ALL');
  const [category, setCategory] = useState('All');
  const [ticketTypeId, setTicketTypeId] = useState('');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');
  const [validationError, setValidationError] = useState(null);
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 10;

  // State: Event Drilldown / Modal
  const [selectedEventDetails, setSelectedEventDetails] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState(null);
  const [isModalOpen, setIsModalOpen] = useState(false);

  // State: Refreshing
  const [refreshing, setRefreshing] = useState(false);

  // Debounce search query
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(searchQuery.trim());
      setCurrentPage(1);
    }, 400);
    return () => clearTimeout(timer);
  }, [searchQuery]);

  // Sync route param with selectedEventId
  useEffect(() => {
    if (routeEventId) {
      setSelectedEventId(routeEventId);
    }
  }, [routeEventId]);

  // Load overall booking summary
  const loadSummary = useCallback(async () => {
    const validation = validateFilterInputs({
      from: dateFrom,
      to: dateTo,
      category,
      eventId: selectedEventId === 'ALL' ? '' : selectedEventId,
      ticketTypeId,
    }, { role: 'Organizer' });

    if (!validation.isValid) {
      setValidationError(validation.error);
      setSummaryLoading(false);
      return;
    }

    setValidationError(null);
    setSummaryLoading(true);
    setSummaryError(null);

    try {
      const params = {};
      if (dateFrom && !isNaN(new Date(dateFrom).getTime())) params.from = new Date(dateFrom).toISOString();
      if (dateTo && !isNaN(new Date(dateTo).getTime())) {
        const toDate = new Date(dateTo);
        toDate.setHours(23, 59, 59, 999);
        params.to = toDate.toISOString();
      }
      if (category && category !== 'All') params.category = category;
      if (ticketTypeId) params.ticketTypeId = ticketTypeId;

      const data = await getOrganizerBookingSummary(params);
      setSummary(data);
    } catch (err) {
      setSummaryError(formatBookingAnalyticsError(err));
    } finally {
      setSummaryLoading(false);
    }
  }, [dateFrom, dateTo, category, ticketTypeId, selectedEventId]);

  // Load events booking list
  const loadEventsList = useCallback(async () => {
    const validation = validateFilterInputs({
      from: dateFrom,
      to: dateTo,
      category,
    }, { role: 'Organizer' });

    if (!validation.isValid) {
      setValidationError(validation.error);
      setEventsLoading(false);
      return;
    }

    setEventsLoading(true);
    setEventsError(null);

    try {
      const params = {
        page: currentPage,
        pageSize,
      };
      if (debouncedSearch) params.search = debouncedSearch;
      if (dateFrom && !isNaN(new Date(dateFrom).getTime())) params.from = new Date(dateFrom).toISOString();
      if (dateTo && !isNaN(new Date(dateTo).getTime())) {
        const toDate = new Date(dateTo);
        toDate.setHours(23, 59, 59, 999);
        params.to = toDate.toISOString();
      }
      if (category && category !== 'All') params.category = category;

      const data = await getOrganizerEventsBookingList(params);
      setEventsData({
        items: Array.isArray(data?.items) ? data.items : [],
        totalCount: data?.totalCount || 0,
        page: data?.page || currentPage,
        pageSize: data?.pageSize || pageSize,
      });
    } catch (err) {
      setEventsError(formatBookingAnalyticsError(err));
    } finally {
      setEventsLoading(false);
    }
  }, [debouncedSearch, dateFrom, dateTo, category, currentPage]);

  // Load individual event details for drilldown
  const handleOpenEventModal = async (eventId) => {
    setIsModalOpen(true);
    setDetailLoading(true);
    setDetailError(null);
    setSelectedEventDetails(null);

    try {
      const params = {};
      if (dateFrom) params.from = new Date(dateFrom).toISOString();
      if (dateTo) {
        const toDate = new Date(dateTo);
        toDate.setHours(23, 59, 59, 999);
        params.to = toDate.toISOString();
      }

      const data = await getEventBookingAnalytics(eventId, params);
      setSelectedEventDetails(data);
    } catch (err) {
      setDetailError(formatBookingAnalyticsError(err));
    } finally {
      setDetailLoading(false);
    }
  };

  const handleCloseModal = () => {
    setIsModalOpen(false);
    setSelectedEventDetails(null);
    setDetailError(null);
  };

  // Unified reload
  const handleRefresh = async () => {
    setRefreshing(true);
    await Promise.all([loadSummary(), loadEventsList()]);
    if (isModalOpen && selectedEventDetails?.eventId) {
      await handleOpenEventModal(selectedEventDetails.eventId);
    }
    setRefreshing(false);
  };

  useEffect(() => {
    loadSummary();
  }, [loadSummary]);

  useEffect(() => {
    loadEventsList();
  }, [loadEventsList]);

  // Date range presets
  const applyPreset = (preset) => {
    const now = new Date();
    setCurrentPage(1);

    if (preset === 'ALL') {
      setDateFrom('');
      setDateTo('');
      return;
    }

    const start = new Date();
    if (preset === '7D') {
      start.setDate(now.getDate() - 7);
    } else if (preset === '30D') {
      start.setDate(now.getDate() - 30);
    } else if (preset === '90D') {
      start.setDate(now.getDate() - 90);
    }

    setDateFrom(start.toISOString().split('T')[0]);
    setDateTo(now.toISOString().split('T')[0]);
  };

  // Filtered events based on event selector
  const displayedEvents = useMemo(() => {
    if (selectedEventId === 'ALL') {
      return eventsData.items;
    }
    return eventsData.items.filter((e) => String(e.eventId).toLowerCase() === String(selectedEventId).toLowerCase());
  }, [eventsData.items, selectedEventId]);

  // Proportional booking distribution across events for chart
  const chartItems = useMemo(() => {
    return calculateBookingPercentages(eventsData.items.slice(0, 5));
  }, [eventsData.items]);

  const noBookingsYet = isZeroBookings(summary);
  const totalPages = Math.ceil(eventsData.totalCount / pageSize) || 1;

  return (
    <div
      style={{
        minHeight: '100vh',
        backgroundColor: 'var(--ep-canvas)',
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      <Header />

      <main
        className="container"
        style={{
          flex: 1,
          paddingTop: '36px',
          paddingBottom: '64px',
        }}
      >
        {/* Header Section */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            marginBottom: '28px',
            flexWrap: 'wrap',
            gap: '16px',
          }}
        >
          <div>
            <div
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                backgroundColor: '#ffffff',
                color: 'var(--ep-text-primary)',
                padding: '4px 10px',
                borderRadius: 'var(--ep-radius-pill)',
                fontSize: '12px',
                fontWeight: 600,
                border: '1px solid var(--ep-border)',
                marginBottom: '8px',
              }}
            >
              <Ticket size={14} color="var(--ep-primary)" />
              <span>Booking Statistics & Capacity Intelligence</span>
            </div>
            <h1
              style={{
                fontSize: '28px',
                fontWeight: 700,
                color: 'var(--ep-text-primary)',
                margin: 0,
                letterSpacing: '-0.01em',
              }}
            >
              Booking Analytics
            </h1>
            <p
              style={{
                fontSize: '14px',
                color: 'var(--ep-text-secondary)',
                marginTop: '4px',
                margin: 0,
              }}
            >
              Track booking conversion, ticket volume, cancellation rates, and attendance capacity across your events.
            </p>
          </div>

          <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap', alignItems: 'center' }}>
            <button
              type="button"
              data-testid="booking-analytics-refresh-btn"
              onClick={handleRefresh}
              disabled={refreshing || summaryLoading || eventsLoading}
              className="ep-btn-secondary"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                padding: '10px 16px',
                fontSize: '13px',
                fontWeight: 600,
                backgroundColor: '#ffffff',
                border: '1px solid var(--ep-border)',
                borderRadius: 'var(--ep-radius-btn)',
                cursor: refreshing ? 'not-allowed' : 'pointer',
              }}
            >
              <RefreshCw size={14} className={refreshing ? 'spin' : ''} />
              <span>{refreshing ? 'Refreshing...' : 'Refresh Data'}</span>
            </button>

            <Link
              to="/organizer/sales-analytics"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                padding: '10px 16px',
                fontSize: '13px',
                fontWeight: 600,
                backgroundColor: '#ffffff',
                border: '1px solid var(--ep-border)',
                borderRadius: 'var(--ep-radius-btn)',
                color: 'var(--ep-text-primary)',
                textDecoration: 'none',
              }}
            >
              <TrendingUp size={14} color="var(--ep-primary)" />
              <span>Sales Analytics</span>
            </Link>

            <Link
              to="/organizer"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                padding: '10px 16px',
                fontSize: '13px',
                fontWeight: 600,
                backgroundColor: '#ffffff',
                border: '1px solid var(--ep-border)',
                borderRadius: 'var(--ep-radius-btn)',
                color: 'var(--ep-text-primary)',
                textDecoration: 'none',
              }}
            >
              <Layers size={14} />
              <span>Organizer Dashboard</span>
            </Link>
          </div>
        </div>

        {/* Filter Controls Bar */}
        <div
          style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            padding: '18px 20px',
            marginBottom: '24px',
            boxShadow: 'var(--ep-shadow-card)',
            display: 'flex',
            flexDirection: 'column',
            gap: '16px',
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
            {/* Event Selector */}
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', minWidth: '220px' }}>
              <Filter size={16} color="var(--ep-text-secondary)" />
              <label htmlFor="event-filter" style={{ fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                Filter Event:
              </label>
              <select
                id="event-filter"
                data-testid="booking-event-filter"
                value={selectedEventId}
                onChange={(e) => setSelectedEventId(e.target.value)}
                style={{
                  padding: '8px 12px',
                  borderRadius: 'var(--ep-radius-btn)',
                  border: '1px solid var(--ep-border)',
                  fontSize: '13px',
                  color: 'var(--ep-text-primary)',
                  backgroundColor: '#ffffff',
                  outline: 'none',
                  flex: 1,
                }}
              >
                <option value="ALL">All Events (Overall Portfolio)</option>
                {eventsData.items.map((evt) => (
                  <option key={evt.eventId} value={evt.eventId}>
                    {evt.eventName}
                  </option>
                ))}
              </select>
            </div>

            {/* Category Selector */}
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', minWidth: '180px' }}>
              <label htmlFor="booking-category-filter" style={{ fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                Category:
              </label>
              <select
                id="booking-category-filter"
                data-testid="booking-category-filter"
                value={category}
                onChange={(e) => {
                  setCategory(e.target.value);
                  setCurrentPage(1);
                }}
                style={{
                  padding: '8px 12px',
                  borderRadius: 'var(--ep-radius-btn)',
                  border: '1px solid var(--ep-border)',
                  fontSize: '13px',
                  color: 'var(--ep-text-primary)',
                  backgroundColor: '#ffffff',
                  outline: 'none',
                  flex: 1,
                }}
              >
                {CATEGORY_OPTIONS.map((opt) => (
                  <option key={opt.value} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </select>
            </div>

            {/* Quick Period Presets */}
            <div style={{ display: 'flex', alignItems: 'center', gap: '6px', flexWrap: 'wrap' }}>
              <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', marginRight: '4px', fontWeight: 500 }}>
                Period:
              </span>
              <button
                type="button"
                onClick={() => applyPreset('ALL')}
                style={{
                  padding: '5px 10px',
                  fontSize: '12px',
                  borderRadius: 'var(--ep-radius-pill)',
                  border: !dateFrom && !dateTo ? '1px solid var(--ep-primary)' : '1px solid var(--ep-border)',
                  backgroundColor: !dateFrom && !dateTo ? '#FFF0E6' : '#ffffff',
                  color: !dateFrom && !dateTo ? 'var(--ep-primary)' : 'var(--ep-text-secondary)',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                All Time
              </button>
              <button
                type="button"
                onClick={() => applyPreset('7D')}
                style={{
                  padding: '5px 10px',
                  fontSize: '12px',
                  borderRadius: 'var(--ep-radius-pill)',
                  border: '1px solid var(--ep-border)',
                  backgroundColor: '#ffffff',
                  color: 'var(--ep-text-secondary)',
                  fontWeight: 500,
                  cursor: 'pointer',
                }}
              >
                Last 7 Days
              </button>
              <button
                type="button"
                onClick={() => applyPreset('30D')}
                style={{
                  padding: '5px 10px',
                  fontSize: '12px',
                  borderRadius: 'var(--ep-radius-pill)',
                  border: '1px solid var(--ep-border)',
                  backgroundColor: '#ffffff',
                  color: 'var(--ep-text-secondary)',
                  fontWeight: 500,
                  cursor: 'pointer',
                }}
              >
                Last 30 Days
              </button>
              <button
                type="button"
                onClick={() => applyPreset('90D')}
                style={{
                  padding: '5px 10px',
                  fontSize: '12px',
                  borderRadius: 'var(--ep-radius-pill)',
                  border: '1px solid var(--ep-border)',
                  backgroundColor: '#ffffff',
                  color: 'var(--ep-text-secondary)',
                  fontWeight: 500,
                  cursor: 'pointer',
                }}
              >
                Last 90 Days
              </button>
            </div>
          </div>

          {/* Date range inputs & Search input */}
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              flexWrap: 'wrap',
              gap: '14px',
              borderTop: '1px solid var(--ep-border)',
              paddingTop: '14px',
            }}
          >
            {/* Custom Dates */}
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', fontWeight: 500 }}>From:</span>
                <input
                  type="date"
                  data-testid="booking-date-from"
                  value={dateFrom}
                  onChange={(e) => {
                    setDateFrom(e.target.value);
                    setCurrentPage(1);
                  }}
                  style={{
                    padding: '6px 10px',
                    fontSize: '12px',
                    borderRadius: 'var(--ep-radius-btn)',
                    border: '1px solid var(--ep-border)',
                    color: 'var(--ep-text-primary)',
                  }}
                />
              </div>

              <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', fontWeight: 500 }}>To:</span>
                <input
                  type="date"
                  data-testid="booking-date-to"
                  value={dateTo}
                  onChange={(e) => {
                    setDateTo(e.target.value);
                    setCurrentPage(1);
                  }}
                  style={{
                    padding: '6px 10px',
                    fontSize: '12px',
                    borderRadius: 'var(--ep-radius-btn)',
                    border: '1px solid var(--ep-border)',
                    color: 'var(--ep-text-primary)',
                  }}
                />
              </div>

              {(dateFrom || dateTo) && (
                <button
                  type="button"
                  onClick={() => applyPreset('ALL')}
                  style={{
                    background: 'none',
                    border: 'none',
                    color: 'var(--ep-primary)',
                    fontSize: '12px',
                    fontWeight: 600,
                    cursor: 'pointer',
                    textDecoration: 'underline',
                  }}
                >
                  Clear Dates
                </button>
              )}
            </div>

            {/* Event Title Search */}
            <div style={{ position: 'relative', width: '280px' }}>
              <Search
                size={14}
                style={{
                  position: 'absolute',
                  left: '12px',
                  top: '50%',
                  transform: 'translateY(-50%)',
                  color: 'var(--ep-text-secondary)',
                }}
              />
              <input
                type="text"
                data-testid="booking-analytics-search-input"
                placeholder="Search events by title..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                style={{
                  width: '100%',
                  padding: '8px 12px 8px 34px',
                  borderRadius: 'var(--ep-radius-btn)',
                  border: '1px solid var(--ep-border)',
                  fontSize: '13px',
                  outline: 'none',
                  boxSizing: 'border-box',
                }}
              />
              {/* Active Filter Badges & Reset Button */}
              {(hasActiveFilters({ from: dateFrom, to: dateTo, category, eventId: selectedEventId, ticketTypeId }, 'Organizer') || searchQuery) && (
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    flexWrap: 'wrap',
                    gap: '8px',
                    width: '100%',
                    borderTop: '1px solid var(--ep-border)',
                    paddingTop: '12px',
                    marginTop: '4px',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '6px', flexWrap: 'wrap' }}>
                    <span style={{ fontSize: '11px', fontWeight: 700, textTransform: 'uppercase', color: 'var(--ep-text-secondary)', marginRight: '4px' }}>
                      Active Filters:
                    </span>
                    {getActiveFilterChips({ from: dateFrom, to: dateTo, category, eventId: selectedEventId, ticketTypeId }, {
                      role: 'Organizer',
                      onRemove: (field) => {
                        if (field === 'dateRange') { setDateFrom(''); setDateTo(''); }
                        if (field === 'category') { setCategory('All'); }
                        if (field === 'eventId') { setSelectedEventId('ALL'); setTicketTypeId(''); }
                        if (field === 'ticketTypeId') { setTicketTypeId(''); }
                      },
                      eventLabels: eventsData.items.reduce((acc, ev) => { acc[ev.eventId] = ev.eventName; return acc; }, {}),
                    }).map((chip) => (
                      <span
                        key={chip.id}
                        data-testid={`booking-active-chip-${chip.id}`}
                        style={{
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '4px',
                          padding: '3px 8px',
                          borderRadius: 'var(--ep-radius-pill)',
                          backgroundColor: 'rgba(255, 90, 39, 0.08)',
                          color: 'var(--ep-primary)',
                          fontSize: '12px',
                          fontWeight: 600,
                        }}
                      >
                        <span>{chip.label}</span>
                        <button
                          type="button"
                          onClick={chip.onRemove}
                          style={{ background: 'none', border: 'none', padding: 0, cursor: 'pointer', color: 'inherit', display: 'flex' }}
                        >
                          <X size={12} />
                        </button>
                      </span>
                    ))}
                    {searchQuery && (
                      <span
                        style={{
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '4px',
                          padding: '3px 8px',
                          borderRadius: 'var(--ep-radius-pill)',
                          backgroundColor: 'rgba(255, 90, 39, 0.08)',
                          color: 'var(--ep-primary)',
                          fontSize: '12px',
                          fontWeight: 600,
                        }}
                      >
                        <span>Search: "{searchQuery}"</span>
                        <button
                          type="button"
                          onClick={() => setSearchQuery('')}
                          style={{ background: 'none', border: 'none', padding: 0, cursor: 'pointer', color: 'inherit', display: 'flex' }}
                        >
                          <X size={12} />
                        </button>
                      </span>
                    )}
                  </div>

                  <button
                    type="button"
                    data-testid="booking-clear-filters-btn"
                    onClick={() => {
                      setSelectedEventId('ALL');
                      setCategory('All');
                      setDateFrom('');
                      setDateTo('');
                      setTicketTypeId('');
                      setSearchQuery('');
                      setValidationError(null);
                      setCurrentPage(1);
                    }}
                    style={{
                      background: 'none',
                      border: 'none',
                      color: 'var(--ep-primary)',
                      fontSize: '12px',
                      fontWeight: 600,
                      cursor: 'pointer',
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '4px',
                    }}
                  >
                    <RotateCcw size={12} />
                    <span>Reset All Filters</span>
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Validation Error Banner */}
        {validationError && (
          <div
            role="alert"
            data-testid="booking-validation-alert"
            style={{
              backgroundColor: '#FEF2F2',
              border: '1px solid #FECACA',
              borderRadius: 'var(--ep-radius-card)',
              padding: '12px 18px',
              marginBottom: '20px',
              display: 'flex',
              alignItems: 'center',
              gap: '10px',
              color: '#991B1B',
              fontSize: '13px',
              fontWeight: 500,
            }}
          >
            <AlertCircle size={16} />
            <span>{validationError}</span>
          </div>
        )}

        {/* Summary Error Banner with Retry */}
        {summaryError && (
          <div
            style={{
              backgroundColor: '#FFEBEE',
              border: '1px solid #FFCDD2',
              borderRadius: 'var(--ep-radius-card)',
              padding: '16px 20px',
              marginBottom: '24px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: '12px',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
              <AlertCircle size={18} color="#C62828" />
              <span style={{ fontSize: '14px', color: '#C62828', fontWeight: 500 }}>{summaryError}</span>
            </div>
            <button
              type="button"
              data-testid="booking-analytics-retry-btn"
              onClick={handleRefresh}
              style={{
                backgroundColor: '#ffffff',
                border: '1px solid #C62828',
                color: '#C62828',
                padding: '6px 14px',
                borderRadius: 'var(--ep-radius-btn)',
                fontSize: '12px',
                fontWeight: 600,
                cursor: 'pointer',
              }}
            >
              Retry
            </button>
          </div>
        )}

        {/* Summary Cards Grid (EP-390 Step 3) */}
        <div
          data-testid="booking-summary-cards"
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(210px, 1fr))',
            gap: '16px',
            marginBottom: '28px',
          }}
        >
          {/* Card 1: Total Bookings */}
          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              padding: '20px',
              boxShadow: 'var(--ep-shadow-card)',
              display: 'flex',
              flexDirection: 'column',
              justifyContent: 'space-between',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '12px' }}>
              <span style={{ fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-secondary)' }}>
                Total Bookings
              </span>
              <div
                style={{
                  width: '32px',
                  height: '32px',
                  borderRadius: 'var(--ep-radius-pill)',
                  backgroundColor: '#E3F2FD',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                }}
              >
                <Activity size={16} color="#1565C0" />
              </div>
            </div>
            <div style={{ fontSize: '26px', fontWeight: 700, color: 'var(--ep-text-primary)', marginBottom: '4px' }}>
              {summaryLoading ? (
                <span className="spinner-border spinner-border-sm" role="status" />
              ) : (
                formatCount(summary?.totalBookings)
              )}
            </div>
            <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
              Total confirmed + cancelled orders
            </span>
          </div>

          {/* Card 2: Confirmed Bookings */}
          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              padding: '20px',
              boxShadow: 'var(--ep-shadow-card)',
              display: 'flex',
              flexDirection: 'column',
              justifyContent: 'space-between',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '12px' }}>
              <span style={{ fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-secondary)' }}>
                Confirmed Bookings
              </span>
              <div
                style={{
                  width: '32px',
                  height: '32px',
                  borderRadius: 'var(--ep-radius-pill)',
                  backgroundColor: '#E8F5E9',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                }}
              >
                <CheckCircle2 size={16} color="#2E7D32" />
              </div>
            </div>
            <div style={{ fontSize: '26px', fontWeight: 700, color: '#2E7D32', marginBottom: '4px' }}>
              {summaryLoading ? (
                <span className="spinner-border spinner-border-sm" role="status" />
              ) : (
                formatCount(summary?.confirmedBookings)
              )}
            </div>
            <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
              Active, non-cancelled orders
            </span>
          </div>

          {/* Card 3: Cancelled Bookings */}
          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              padding: '20px',
              boxShadow: 'var(--ep-shadow-card)',
              display: 'flex',
              flexDirection: 'column',
              justifyContent: 'space-between',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '12px' }}>
              <span style={{ fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-secondary)' }}>
                Cancelled Bookings
              </span>
              <div
                style={{
                  width: '32px',
                  height: '32px',
                  borderRadius: 'var(--ep-radius-pill)',
                  backgroundColor: '#FFEBEE',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                }}
              >
                <XCircle size={16} color="#C62828" />
              </div>
            </div>
            <div style={{ fontSize: '26px', fontWeight: 700, color: '#C62828', marginBottom: '4px' }}>
              {summaryLoading ? (
                <span className="spinner-border spinner-border-sm" role="status" />
              ) : (
                formatCount(summary?.cancelledBookings)
              )}
            </div>
            <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
              Bookings cancelled or refunded
            </span>
          </div>

          {/* Card 4: Tickets Booked (Active vs Total) */}
          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              padding: '20px',
              boxShadow: 'var(--ep-shadow-card)',
              display: 'flex',
              flexDirection: 'column',
              justifyContent: 'space-between',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '12px' }}>
              <span style={{ fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-secondary)' }}>
                Active Tickets Booked
              </span>
              <div
                style={{
                  width: '32px',
                  height: '32px',
                  borderRadius: 'var(--ep-radius-pill)',
                  backgroundColor: '#FFF0E6',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                }}
              >
                <Ticket size={16} color="var(--ep-primary)" />
              </div>
            </div>
            <div style={{ fontSize: '26px', fontWeight: 700, color: 'var(--ep-text-primary)', marginBottom: '4px' }}>
              {summaryLoading ? (
                <span className="spinner-border spinner-border-sm" role="status" />
              ) : (
                formatCount(summary?.activeTicketsBooked)
              )}
            </div>
            <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
              {formatCount(summary?.totalTicketsBooked)} total tickets issued
            </span>
          </div>

          {/* Card 5: Cancellation Rate */}
          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              padding: '20px',
              boxShadow: 'var(--ep-shadow-card)',
              display: 'flex',
              flexDirection: 'column',
              justifyContent: 'space-between',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '12px' }}>
              <span style={{ fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-secondary)' }}>
                Cancellation Rate
              </span>
              <div
                style={{
                  width: '32px',
                  height: '32px',
                  borderRadius: 'var(--ep-radius-pill)',
                  backgroundColor: '#F3E5F5',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                }}
              >
                <PieChart size={16} color="#7B1FA2" />
              </div>
            </div>
            <div style={{ fontSize: '26px', fontWeight: 700, color: 'var(--ep-text-primary)', marginBottom: '4px' }}>
              {summaryLoading ? (
                <span className="spinner-border spinner-border-sm" role="status" />
              ) : (
                formatRate(summary?.cancellationRate)
              )}
            </div>
            <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
              Ticket rate: {formatRate(summary?.ticketCancellationRate)} • Avg {formatAverageTickets(summary?.averageTicketsPerBooking)} tix/order
            </span>
          </div>
        </div>

        {/* Visual Booking Distribution Chart (EP-390 Step 6) */}
        {!noBookingsYet && chartItems.length > 0 && (
          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              padding: '24px',
              marginBottom: '28px',
              boxShadow: 'var(--ep-shadow-card)',
            }}
          >
            <div style={{ marginBottom: '18px' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <BarChart3 size={18} color="var(--ep-primary)" />
                <h2 style={{ fontSize: '16px', fontWeight: 700, color: 'var(--ep-text-primary)', margin: 0 }}>
                  Booking Volume Distribution by Event
                </h2>
              </div>
              <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', marginTop: '4px', margin: 0 }}>
                Top performing events ranked by total booking share across your portfolio
              </p>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              {chartItems.map((evt) => (
                <div key={evt.eventId} style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: '13px' }}>
                    <span style={{ fontWeight: 600, color: 'var(--ep-text-primary)' }}>{evt.eventName}</span>
                    <span style={{ fontWeight: 600, color: 'var(--ep-text-secondary)' }}>
                      {formatCount(evt.totalBookings)} bookings ({evt.percentage}%) • {formatCount(evt.activeTicketsBooked)} tickets
                    </span>
                  </div>
                  {/* Proportional Visual Bar */}
                  <div
                    style={{
                      height: '10px',
                      width: '100%',
                      backgroundColor: 'var(--ep-canvas)',
                      borderRadius: '5px',
                      overflow: 'hidden',
                    }}
                  >
                    <div
                      style={{
                        height: '100%',
                        width: `${Math.max(4, evt.percentage)}%`,
                        backgroundColor: 'var(--ep-primary)',
                        borderRadius: '5px',
                        transition: 'width 0.4s ease',
                      }}
                    />
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Per-Event Booking Performance Table (EP-390 Step 4) */}
        <div
          style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            boxShadow: 'var(--ep-shadow-card)',
            overflow: 'hidden',
          }}
        >
          <div
            style={{
              padding: '20px 24px',
              borderBottom: '1px solid var(--ep-border)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              flexWrap: 'wrap',
              gap: '12px',
            }}
          >
            <div>
              <h2 style={{ fontSize: '18px', fontWeight: 700, color: 'var(--ep-text-primary)', margin: 0 }}>
                Event Booking Statistics
              </h2>
              <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', marginTop: '4px', margin: 0 }}>
                Per-event booking activity and capacity status. Click "View Details" to inspect tier breakdown.
              </p>
            </div>
            <span style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', fontWeight: 500 }}>
              Showing {displayedEvents.length} of {eventsData.totalCount} events
            </span>
          </div>

          {/* Table or States */}
          {eventsLoading ? (
            <div
              aria-busy="true"
              style={{
                padding: '48px 24px',
                textAlign: 'center',
                color: 'var(--ep-text-secondary)',
              }}
            >
              <div className="spinner-border text-primary" role="status" style={{ width: '2rem', height: '2rem' }}>
                <span className="visually-hidden">Loading booking data...</span>
              </div>
              <p style={{ marginTop: '12px', fontSize: '14px' }}>Loading event booking records...</p>
            </div>
          ) : eventsError ? (
            <div style={{ padding: '36px 24px', textAlign: 'center' }}>
              <AlertCircle size={28} color="#C62828" style={{ marginBottom: '8px' }} />
              <p style={{ color: '#C62828', fontSize: '14px', fontWeight: 500 }}>{eventsError}</p>
              <button
                type="button"
                onClick={loadEventsList}
                style={{
                  marginTop: '10px',
                  backgroundColor: 'var(--ep-primary)',
                  color: '#ffffff',
                  border: 'none',
                  padding: '8px 16px',
                  borderRadius: 'var(--ep-radius-btn)',
                  fontSize: '13px',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                Retry
              </button>
            </div>
          ) : displayedEvents.length === 0 ? (
            <div
              data-testid="booking-analytics-empty-state"
              style={{
                padding: '48px 24px',
                textAlign: 'center',
                color: 'var(--ep-text-secondary)',
              }}
            >
              <Ticket size={36} color="var(--ep-border)" style={{ margin: '0 auto 12px auto' }} />
              <h3 style={{ fontSize: '16px', fontWeight: 600, color: 'var(--ep-text-primary)', margin: 0 }}>
                No booking data is available for your events yet.
              </h3>
              <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', marginTop: '4px', maxWidth: '420px', margin: '4px auto 0 auto' }}>
                When customers purchase tickets to your published events, booking numbers, conversion metrics, and cancellation rates will appear here automatically.
              </p>
            </div>
          ) : (
            <div className="table-responsive">
              <table
                data-testid="event-booking-table"
                style={{
                  width: '100%',
                  borderCollapse: 'collapse',
                  textAlign: 'left',
                  fontSize: '13px',
                }}
              >
                <thead>
                  <tr style={{ backgroundColor: 'var(--ep-canvas)', borderBottom: '1px solid var(--ep-border)' }}>
                    <th style={{ padding: '14px 20px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>Event Name</th>
                    <th style={{ padding: '14px 16px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>Total Bookings</th>
                    <th style={{ padding: '14px 16px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>Confirmed</th>
                    <th style={{ padding: '14px 16px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>Cancelled</th>
                    <th style={{ padding: '14px 16px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>Active Tickets</th>
                    <th style={{ padding: '14px 16px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>Cancellation Rate</th>
                    <th style={{ padding: '14px 16px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>Capacity</th>
                    <th style={{ padding: '14px 20px', fontWeight: 600, color: 'var(--ep-text-primary)', textAlign: 'right' }}>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {displayedEvents.map((evt) => (
                    <tr
                      key={evt.eventId}
                      style={{
                        borderBottom: '1px solid var(--ep-border)',
                        transition: 'background-color 0.15s ease',
                      }}
                      onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = '#FFFBF8')}
                      onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = 'transparent')}
                    >
                      <td style={{ padding: '14px 20px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                        <div style={{ display: 'flex', flexDirection: 'column' }}>
                          <span>{evt.eventName}</span>
                          {evt.lastBookingAtUtc && (
                            <span style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', fontWeight: 400 }}>
                              Last booked: {formatBookingDate(evt.lastBookingAtUtc)}
                            </span>
                          )}
                        </div>
                      </td>
                      <td style={{ padding: '14px 16px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                        {formatCount(evt.totalBookings)}
                      </td>
                      <td style={{ padding: '14px 16px', color: '#2E7D32', fontWeight: 600 }}>
                        {formatCount(evt.confirmedBookings)}
                      </td>
                      <td style={{ padding: '14px 16px', color: evt.cancelledBookings > 0 ? '#C62828' : 'var(--ep-text-secondary)', fontWeight: 600 }}>
                        {formatCount(evt.cancelledBookings)}
                      </td>
                      <td style={{ padding: '14px 16px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                        {formatCount(evt.activeTicketsBooked)}
                      </td>
                      <td style={{ padding: '14px 16px' }}>
                        <span
                          style={{
                            padding: '3px 8px',
                            borderRadius: 'var(--ep-radius-pill)',
                            fontSize: '11px',
                            fontWeight: 600,
                            backgroundColor: evt.cancellationRate > 20 ? '#FFEBEE' : evt.cancellationRate > 0 ? '#FFF9C4' : '#E8F5E9',
                            color: evt.cancellationRate > 20 ? '#C62828' : evt.cancellationRate > 0 ? '#F57F17' : '#2E7D32',
                          }}
                        >
                          {formatRate(evt.cancellationRate)}
                        </span>
                      </td>
                      <td style={{ padding: '14px 16px', color: 'var(--ep-text-secondary)' }}>
                        {evt.totalCapacity > 0 ? (
                          <span>
                            {formatCount(evt.activeTicketsBooked)} / {formatCount(evt.totalCapacity)} ({Math.round((evt.activeTicketsBooked / evt.totalCapacity) * 100)}%)
                          </span>
                        ) : (
                          <span>—</span>
                        )}
                      </td>
                      <td style={{ padding: '14px 20px', textAlign: 'right' }}>
                        <button
                          type="button"
                          data-testid={`view-details-${evt.eventId}`}
                          onClick={() => handleOpenEventModal(evt.eventId)}
                          style={{
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: '4px',
                            padding: '6px 12px',
                            borderRadius: 'var(--ep-radius-btn)',
                            border: '1px solid var(--ep-border)',
                            backgroundColor: '#ffffff',
                            color: 'var(--ep-primary)',
                            fontSize: '12px',
                            fontWeight: 600,
                            cursor: 'pointer',
                          }}
                        >
                          <span>View Details</span>
                          <ArrowRight size={13} />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* Pagination Controls */}
          {eventsData.totalCount > pageSize && (
            <div
              style={{
                padding: '16px 24px',
                borderTop: '1px solid var(--ep-border)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
              }}
            >
              <span style={{ fontSize: '13px', color: 'var(--ep-text-secondary)' }}>
                Page {currentPage} of {totalPages}
              </span>
              <div style={{ display: 'flex', gap: '8px' }}>
                <button
                  type="button"
                  disabled={currentPage <= 1}
                  onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '4px',
                    padding: '6px 12px',
                    fontSize: '12px',
                    fontWeight: 600,
                    borderRadius: 'var(--ep-radius-btn)',
                    border: '1px solid var(--ep-border)',
                    backgroundColor: '#ffffff',
                    cursor: currentPage <= 1 ? 'not-allowed' : 'pointer',
                    opacity: currentPage <= 1 ? 0.5 : 1,
                  }}
                >
                  <ChevronLeft size={14} />
                  <span>Previous</span>
                </button>
                <button
                  type="button"
                  disabled={currentPage >= totalPages}
                  onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '4px',
                    padding: '6px 12px',
                    fontSize: '12px',
                    fontWeight: 600,
                    borderRadius: 'var(--ep-radius-btn)',
                    border: '1px solid var(--ep-border)',
                    backgroundColor: '#ffffff',
                    cursor: currentPage >= totalPages ? 'not-allowed' : 'pointer',
                    opacity: currentPage >= totalPages ? 0.5 : 1,
                  }}
                >
                  <span>Next</span>
                  <ChevronRight size={14} />
                </button>
              </div>
            </div>
          )}
        </div>
      </main>

      {/* Drilldown Modal (EP-390 Step 4 & 6) */}
      {isModalOpen && (
        <div
          data-testid="event-drilldown-modal"
          style={{
            position: 'fixed',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            backgroundColor: 'rgba(0, 0, 0, 0.45)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000,
            padding: '16px',
          }}
          onClick={handleCloseModal}
        >
          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              maxWidth: '720px',
              width: '100%',
              maxHeight: '90vh',
              overflowY: 'auto',
              boxShadow: 'var(--ep-shadow-hover)',
            }}
            onClick={(e) => e.stopPropagation()}
          >
            {/* Modal Header */}
            <div
              style={{
                padding: '18px 24px',
                borderBottom: '1px solid var(--ep-border)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
              }}
            >
              <div>
                <h3 style={{ fontSize: '18px', fontWeight: 700, margin: 0, color: 'var(--ep-text-primary)' }}>
                  {selectedEventDetails?.eventName || 'Event Booking Intelligence'}
                </h3>
                <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                  Detailed booking counts, ticket tier breakdown, and capacity metrics
                </span>
              </div>
              <button
                type="button"
                onClick={handleCloseModal}
                style={{
                  background: 'none',
                  border: 'none',
                  cursor: 'pointer',
                  color: 'var(--ep-text-secondary)',
                  padding: '4px',
                }}
              >
                <X size={20} />
              </button>
            </div>

            {/* Modal Content */}
            <div style={{ padding: '24px' }}>
              {detailLoading ? (
                <div style={{ padding: '36px', textAlign: 'center', color: 'var(--ep-text-secondary)' }}>
                  <div className="spinner-border text-primary" role="status" style={{ width: '2rem', height: '2rem' }}>
                    <span className="visually-hidden">Loading event details...</span>
                  </div>
                  <p style={{ marginTop: '12px', fontSize: '13px' }}>Loading ticket tier details...</p>
                </div>
              ) : detailError ? (
                <div style={{ padding: '24px', textAlign: 'center', color: '#C62828' }}>
                  <AlertCircle size={24} style={{ marginBottom: '8px' }} />
                  <p style={{ fontSize: '14px', fontWeight: 500 }}>{detailError}</p>
                </div>
              ) : selectedEventDetails ? (
                <div>
                  {/* High level metrics */}
                  <div
                    style={{
                      display: 'grid',
                      gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))',
                      gap: '12px',
                      marginBottom: '20px',
                    }}
                  >
                    <div style={{ padding: '12px', borderRadius: 'var(--ep-radius-card)', backgroundColor: 'var(--ep-canvas)' }}>
                      <span style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', fontWeight: 600 }}>TOTAL BOOKINGS</span>
                      <div style={{ fontSize: '18px', fontWeight: 700, color: 'var(--ep-text-primary)', marginTop: '2px' }}>
                        {formatCount(selectedEventDetails.totalBookings)}
                      </div>
                    </div>
                    <div style={{ padding: '12px', borderRadius: 'var(--ep-radius-card)', backgroundColor: '#E8F5E9' }}>
                      <span style={{ fontSize: '11px', color: '#2E7D32', fontWeight: 600 }}>CONFIRMED</span>
                      <div style={{ fontSize: '18px', fontWeight: 700, color: '#2E7D32', marginTop: '2px' }}>
                        {formatCount(selectedEventDetails.confirmedBookings)}
                      </div>
                    </div>
                    <div style={{ padding: '12px', borderRadius: 'var(--ep-radius-card)', backgroundColor: '#FFEBEE' }}>
                      <span style={{ fontSize: '11px', color: '#C62828', fontWeight: 600 }}>CANCELLED</span>
                      <div style={{ fontSize: '18px', fontWeight: 700, color: '#C62828', marginTop: '2px' }}>
                        {formatCount(selectedEventDetails.cancelledBookings)}
                      </div>
                    </div>
                    <div style={{ padding: '12px', borderRadius: 'var(--ep-radius-card)', backgroundColor: 'var(--ep-canvas)' }}>
                      <span style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', fontWeight: 600 }}>ACTIVE TICKETS</span>
                      <div style={{ fontSize: '18px', fontWeight: 700, color: 'var(--ep-text-primary)', marginTop: '2px' }}>
                        {formatCount(selectedEventDetails.activeTicketsBooked)}
                      </div>
                    </div>
                    <div style={{ padding: '12px', borderRadius: 'var(--ep-radius-card)', backgroundColor: 'var(--ep-canvas)' }}>
                      <span style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', fontWeight: 600 }}>CAPACITY UTIL.</span>
                      <div style={{ fontSize: '18px', fontWeight: 700, color: 'var(--ep-text-primary)', marginTop: '2px' }}>
                        {formatCapacityUtilization(selectedEventDetails.capacityUtilizationRate)}
                      </div>
                    </div>
                  </div>

                  {/* Overall Capacity Bar */}
                  {selectedEventDetails.totalCapacity > 0 && (
                    <div style={{ marginBottom: '24px' }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '12px', marginBottom: '6px' }}>
                        <span style={{ fontWeight: 600, color: 'var(--ep-text-primary)' }}>Overall Capacity Progress</span>
                        <span style={{ color: 'var(--ep-text-secondary)' }}>
                          {formatCount(selectedEventDetails.activeTicketsBooked)} of {formatCount(selectedEventDetails.totalCapacity)} spots filled ({formatCapacityUtilization(selectedEventDetails.capacityUtilizationRate)})
                        </span>
                      </div>
                      <div style={{ height: '8px', width: '100%', backgroundColor: 'var(--ep-canvas)', borderRadius: '4px', overflow: 'hidden' }}>
                        <div
                          style={{
                            height: '100%',
                            width: `${Math.min(100, Math.max(0, selectedEventDetails.capacityUtilizationRate))}%`,
                            backgroundColor: 'var(--ep-primary)',
                            borderRadius: '4px',
                          }}
                        />
                      </div>
                    </div>
                  )}

                  {/* Ticket Types Breakdown */}
                  <h4 style={{ fontSize: '14px', fontWeight: 700, margin: '0 0 12px 0', color: 'var(--ep-text-primary)' }}>
                    Ticket Types & Tier Breakdown
                  </h4>
                  {Array.isArray(selectedEventDetails.ticketTypes) && selectedEventDetails.ticketTypes.length > 0 ? (
                    <div className="table-responsive">
                      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px' }}>
                        <thead>
                          <tr style={{ backgroundColor: 'var(--ep-canvas)', borderBottom: '1px solid var(--ep-border)' }}>
                            <th style={{ padding: '10px 14px', fontWeight: 600 }}>Ticket Tier</th>
                            <th style={{ padding: '10px 12px', fontWeight: 600 }}>Bookings</th>
                            <th style={{ padding: '10px 12px', fontWeight: 600 }}>Active</th>
                            <th style={{ padding: '10px 12px', fontWeight: 600 }}>Cancelled</th>
                            <th style={{ padding: '10px 12px', fontWeight: 600 }}>Capacity</th>
                            <th style={{ padding: '10px 14px', fontWeight: 600 }}>Utilization</th>
                          </tr>
                        </thead>
                        <tbody>
                          {calculateTierCapacityBreakdown(selectedEventDetails.ticketTypes).map((tier) => (
                            <tr key={tier.ticketTypeId} style={{ borderBottom: '1px solid var(--ep-border)' }}>
                              <td style={{ padding: '10px 14px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                                {tier.ticketTypeName}
                              </td>
                              <td style={{ padding: '10px 12px' }}>{formatCount(tier.bookingsCount)}</td>
                              <td style={{ padding: '10px 12px', color: '#2E7D32', fontWeight: 600 }}>
                                {formatCount(tier.activeTickets)}
                              </td>
                              <td style={{ padding: '10px 12px', color: tier.cancelledTickets > 0 ? '#C62828' : 'var(--ep-text-secondary)' }}>
                                {formatCount(tier.cancelledTickets)}
                              </td>
                              <td style={{ padding: '10px 12px', color: 'var(--ep-text-secondary)' }}>
                                {formatCount(tier.computedCapacity)}
                              </td>
                              <td style={{ padding: '10px 14px' }}>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                  <div style={{ flex: 1, height: '6px', backgroundColor: 'var(--ep-canvas)', borderRadius: '3px', overflow: 'hidden' }}>
                                    <div
                                      style={{
                                        height: '100%',
                                        width: `${tier.activePercentage}%`,
                                        backgroundColor: 'var(--ep-primary)',
                                      }}
                                    />
                                  </div>
                                  <span style={{ fontSize: '11px', fontWeight: 600, minWidth: '32px' }}>
                                    {tier.activePercentage}%
                                  </span>
                                </div>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  ) : (
                    <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: 0 }}>
                      No tier breakdown records found for this event.
                    </p>
                  )}
                </div>
              ) : null}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
