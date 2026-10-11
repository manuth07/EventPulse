import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { useAuth } from '../../context/AuthContext';
import {
  TrendingUp,
  Ticket,
  RotateCcw,
  RefreshCw,
  AlertCircle,
  Calendar,
  Search,
  ChevronLeft,
  ChevronRight,
  DollarSign,
  Wallet,
  CheckCircle2,
  ExternalLink,
  X,
  Layers,
  ArrowRight,
  Filter,
} from 'lucide-react';
import {
  getOrganizerSalesSummary,
  getOrganizerEventsSalesList,
  getEventSalesAnalytics,
  formatSalesAnalyticsError,
} from '../../services/salesAnalyticsService';
import {
  formatCurrencyAmount,
  formatCount,
  formatMultiCurrencyBreakdown,
  calculateRevenuePercentages,
  isZeroSales,
  formatSaleDate,
} from '../../utils/salesAnalyticsDashboardUtils';

export function OrganizerSalesAnalytics() {
  const navigate = useNavigate();
  const location = useLocation();
  const { accessToken } = useAuth();

  // State: Overall Sales Summary
  const [summary, setSummary] = useState(null);
  const [summaryLoading, setSummaryLoading] = useState(true);
  const [summaryError, setSummaryError] = useState(null);

  // State: Events Sales List
  const [eventsData, setEventsData] = useState({ items: [], totalCount: 0, page: 1, pageSize: 10 });
  const [eventsLoading, setEventsLoading] = useState(true);
  const [eventsError, setEventsError] = useState(null);

  // State: Filters
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [selectedEventId, setSelectedEventId] = useState('ALL');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 10;

  // State: Event Drilldown / Modal
  const [selectedEventDetails, setSelectedEventDetails] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState(null);
  const [isModalOpen, setIsModalOpen] = useState(false);

  // State: Refreshing
  const [refreshing, setRefreshing] = useState(false);

  // Search debounce
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(searchQuery.trim());
      setCurrentPage(1);
    }, 400);
    return () => clearTimeout(timer);
  }, [searchQuery]);

  // Load overall summary
  const loadSummary = useCallback(async () => {
    setSummaryLoading(true);
    setSummaryError(null);

    try {
      const params = {};
      if (dateFrom) params.from = new Date(dateFrom).toISOString();
      if (dateTo) {
        const toDate = new Date(dateTo);
        toDate.setHours(23, 59, 59, 999);
        params.to = toDate.toISOString();
      }

      const data = await getOrganizerSalesSummary(params);
      setSummary(data);
    } catch (err) {
      setSummaryError(formatSalesAnalyticsError(err));
    } finally {
      setSummaryLoading(false);
    }
  }, [dateFrom, dateTo]);

  // Load events list
  const loadEventsList = useCallback(async () => {
    setEventsLoading(true);
    setEventsError(null);

    try {
      const params = {
        page: currentPage,
        pageSize,
      };
      if (debouncedSearch) params.search = debouncedSearch;
      if (dateFrom) params.from = new Date(dateFrom).toISOString();
      if (dateTo) {
        const toDate = new Date(dateTo);
        toDate.setHours(23, 59, 59, 999);
        params.to = toDate.toISOString();
      }

      const data = await getOrganizerEventsSalesList(params);
      setEventsData({
        items: Array.isArray(data?.items) ? data.items : [],
        totalCount: data?.totalCount || 0,
        page: data?.page || currentPage,
        pageSize: data?.pageSize || pageSize,
      });
    } catch (err) {
      setEventsError(formatSalesAnalyticsError(err));
    } finally {
      setEventsLoading(false);
    }
  }, [debouncedSearch, dateFrom, dateTo, currentPage]);

  // Unified reload
  const handleRefresh = async () => {
    setRefreshing(true);
    await Promise.all([loadSummary(), loadEventsList()]);
    setRefreshing(false);
  };

  useEffect(() => {
    loadSummary();
  }, [loadSummary]);

  useEffect(() => {
    loadEventsList();
  }, [loadEventsList]);

  // Load single event detailed breakdown
  const handleOpenEventDetail = useCallback(async (eventId) => {
    if (!eventId) return;
    setIsModalOpen(true);
    setDetailLoading(true);
    setDetailError(null);

    try {
      const params = {};
      if (dateFrom) params.from = new Date(dateFrom).toISOString();
      if (dateTo) {
        const toDate = new Date(dateTo);
        toDate.setHours(23, 59, 59, 999);
        params.to = toDate.toISOString();
      }

      const data = await getEventSalesAnalytics(eventId, params);
      setSelectedEventDetails(data);
    } catch (err) {
      setDetailError(formatSalesAnalyticsError(err));
    } finally {
      setDetailLoading(false);
    }
  }, [dateFrom, dateTo]);

  // Read query params from URL (e.g., ?eventId=xxx)
  useEffect(() => {
    const searchParams = new URLSearchParams(location.search);
    const eventIdParam = searchParams.get('eventId');
    if (eventIdParam) {
      setSelectedEventId(eventIdParam);
      handleOpenEventDetail(eventIdParam);
    }
  }, [location.search, handleOpenEventDetail]);

  const handleCloseModal = () => {
    setIsModalOpen(false);
    setSelectedEventDetails(null);
    setDetailError(null);
  };

  // Quick Date Range Presets
  const applyPreset = (preset) => {
    const now = new Date();
    if (preset === 'ALL') {
      setDateFrom('');
      setDateTo('');
    } else if (preset === '7D') {
      const past = new Date();
      past.setDate(now.getDate() - 7);
      setDateFrom(past.toISOString().split('T')[0]);
      setDateTo(now.toISOString().split('T')[0]);
    } else if (preset === '30D') {
      const past = new Date();
      past.setDate(now.getDate() - 30);
      setDateFrom(past.toISOString().split('T')[0]);
      setDateTo(now.toISOString().split('T')[0]);
    } else if (preset === 'THIS_MONTH') {
      const firstDay = new Date(now.getFullYear(), now.getMonth(), 1);
      setDateFrom(firstDay.toISOString().split('T')[0]);
      setDateTo(now.toISOString().split('T')[0]);
    }
    setCurrentPage(1);
  };

  // Filtered events when specific event selector is used
  const displayedEvents = useMemo(() => {
    if (selectedEventId === 'ALL') {
      return eventsData.items;
    }
    return eventsData.items.filter((item) => String(item.eventId) === String(selectedEventId));
  }, [eventsData.items, selectedEventId]);

  // Chart data calculations
  const chartItems = useMemo(() => {
    return calculateRevenuePercentages(eventsData.items.slice(0, 5));
  }, [eventsData.items]);

  const totalPages = Math.ceil((eventsData.totalCount || 0) / pageSize) || 1;
  const noSalesYet = isZeroSales(summary);
  const multiCurrencies = formatMultiCurrencyBreakdown(summary?.currencies);

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
              <TrendingUp size={14} color="var(--ep-primary)" />
              <span>Financial & Sales Intelligence</span>
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
              Sales Analytics
            </h1>
            <p
              style={{
                fontSize: '14px',
                color: 'var(--ep-text-secondary)',
                marginTop: '4px',
                margin: 0,
              }}
            >
              Monitor real-time revenue, ticket sales volume, and financial performance across your events.
            </p>
          </div>

          <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap', alignItems: 'center' }}>
            <button
              type="button"
              data-testid="sales-analytics-refresh-btn"
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

            {/* Quick Presets */}
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
                data-testid="sales-analytics-search-input"
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
            </div>
          </div>
        </div>

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
              data-testid="sales-analytics-retry-btn"
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

        {/* Summary Cards Grid */}
        <div
          data-testid="sales-summary-cards"
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(230px, 1fr))',
            gap: '16px',
            marginBottom: '28px',
          }}
        >
          {/* Card 1: Gross Revenue */}
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
                Gross Revenue
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
                <DollarSign size={16} color="#1565C0" />
              </div>
            </div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--ep-text-primary)', marginBottom: '4px' }}>
              {summaryLoading ? (
                <span className="spinner-border spinner-border-sm" role="status" />
              ) : (
                formatCurrencyAmount(summary?.grossRevenue, summary?.currency)
              )}
            </div>
            <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
              Total charged before refunds ({formatCount(summary?.successfulPurchaseCount || 0)} orders)
            </span>
          </div>

          {/* Card 2: Net Revenue */}
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
                Net Revenue
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
                <Wallet size={16} color="#2E7D32" />
              </div>
            </div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#2E7D32', marginBottom: '4px' }}>
              {summaryLoading ? (
                <span className="spinner-border spinner-border-sm" role="status" />
              ) : (
                formatCurrencyAmount(summary?.netRevenue, summary?.currency)
              )}
            </div>
            <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
              Gross minus completed refund adjustments
            </span>
          </div>

          {/* Card 3: Tickets Sold */}
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
                Tickets Sold
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
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--ep-text-primary)', marginBottom: '4px' }}>
              {summaryLoading ? (
                <span className="spinner-border spinner-border-sm" role="status" />
              ) : (
                formatCount(summary?.ticketsSold || 0)
              )}
            </div>
            <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
              Net active tickets: {formatCount(summary?.netTicketsSold || 0)}
            </span>
          </div>

          {/* Card 4: Refunded Amount */}
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
                Refunded Amount
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
                <RotateCcw size={16} color="#C62828" />
              </div>
            </div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#C62828', marginBottom: '4px' }}>
              {summaryLoading ? (
                <span className="spinner-border spinner-border-sm" role="status" />
              ) : (
                formatCurrencyAmount(summary?.refundedAmount, summary?.currency)
              )}
            </div>
            <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
              Total refunded to customers
            </span>
          </div>
        </div>

        {/* Multi-Currency Breakdown Banner if multiple currencies exist */}
        {multiCurrencies.length > 1 && (
          <div
            style={{
              backgroundColor: '#E8F4FD',
              border: '1px solid #BEE3F8',
              borderRadius: 'var(--ep-radius-card)',
              padding: '14px 18px',
              marginBottom: '28px',
              display: 'flex',
              flexDirection: 'column',
              gap: '8px',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <AlertCircle size={16} color="#2B6CB0" />
              <strong style={{ fontSize: '13px', color: '#2B6CB0' }}>
                Multi-Currency Sales Detected
              </strong>
            </div>
            <p style={{ fontSize: '12px', color: '#2D3748', margin: 0 }}>
              To ensure financial precision, EventPulse does not sum different currencies into a single total without currency conversion. Breakdown by currency:
            </p>
            <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap', marginTop: '4px' }}>
              {multiCurrencies.map((c) => (
                <div
                  key={c.currency}
                  style={{
                    backgroundColor: '#ffffff',
                    padding: '6px 12px',
                    borderRadius: 'var(--ep-radius-pill)',
                    fontSize: '12px',
                    fontWeight: 600,
                    color: 'var(--ep-text-primary)',
                    border: '1px solid #BEE3F8',
                  }}
                >
                  {c.label} ({c.ticketsSold} tickets)
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Visual Charts Section (EP-385 Step 6) */}
        {!noSalesYet && chartItems.length > 0 && (
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
              <h2 style={{ fontSize: '16px', fontWeight: 700, color: 'var(--ep-text-primary)', margin: 0 }}>
                Revenue Distribution by Event
              </h2>
              <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', marginTop: '4px', margin: 0 }}>
                Top performing events ranked by gross revenue contribution
              </p>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              {chartItems.map((evt) => (
                <div key={evt.eventId} style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: '13px' }}>
                    <span style={{ fontWeight: 600, color: 'var(--ep-text-primary)' }}>{evt.eventName}</span>
                    <span style={{ fontWeight: 600, color: 'var(--ep-text-secondary)' }}>
                      {formatCurrencyAmount(evt.grossRevenue, evt.currency)} ({evt.percentage}%)
                    </span>
                  </div>
                  {/* Visual Bar */}
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

        {/* Per-Event Sales Performance Table */}
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
                Event Sales Performance
              </h2>
              <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', marginTop: '4px', margin: 0 }}>
                Detailed revenue and sales figures per event. Click any event to inspect ticket type breakdowns.
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
                <span className="visually-hidden">Loading sales data...</span>
              </div>
              <p style={{ marginTop: '12px', fontSize: '14px' }}>Loading event sales records...</p>
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
                Retry Loading Events
              </button>
            </div>
          ) : displayedEvents.length === 0 ? (
            /* Empty State */
            <div
              style={{
                padding: '64px 24px',
                textAlign: 'center',
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                gap: '12px',
              }}
            >
              <div
                style={{
                  width: '56px',
                  height: '56px',
                  borderRadius: 'var(--ep-radius-pill)',
                  backgroundColor: '#FFF0E6',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                }}
              >
                <Ticket size={24} color="var(--ep-primary)" />
              </div>
              <h3 style={{ fontSize: '18px', fontWeight: 600, color: 'var(--ep-text-primary)', margin: 0 }}>
                No sales data is available for your events yet.
              </h3>
              <p style={{ fontSize: '14px', color: 'var(--ep-text-secondary)', maxWidth: '440px', margin: 0 }}>
                {debouncedSearch
                  ? `No events found matching "${debouncedSearch}". Try clearing your search filter.`
                  : 'Once customers begin purchasing tickets for your published events, their transactions and real-time revenue performance will be displayed here.'}
              </p>
              {debouncedSearch ? (
                <button
                  type="button"
                  onClick={() => setSearchQuery('')}
                  style={{
                    marginTop: '8px',
                    padding: '8px 16px',
                    backgroundColor: 'transparent',
                    border: '1px solid var(--ep-border)',
                    borderRadius: 'var(--ep-radius-btn)',
                    fontSize: '13px',
                    fontWeight: 600,
                    cursor: 'pointer',
                  }}
                >
                  Clear Search
                </button>
              ) : (
                <Link
                  to="/events/create"
                  className="ep-btn-primary"
                  style={{
                    marginTop: '8px',
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '6px',
                    padding: '10px 18px',
                    fontSize: '13px',
                    fontWeight: 600,
                    borderRadius: 'var(--ep-radius-btn)',
                    textDecoration: 'none',
                  }}
                >
                  Create New Event
                </Link>
              )}
            </div>
          ) : (
            /* Table Data */
            <div style={{ overflowX: 'auto' }}>
              <table
                style={{
                  width: '100%',
                  borderCollapse: 'collapse',
                  textAlign: 'left',
                  fontSize: '13px',
                }}
              >
                <thead>
                  <tr
                    style={{
                      borderBottom: '1px solid var(--ep-border)',
                      backgroundColor: 'var(--ep-canvas)',
                      color: 'var(--ep-text-secondary)',
                      fontWeight: 600,
                    }}
                  >
                    <th style={{ padding: '14px 20px' }}>Event Name</th>
                    <th style={{ padding: '14px 16px' }}>Tickets Sold</th>
                    <th style={{ padding: '14px 16px' }}>Gross Revenue</th>
                    <th style={{ padding: '14px 16px' }}>Refunded</th>
                    <th style={{ padding: '14px 16px' }}>Net Revenue</th>
                    <th style={{ padding: '14px 16px' }}>Last Sale</th>
                    <th style={{ padding: '14px 20px', textAlign: 'right' }}>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {displayedEvents.map((item) => (
                    <tr
                      key={item.eventId}
                      style={{
                        borderBottom: '1px solid var(--ep-border)',
                        transition: 'background-color 0.15s ease',
                      }}
                      onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = 'var(--ep-canvas)')}
                      onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = 'transparent')}
                    >
                      <td style={{ padding: '16px 20px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                        {item.eventName}
                      </td>
                      <td style={{ padding: '16px 16px', color: 'var(--ep-text-primary)' }}>
                        <div>{formatCount(item.ticketsSold)}</div>
                        <div style={{ fontSize: '11px', color: 'var(--ep-text-secondary)' }}>
                          Net: {formatCount(item.netTicketsSold)}
                        </div>
                      </td>
                      <td style={{ padding: '16px 16px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                        {formatCurrencyAmount(item.grossRevenue, item.currency)}
                      </td>
                      <td style={{ padding: '16px 16px', color: item.refundedAmount > 0 ? '#C62828' : 'var(--ep-text-secondary)' }}>
                        {formatCurrencyAmount(item.refundedAmount, item.currency)}
                      </td>
                      <td style={{ padding: '16px 16px', fontWeight: 700, color: '#2E7D32' }}>
                        {formatCurrencyAmount(item.netRevenue, item.currency)}
                      </td>
                      <td style={{ padding: '16px 16px', color: 'var(--ep-text-secondary)', fontSize: '12px' }}>
                        {formatSaleDate(item.lastSaleAtUtc)}
                      </td>
                      <td style={{ padding: '16px 20px', textAlign: 'right' }}>
                        <button
                          type="button"
                          onClick={() => handleOpenEventDetail(item.eventId)}
                          style={{
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: '4px',
                            backgroundColor: 'transparent',
                            border: '1px solid var(--ep-border)',
                            borderRadius: 'var(--ep-radius-btn)',
                            padding: '6px 12px',
                            fontSize: '12px',
                            fontWeight: 600,
                            color: 'var(--ep-primary)',
                            cursor: 'pointer',
                          }}
                        >
                          <span>Breakdown</span>
                          <ArrowRight size={12} />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* Pagination Footer */}
          {totalPages > 1 && (
            <div
              style={{
                padding: '14px 20px',
                borderTop: '1px solid var(--ep-border)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                backgroundColor: '#ffffff',
              }}
            >
              <span style={{ fontSize: '13px', color: 'var(--ep-text-secondary)' }}>
                Page {currentPage} of {totalPages}
              </span>
              <div style={{ display: 'flex', gap: '8px' }}>
                <button
                  type="button"
                  disabled={currentPage <= 1 || eventsLoading}
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
                    color: currentPage <= 1 ? 'var(--ep-text-disabled)' : 'var(--ep-text-primary)',
                  }}
                >
                  <ChevronLeft size={14} />
                  <span>Previous</span>
                </button>
                <button
                  type="button"
                  disabled={currentPage >= totalPages || eventsLoading}
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
                    color: currentPage >= totalPages ? 'var(--ep-text-disabled)' : 'var(--ep-text-primary)',
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

      {/* Event Detailed Breakdown Modal */}
      {isModalOpen && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Event Sales Breakdown"
          style={{
            position: 'fixed',
            inset: 0,
            backgroundColor: 'rgba(0, 0, 0, 0.45)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1050,
            padding: '16px',
          }}
        >
          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              width: '100%',
              maxWidth: '680px',
              maxHeight: '90vh',
              overflowY: 'auto',
              boxShadow: 'var(--ep-shadow-modal)',
              padding: '28px',
              position: 'relative',
            }}
          >
            {/* Modal Header */}
            <div
              style={{
                display: 'flex',
                alignItems: 'flex-start',
                justifyContent: 'space-between',
                marginBottom: '20px',
              }}
            >
              <div>
                <span
                  style={{
                    fontSize: '11px',
                    fontWeight: 700,
                    letterSpacing: '0.05em',
                    color: 'var(--ep-primary)',
                    textTransform: 'uppercase',
                  }}
                >
                  Ticket Tier Breakdown
                </span>
                <h3
                  style={{
                    fontSize: '20px',
                    fontWeight: 700,
                    color: 'var(--ep-text-primary)',
                    margin: '4px 0 0 0',
                  }}
                >
                  {selectedEventDetails?.eventName || 'Event Details'}
                </h3>
              </div>
              <button
                type="button"
                onClick={handleCloseModal}
                style={{
                  background: 'none',
                  border: 'none',
                  color: 'var(--ep-text-secondary)',
                  cursor: 'pointer',
                  padding: '4px',
                }}
              >
                <X size={20} />
              </button>
            </div>

            {/* Modal Content */}
            {detailLoading ? (
              <div style={{ textAlign: 'center', padding: '40px 0' }}>
                <div className="spinner-border text-primary" role="status" />
                <p style={{ marginTop: '12px', fontSize: '13px', color: 'var(--ep-text-secondary)' }}>
                  Loading ticket tier breakdown...
                </p>
              </div>
            ) : detailError ? (
              <div
                style={{
                  backgroundColor: '#FFEBEE',
                  color: '#C62828',
                  padding: '14px',
                  borderRadius: 'var(--ep-radius-btn)',
                  fontSize: '13px',
                }}
              >
                {detailError}
              </div>
            ) : selectedEventDetails ? (
              <div>
                {/* Event Summary Snapshot */}
                <div
                  style={{
                    display: 'grid',
                    gridTemplateColumns: 'repeat(3, 1fr)',
                    gap: '12px',
                    marginBottom: '24px',
                    backgroundColor: 'var(--ep-canvas)',
                    padding: '14px',
                    borderRadius: 'var(--ep-radius-btn)',
                  }}
                >
                  <div>
                    <span style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', fontWeight: 600 }}>
                      GROSS REVENUE
                    </span>
                    <div style={{ fontSize: '16px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                      {formatCurrencyAmount(selectedEventDetails.grossRevenue, selectedEventDetails.currency)}
                    </div>
                  </div>
                  <div>
                    <span style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', fontWeight: 600 }}>
                      NET REVENUE
                    </span>
                    <div style={{ fontSize: '16px', fontWeight: 700, color: '#2E7D32' }}>
                      {formatCurrencyAmount(selectedEventDetails.netRevenue, selectedEventDetails.currency)}
                    </div>
                  </div>
                  <div>
                    <span style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', fontWeight: 600 }}>
                      TICKETS SOLD
                    </span>
                    <div style={{ fontSize: '16px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                      {formatCount(selectedEventDetails.ticketsSold)}
                    </div>
                  </div>
                </div>

                {/* Ticket Types Breakdown Table */}
                <h4 style={{ fontSize: '14px', fontWeight: 700, color: 'var(--ep-text-primary)', marginBottom: '12px' }}>
                  Sales by Ticket Type
                </h4>

                {selectedEventDetails.ticketTypes?.length === 0 ? (
                  <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', fontStyle: 'italic' }}>
                    No ticket types have recorded sales for this event yet.
                  </p>
                ) : (
                  <div style={{ overflowX: 'auto' }}>
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px' }}>
                      <thead>
                        <tr style={{ borderBottom: '1px solid var(--ep-border)', color: 'var(--ep-text-secondary)' }}>
                          <th style={{ padding: '8px 6px', textAlign: 'left' }}>Ticket Type</th>
                          <th style={{ padding: '8px 6px', textAlign: 'right' }}>Sold</th>
                          <th style={{ padding: '8px 6px', textAlign: 'right' }}>Unit Price</th>
                          <th style={{ padding: '8px 6px', textAlign: 'right' }}>Gross</th>
                          <th style={{ padding: '8px 6px', textAlign: 'right' }}>Refunds</th>
                          <th style={{ padding: '8px 6px', textAlign: 'right' }}>Net</th>
                        </tr>
                      </thead>
                      <tbody>
                        {selectedEventDetails.ticketTypes.map((t) => (
                          <tr key={t.ticketTypeId} style={{ borderBottom: '1px solid var(--ep-border)' }}>
                            <td style={{ padding: '10px 6px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                              {t.ticketTypeName}
                            </td>
                            <td style={{ padding: '10px 6px', textAlign: 'right' }}>
                              {formatCount(t.quantitySold)}
                              {t.refundedQuantity > 0 && (
                                <span style={{ color: '#C62828', fontSize: '11px' }}> (-{t.refundedQuantity})</span>
                              )}
                            </td>
                            <td style={{ padding: '10px 6px', textAlign: 'right', color: 'var(--ep-text-secondary)' }}>
                              {formatCurrencyAmount(t.unitPrice, selectedEventDetails.currency)}
                            </td>
                            <td style={{ padding: '10px 6px', textAlign: 'right', fontWeight: 600 }}>
                              {formatCurrencyAmount(t.grossRevenue, selectedEventDetails.currency)}
                            </td>
                            <td style={{ padding: '10px 6px', textAlign: 'right', color: t.refundedAmount > 0 ? '#C62828' : 'var(--ep-text-secondary)' }}>
                              {formatCurrencyAmount(t.refundedAmount, selectedEventDetails.currency)}
                            </td>
                            <td style={{ padding: '10px 6px', textAlign: 'right', fontWeight: 700, color: '#2E7D32' }}>
                              {formatCurrencyAmount(t.netRevenue, selectedEventDetails.currency)}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            ) : null}

            {/* Modal Footer */}
            <div style={{ marginTop: '24px', textAlign: 'right' }}>
              <button
                type="button"
                onClick={handleCloseModal}
                style={{
                  backgroundColor: 'var(--ep-canvas)',
                  border: '1px solid var(--ep-border)',
                  borderRadius: 'var(--ep-radius-btn)',
                  padding: '8px 18px',
                  fontSize: '13px',
                  fontWeight: 600,
                  color: 'var(--ep-text-primary)',
                  cursor: 'pointer',
                }}
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
