import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { Link } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import {
  ShieldCheck,
  Filter,
  RefreshCw,
  AlertCircle,
  FileText,
  Wallet,
  Ticket,
  Users,
  Layers,
  ArrowLeft,
  TrendingUp,
  Percent,
  CheckCircle2,
  XCircle,
  Clock,
  Briefcase,
} from 'lucide-react';
import {
  generateDynamicReport,
  formatReportApiError,
} from '../../services/adminReportsService';
import {
  SUPPORTED_REPORT_TYPES,
  validateReportCriteria,
  formatReportCount,
  formatReportRate,
  formatReportCurrency,
  formatReportingPeriodLabel,
  isReportDataEmpty,
} from '../../utils/adminReportsUtils';
import { ReportFilterPanel } from '../../components/common/ReportFilterPanel';
import { getDefaultFilters } from '../../utils/reportFilterUtils';
import { fetchPublishedEvents } from '../../services/eventService';
import { getPublicTicketTypes } from '../../services/ticketTypeService';

export function AdminReports() {
  // Criteria State
  const [reportType, setReportType] = useState('platform-summary');
  const [activePreset, setActivePreset] = useState('allTime');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');
  const [category, setCategory] = useState('All');
  const [bookingStatus, setBookingStatus] = useState('All');
  const [currency, setCurrency] = useState('ALL');
  const [eventId, setEventId] = useState('');
  const [ticketTypeId, setTicketTypeId] = useState('');
  const [organizerId, setOrganizerId] = useState('');

  // Dynamic Options for Event & Ticket Tier Dropdowns
  const [eventsList, setEventsList] = useState([]);
  const [ticketTypesList, setTicketTypesList] = useState([]);

  // Load published events on mount
  useEffect(() => {
    let isMounted = true;
    async function loadEvents() {
      try {
        const events = await fetchPublishedEvents();
        if (isMounted && Array.isArray(events)) {
          setEventsList(events);
        }
      } catch (err) {
        // Fallback: eventsList remains empty, freeform input is available
      }
    }
    loadEvents();
    return () => {
      isMounted = false;
    };
  }, []);

  // Load ticket types when event changes
  useEffect(() => {
    let isMounted = true;
    async function loadTicketTypes() {
      if (!eventId || eventId === 'ALL') {
        setTicketTypesList([]);
        return;
      }
      try {
        const tiers = await getPublicTicketTypes(eventId);
        if (isMounted && Array.isArray(tiers)) {
          setTicketTypesList(tiers);
        }
      } catch (err) {
        if (isMounted) setTicketTypesList([]);
      }
    }
    loadTicketTypes();
    return () => {
      isMounted = false;
    };
  }, [eventId]);

  const eventLabels = useMemo(() => {
    const map = {};
    eventsList.forEach((e) => {
      if (e.id) map[e.id] = e.title || e.name || e.id;
    });
    return map;
  }, [eventsList]);

  const ticketTypeLabels = useMemo(() => {
    const map = {};
    ticketTypesList.forEach((t) => {
      const id = t.id || t.ticketTypeId;
      if (id) map[id] = t.name || t.tierName || id;
    });
    return map;
  }, [ticketTypesList]);

  // Generation & Results State
  const [loading, setLoading] = useState(false);
  const [validationError, setValidationError] = useState(null);
  const [apiError, setApiError] = useState(null);
  const [reportResult, setReportResult] = useState(null);
  const [appliedCriteria, setAppliedCriteria] = useState(null);
  const [generatedAt, setGeneratedAt] = useState(null);

  // Handler when filters change in ReportFilterPanel
  const handleFilterPanelChange = (next) => {
    setDateFrom(next.from);
    setDateTo(next.to);
    setActivePreset(next.activePreset);
    setCategory(next.category);
    setEventId(next.eventId);
    setTicketTypeId(next.ticketTypeId);
    setBookingStatus(next.bookingStatus);
    setCurrency(next.currency);
    setOrganizerId(next.organizerId);
    setValidationError(null);
  };

  // Clear all filters back to default
  const handleClearFilters = () => {
    const defaults = getDefaultFilters('Administrator');
    setDateFrom(defaults.from);
    setDateTo(defaults.to);
    setActivePreset(defaults.activePreset);
    setCategory(defaults.category);
    setEventId(defaults.eventId);
    setTicketTypeId(defaults.ticketTypeId);
    setBookingStatus(defaults.bookingStatus);
    setCurrency(defaults.currency);
    setOrganizerId(defaults.organizerId);
    setTicketTypesList([]);
    setValidationError(null);

    handleGenerateReport({
      dateFrom: defaults.from,
      dateTo: defaults.to,
      activePreset: defaults.activePreset,
      category: defaults.category,
      eventId: defaults.eventId,
      ticketTypeId: defaults.ticketTypeId,
      bookingStatus: defaults.bookingStatus,
      currency: defaults.currency,
      organizerId: defaults.organizerId,
    });
  };

  // Fetch Report Data
  const handleGenerateReport = useCallback(async (customOverrides = {}) => {
    const targetType = customOverrides.reportType || reportType;
    const targetFrom = customOverrides.dateFrom !== undefined ? customOverrides.dateFrom : dateFrom;
    const targetTo = customOverrides.dateTo !== undefined ? customOverrides.dateTo : dateTo;
    const targetPreset = customOverrides.activePreset !== undefined ? customOverrides.activePreset : activePreset;
    const targetStatus = customOverrides.bookingStatus !== undefined ? customOverrides.bookingStatus : bookingStatus;
    const targetCurrency = customOverrides.currency !== undefined ? customOverrides.currency : currency;
    const targetCategory = customOverrides.category !== undefined ? customOverrides.category : category;
    const targetEventId = customOverrides.eventId !== undefined ? customOverrides.eventId : eventId;
    const targetTicketTypeId = customOverrides.ticketTypeId !== undefined ? customOverrides.ticketTypeId : ticketTypeId;
    const targetOrganizerId = customOverrides.organizerId !== undefined ? customOverrides.organizerId : organizerId;

    // Client-side criteria validation
    const validation = validateReportCriteria({
      reportType: targetType,
      from: targetFrom,
      to: targetTo,
      currency: targetCurrency,
      category: targetCategory,
      eventId: targetEventId,
      ticketTypeId: targetTicketTypeId,
      organizerId: targetOrganizerId,
    }, 'Administrator');

    if (!validation.isValid) {
      setValidationError(validation.error);
      return;
    }

    // Build criteria payload
    const criteria = {
      reportType: targetType,
      from: targetFrom && !isNaN(new Date(targetFrom).getTime()) ? new Date(targetFrom).toISOString() : undefined,
      to: targetTo && !isNaN(new Date(targetTo).getTime()) ? (() => {
        const d = new Date(targetTo);
        d.setHours(23, 59, 59, 999);
        return d.toISOString();
      })() : undefined,
    };

    if (targetStatus && targetStatus !== 'All') {
      criteria.bookingStatus = targetStatus;
    }

    if (targetCurrency && targetCurrency !== 'ALL') {
      criteria.currency = targetCurrency;
    }

    if (targetCategory && targetCategory !== 'All') {
      criteria.category = targetCategory;
    }

    if (targetEventId && targetEventId.trim() && targetEventId !== 'ALL') {
      criteria.eventId = targetEventId.trim();
    }

    if (targetTicketTypeId && targetTicketTypeId.trim() && targetTicketTypeId !== 'ALL') {
      criteria.ticketTypeId = targetTicketTypeId.trim();
    }

    if (targetOrganizerId && targetOrganizerId.trim()) {
      criteria.organizerId = targetOrganizerId.trim();
    }

    setValidationError(null);
    setApiError(null);
    setLoading(true);

    try {
      const response = await generateDynamicReport(criteria);
      const data = response?.data !== undefined ? response.data : response;
      const genTime = response?.generatedAtUtc || new Date().toISOString();

      setReportResult(data);
      setGeneratedAt(genTime);
      setAppliedCriteria({
        reportType: targetType,
        from: targetFrom,
        to: targetTo,
        activePreset: targetPreset,
        bookingStatus: targetStatus,
        currency: targetCurrency,
        category: targetCategory,
        eventId: targetEventId,
        ticketTypeId: targetTicketTypeId,
        organizerId: targetOrganizerId,
      });
    } catch (err) {
      setApiError(formatReportApiError(err));
      setReportResult(null);
    } finally {
      setLoading(false);
    }
  }, [reportType, dateFrom, dateTo, activePreset, bookingStatus, currency, category, eventId, ticketTypeId, organizerId]);

  // Reset dates to all time and regenerate
  const handleClearDates = () => {
    setDateFrom('');
    setDateTo('');
    setActivePreset('allTime');
    setValidationError(null);

    handleGenerateReport({
      dateFrom: '',
      dateTo: '',
      activePreset: 'allTime',
    });
  };

  // Initial load: Generate default summary report on mount
  useEffect(() => {
    handleGenerateReport();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Check if current result is empty
  const isDataEmpty = reportResult ? isReportDataEmpty(appliedCriteria?.reportType || reportType, reportResult) : false;

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
          paddingTop: '32px',
          paddingBottom: '64px',
        }}
      >
        {/* Navigation & Header Section */}
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
              <ShieldCheck size={14} color="var(--ep-primary)" />
              <span>Platform Administration</span>
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
              Dynamic Reports
            </h1>
            <p
              style={{
                fontSize: '14px',
                color: 'var(--ep-text-secondary)',
                marginTop: '4px',
                margin: 0,
              }}
            >
              Generate, filter, and analyze operational, booking, and revenue reports across the platform.
            </p>
          </div>

          <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap', alignItems: 'center' }}>
            <Link
              to="/admin"
              className="ep-btn-secondary"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                padding: '10px 16px',
                fontSize: '13px',
                fontWeight: 600,
                textDecoration: 'none',
              }}
            >
              <ArrowLeft size={14} />
              <span>Back to Admin Dashboard</span>
            </Link>

            <button
              type="button"
              data-testid="admin-reports-refresh-btn"
              onClick={() => handleGenerateReport()}
              disabled={loading}
              className="ep-btn-secondary"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                padding: '10px 16px',
                fontSize: '13px',
                fontWeight: 600,
              }}
            >
              <RefreshCw size={14} className={loading ? 'spin' : ''} />
              <span>{loading ? 'Generating...' : 'Refresh Report'}</span>
            </button>
          </div>
        </div>

        {/* Report Filter & Criteria Card */}
        <section
          aria-labelledby="reports-criteria-heading"
          style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            padding: '24px',
            marginBottom: '24px',
            boxShadow: 'var(--ep-shadow-card)',
          }}
        >
          <h2
            id="reports-criteria-heading"
            style={{
              fontSize: '16px',
              fontWeight: 700,
              color: 'var(--ep-text-primary)',
              margin: '0 0 16px 0',
              display: 'flex',
              alignItems: 'center',
              gap: '8px',
            }}
          >
            <Filter size={18} color="var(--ep-primary)" />
            <span>Select Report Criteria</span>
          </h2>

          {/* Report Type Selector Pills */}
          <div style={{ marginBottom: '20px' }}>
            <label
              style={{
                display: 'block',
                fontSize: '13px',
                fontWeight: 600,
                color: 'var(--ep-text-primary)',
                marginBottom: '8px',
              }}
            >
              Report Type:
            </label>
            <div
              style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))',
                gap: '12px',
              }}
            >
              {SUPPORTED_REPORT_TYPES.map((type) => {
                const isSelected = reportType === type.id;
                return (
                  <button
                    key={type.id}
                    type="button"
                    data-testid={`report-type-${type.id}`}
                    onClick={() => {
                      setReportType(type.id);
                      setValidationError(null);
                    }}
                    style={{
                      display: 'flex',
                      flexDirection: 'column',
                      alignItems: 'flex-start',
                      padding: '14px 16px',
                      borderRadius: 'var(--ep-radius-btn)',
                      border: isSelected
                        ? '2px solid var(--ep-primary)'
                        : '1px solid var(--ep-border)',
                      backgroundColor: isSelected ? 'rgba(255, 90, 39, 0.04)' : '#ffffff',
                      cursor: 'pointer',
                      textAlign: 'left',
                      transition: 'all 0.15s ease',
                    }}
                  >
                    <div
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        width: '100%',
                        marginBottom: '4px',
                      }}
                    >
                      <span
                        style={{
                          fontSize: '14px',
                          fontWeight: 700,
                          color: isSelected ? 'var(--ep-primary)' : 'var(--ep-text-primary)',
                        }}
                      >
                        {type.label}
                      </span>
                      {isSelected && (
                        <span
                          style={{
                            fontSize: '11px',
                            fontWeight: 700,
                            backgroundColor: 'var(--ep-primary)',
                            color: '#ffffff',
                            padding: '2px 8px',
                            borderRadius: 'var(--ep-radius-pill)',
                          }}
                        >
                          Active
                        </span>
                      )}
                    </div>
                    <span
                      style={{
                        fontSize: '12px',
                        color: 'var(--ep-text-secondary)',
                        lineHeight: 1.4,
                      }}
                    >
                      {type.description}
                    </span>
                  </button>
                );
              })}
            </div>
          </div>

          {/* Reusable Filter Controls Panel */}
          <div style={{ borderTop: '1px solid var(--ep-border)', paddingTop: '20px' }}>
            <ReportFilterPanel
              role="Administrator"
              title="Report Filter Controls"
              filters={{
                from: dateFrom,
                to: dateTo,
                activePreset,
                category,
                eventId,
                ticketTypeId,
                bookingStatus,
                currency,
                organizerId,
              }}
              eventsList={eventsList}
              ticketTypesList={ticketTypesList}
              eventLabels={eventLabels}
              ticketTypeLabels={ticketTypeLabels}
              onFilterChange={handleFilterPanelChange}
              onApply={() => handleGenerateReport()}
              onClear={handleClearFilters}
              loading={loading}
              validationError={validationError}
              showCategory={true}
              showBookingStatus={reportType === 'bookings' || reportType === 'platform-summary'}
              showCurrency={reportType === 'revenue' || reportType === 'platform-summary'}
              showOrganizer={true}
              applyButtonLabel="Generate Report"
              applyButtonTestId="generate-report-submit-btn"
            />
          </div>
        </section>

        {/* Applied Filters & Reporting Period Banner */}
        {appliedCriteria && (
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              flexWrap: 'wrap',
              gap: '12px',
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              padding: '12px 18px',
              marginBottom: '24px',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
              <span
                style={{
                  fontSize: '11px',
                  fontWeight: 700,
                  textTransform: 'uppercase',
                  letterSpacing: '0.05em',
                  color: 'var(--ep-text-secondary)',
                }}
              >
                Reporting Scope:
              </span>
              <span
                style={{
                  fontSize: '13px',
                  fontWeight: 600,
                  color: 'var(--ep-text-primary)',
                  backgroundColor: 'var(--ep-canvas)',
                  padding: '4px 10px',
                  borderRadius: 'var(--ep-radius-pill)',
                  border: '1px solid var(--ep-border)',
                }}
              >
                {formatReportingPeriodLabel({ from: appliedCriteria.from, to: appliedCriteria.to })}
              </span>

              {appliedCriteria.category && appliedCriteria.category !== 'All' && (
                <span
                  style={{
                    fontSize: '12px',
                    fontWeight: 600,
                    color: 'var(--ep-primary)',
                    backgroundColor: 'rgba(255, 90, 39, 0.08)',
                    padding: '4px 10px',
                    borderRadius: 'var(--ep-radius-pill)',
                  }}
                >
                  Category: {appliedCriteria.category}
                </span>
              )}

              {appliedCriteria.eventId && (
                <span
                  style={{
                    fontSize: '12px',
                    fontWeight: 600,
                    color: 'var(--ep-primary)',
                    backgroundColor: 'rgba(255, 90, 39, 0.08)',
                    padding: '4px 10px',
                    borderRadius: 'var(--ep-radius-pill)',
                  }}
                >
                  Event: {appliedCriteria.eventId}
                </span>
              )}

              {appliedCriteria.ticketTypeId && (
                <span
                  style={{
                    fontSize: '12px',
                    fontWeight: 600,
                    color: 'var(--ep-primary)',
                    backgroundColor: 'rgba(255, 90, 39, 0.08)',
                    padding: '4px 10px',
                    borderRadius: 'var(--ep-radius-pill)',
                  }}
                >
                  Ticket Tier: {appliedCriteria.ticketTypeId}
                </span>
              )}

              {appliedCriteria.bookingStatus && appliedCriteria.bookingStatus !== 'All' && (
                <span
                  style={{
                    fontSize: '12px',
                    fontWeight: 600,
                    color: 'var(--ep-primary)',
                    backgroundColor: 'rgba(255, 90, 39, 0.08)',
                    padding: '4px 10px',
                    borderRadius: 'var(--ep-radius-pill)',
                  }}
                >
                  Status: {appliedCriteria.bookingStatus}
                </span>
              )}

              {appliedCriteria.currency && appliedCriteria.currency !== 'ALL' && (
                <span
                  style={{
                    fontSize: '12px',
                    fontWeight: 600,
                    color: 'var(--ep-primary)',
                    backgroundColor: 'rgba(255, 90, 39, 0.08)',
                    padding: '4px 10px',
                    borderRadius: 'var(--ep-radius-pill)',
                  }}
                >
                  Currency: {appliedCriteria.currency}
                </span>
              )}

              {appliedCriteria.organizerId && (
                <span
                  style={{
                    fontSize: '12px',
                    fontWeight: 600,
                    color: 'var(--ep-primary)',
                    backgroundColor: 'rgba(255, 90, 39, 0.08)',
                    padding: '4px 10px',
                    borderRadius: 'var(--ep-radius-pill)',
                  }}
                >
                  Organizer: {appliedCriteria.organizerId}
                </span>
              )}
            </div>

            {generatedAt && (
              <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                Generated: {new Date(generatedAt).toLocaleString()}
              </span>
            )}
          </div>
        )}

        {/* UI State 1: Loading Skeleton */}
        {loading && (
          <div
            data-testid="reports-loading-state"
            aria-live="polite"
            style={{
              padding: '48px 24px',
              textAlign: 'center',
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              gap: '16px',
            }}
          >
            <RefreshCw size={36} color="var(--ep-primary)" className="spin" />
            <div>
              <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 6px 0', color: 'var(--ep-text-primary)' }}>
                Aggregating Report Metrics
              </h3>
              <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: 0 }}>
                Querying database clusters and calculating authoritative reporting figures...
              </p>
            </div>
          </div>
        )}

        {/* UI State 2: Error State */}
        {!loading && apiError && (
          <div
            data-testid="reports-error-state"
            role="alert"
            style={{
              padding: '24px',
              backgroundColor: '#fef2f2',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid #fecaca',
              display: 'flex',
              flexDirection: 'column',
              gap: '12px',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
              <AlertCircle size={22} color="#dc2626" />
              <h3 style={{ fontSize: '16px', fontWeight: 700, color: '#991b1b', margin: 0 }}>
                Report Generation Failed
              </h3>
            </div>
            <p style={{ fontSize: '14px', color: '#7f1d1d', margin: 0 }}>
              {apiError}
            </p>
            <div style={{ display: 'flex', gap: '12px', marginTop: '6px' }}>
              <button
                type="button"
                onClick={() => handleGenerateReport()}
                className="ep-btn-primary"
                style={{ padding: '8px 16px', fontSize: '13px' }}
              >
                Retry Generation
              </button>
              <button
                type="button"
                onClick={handleClearDates}
                className="ep-btn-secondary"
                style={{ padding: '8px 16px', fontSize: '13px' }}
              >
                Reset Dates to All Time
              </button>
            </div>
          </div>
        )}

        {/* UI State 3: Empty State */}
        {!loading && !apiError && isDataEmpty && (
          <div
            data-testid="reports-empty-state"
            style={{
              padding: '48px 24px',
              textAlign: 'center',
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
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
                borderRadius: '50%',
                backgroundColor: 'rgba(255, 90, 39, 0.08)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              <FileText size={26} color="var(--ep-primary)" />
            </div>
            <h3 style={{ fontSize: '17px', fontWeight: 700, color: 'var(--ep-text-primary)', margin: 0 }}>
              No Report Data Found
            </h3>
            <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', maxWidth: '420px', margin: 0 }}>
              There are no matching booking or transaction records for the selected criteria and date period.
              Try broadening your date filter or selecting all statuses.
            </p>
            <button
              type="button"
              onClick={handleClearDates}
              className="ep-btn-secondary"
              style={{ marginTop: '8px', padding: '8px 16px', fontSize: '13px' }}
            >
              Reset to All Time
            </button>
          </div>
        )}

        {/* UI State 4: Successful Results */}
        {!loading && !apiError && !isDataEmpty && reportResult && (
          <div data-testid="reports-result-content" style={{ display: 'flex', flexDirection: 'column', gap: '28px' }}>
            {/* View A: Platform Summary Report */}
            {appliedCriteria?.reportType === 'platform-summary' && (
              <PlatformSummaryReportView data={reportResult} />
            )}

            {/* View B: Bookings Report */}
            {appliedCriteria?.reportType === 'bookings' && (
              <BookingsReportView data={reportResult} />
            )}

            {/* View C: Revenue Report */}
            {appliedCriteria?.reportType === 'revenue' && (
              <RevenueReportView data={reportResult} />
            )}
          </div>
        )}
      </main>
    </div>
  );
}

/**
 * Metric Card Component
 */
function MetricCard({ title, value, subtitle, icon: Icon, color = 'var(--ep-primary)' }) {
  return (
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
          {title}
        </span>
        {Icon && (
          <div
            style={{
              padding: '6px',
              borderRadius: 'var(--ep-radius-btn)',
              backgroundColor: 'rgba(255, 90, 39, 0.08)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <Icon size={16} color={color} />
          </div>
        )}
      </div>
      <div>
        <div style={{ fontSize: '24px', fontWeight: 800, color: 'var(--ep-text-primary)', letterSpacing: '-0.02em' }}>
          {value}
        </div>
        {subtitle && (
          <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', marginTop: '4px' }}>
            {subtitle}
          </div>
        )}
      </div>
    </div>
  );
}

/**
 * Platform Summary Report Subview
 */
function PlatformSummaryReportView({ data }) {
  const currencyBreakdown = Array.isArray(data.currencyBreakdown) ? data.currencyBreakdown : [];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '24px' }}>
      {/* Primary KPI Grid */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
          gap: '16px',
        }}
      >
        <MetricCard
          title="Total Bookings"
          value={formatReportCount(data.totalBookings)}
          subtitle={`${formatReportCount(data.confirmedBookings)} Confirmed • ${formatReportCount(data.cancelledBookings)} Cancelled`}
          icon={Ticket}
        />
        <MetricCard
          title="Gross Revenue"
          value={formatReportCurrency(data.primaryGrossRevenue, data.primaryCurrency)}
          subtitle={`Primary currency (${data.primaryCurrency || 'LKR'})`}
          icon={Wallet}
        />
        <MetricCard
          title="Net Revenue"
          value={formatReportCurrency(data.primaryNetRevenue, data.primaryCurrency)}
          subtitle={`After ${formatReportCurrency(data.primaryRefundedAmount, data.primaryCurrency)} refunds`}
          icon={TrendingUp}
        />
        <MetricCard
          title="Unique Customers"
          value={formatReportCount(data.totalUniqueCustomers)}
          subtitle="Registered platform buyers"
          icon={Users}
        />
        <MetricCard
          title="Active Events"
          value={formatReportCount(data.totalActiveEvents)}
          subtitle="Events with active inventory"
          icon={Layers}
        />
        <MetricCard
          title="Booking Churn Rate"
          value={formatReportRate(data.cancellationRate)}
          subtitle={`${formatReportRate(data.ticketCancellationRate)} of total tickets`}
          icon={Percent}
        />
      </div>

      {/* Multi-Currency Financial Overview */}
      {currencyBreakdown.length > 0 && (
        <section
          style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            padding: '20px',
            boxShadow: 'var(--ep-shadow-card)',
          }}
        >
          <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 16px 0', color: 'var(--ep-text-primary)' }}>
            Financial Performance by Currency
          </h3>
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))',
              gap: '14px',
            }}
          >
            {currencyBreakdown.map((curr) => (
              <div
                key={curr.currency}
                style={{
                  padding: '16px',
                  borderRadius: 'var(--ep-radius-btn)',
                  backgroundColor: 'var(--ep-canvas)',
                  border: '1px solid var(--ep-border)',
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '10px' }}>
                  <span style={{ fontSize: '14px', fontWeight: 700, color: 'var(--ep-primary)' }}>
                    {curr.currency} Currency Pool
                  </span>
                  <span style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', fontWeight: 600 }}>
                    {curr.successfulPurchaseCount} Orders
                  </span>
                </div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '6px', fontSize: '13px' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span style={{ color: 'var(--ep-text-secondary)' }}>Gross Revenue:</span>
                    <span style={{ fontWeight: 600 }}>{formatReportCurrency(curr.grossRevenue, curr.currency)}</span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span style={{ color: 'var(--ep-text-secondary)' }}>Refunded:</span>
                    <span style={{ fontWeight: 600, color: '#dc2626' }}>
                      -{formatReportCurrency(curr.refundedAmount, curr.currency)}
                    </span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', borderTop: '1px solid var(--ep-border)', paddingTop: '6px' }}>
                    <span style={{ fontWeight: 700, color: 'var(--ep-text-primary)' }}>Net Proceeds:</span>
                    <span style={{ fontWeight: 800, color: '#16a34a' }}>
                      {formatReportCurrency(curr.netRevenue, curr.currency)}
                    </span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </section>
      )}

      {/* Operational Throughput Summary Card */}
      <section
        style={{
          backgroundColor: '#ffffff',
          borderRadius: 'var(--ep-radius-card)',
          border: '1px solid var(--ep-border)',
          padding: '20px',
          boxShadow: 'var(--ep-shadow-card)',
        }}
      >
        <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 16px 0', color: 'var(--ep-text-primary)' }}>
          Operational Throughput & Conversion
        </h3>
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
            gap: '16px',
          }}
        >
          <div style={{ padding: '12px', borderLeft: '3px solid var(--ep-primary)', backgroundColor: 'var(--ep-canvas)' }}>
            <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>Gross Tickets Sold</div>
            <div style={{ fontSize: '20px', fontWeight: 700, marginTop: '4px' }}>{formatReportCount(data.grossTicketsSold)}</div>
          </div>
          <div style={{ padding: '12px', borderLeft: '3px solid #16a34a', backgroundColor: 'var(--ep-canvas)' }}>
            <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>Active Tickets</div>
            <div style={{ fontSize: '20px', fontWeight: 700, marginTop: '4px' }}>{formatReportCount(data.activeTicketsBooked)}</div>
          </div>
          <div style={{ padding: '12px', borderLeft: '3px solid #eab308', backgroundColor: 'var(--ep-canvas)' }}>
            <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>Pending Payment Holds</div>
            <div style={{ fontSize: '20px', fontWeight: 700, marginTop: '4px' }}>{formatReportCount(data.pendingPaymentHolds)}</div>
          </div>
          <div style={{ padding: '12px', borderLeft: '3px solid #dc2626', backgroundColor: 'var(--ep-canvas)' }}>
            <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>Payment Failed / Abandoned</div>
            <div style={{ fontSize: '20px', fontWeight: 700, marginTop: '4px' }}>{formatReportCount(data.paymentFailedCheckouts)}</div>
          </div>
        </div>
      </section>
    </div>
  );
}

/**
 * Bookings Report Subview
 */
function BookingsReportView({ data }) {
  const eventsBreakdown = Array.isArray(data.eventsBreakdown) ? data.eventsBreakdown : [];
  const ticketTypesBreakdown = Array.isArray(data.ticketTypesBreakdown) ? data.ticketTypesBreakdown : [];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '24px' }}>
      {/* Summary Cards */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
          gap: '16px',
        }}
      >
        <MetricCard
          title="Total Orders"
          value={formatReportCount(data.totalBookings)}
          subtitle="All booking attempts"
          icon={Ticket}
        />
        <MetricCard
          title="Confirmed Orders"
          value={formatReportCount(data.confirmedBookings)}
          subtitle="Valid successful purchases"
          icon={CheckCircle2}
          color="#16a34a"
        />
        <MetricCard
          title="Cancelled Orders"
          value={formatReportCount(data.cancelledBookings)}
          subtitle={`Churn: ${formatReportRate(data.cancellationRate)}`}
          icon={XCircle}
          color="#dc2626"
        />
        <MetricCard
          title="Active Tickets"
          value={formatReportCount(data.activeTicketsBooked)}
          subtitle={`Gross: ${formatReportCount(data.grossTicketsSold)}`}
          icon={Layers}
        />
        <MetricCard
          title="Avg Tickets / Order"
          value={Number(data.averageTicketsPerBooking || 0).toFixed(1)}
          subtitle="Average basket density"
          icon={TrendingUp}
        />
        <MetricCard
          title="Holds & Abandoned"
          value={`${formatReportCount(data.pendingPaymentBookings)} / ${formatReportCount(data.paymentFailedBookings)}`}
          subtitle="Pending / Failed payments"
          icon={Clock}
          color="#eab308"
        />
      </div>

      {/* Events Breakdown Table */}
      <section
        style={{
          backgroundColor: '#ffffff',
          borderRadius: 'var(--ep-radius-card)',
          border: '1px solid var(--ep-border)',
          padding: '20px',
          boxShadow: 'var(--ep-shadow-card)',
        }}
      >
        <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 16px 0', color: 'var(--ep-text-primary)' }}>
          Bookings Breakdown by Event
        </h3>

        {eventsBreakdown.length === 0 ? (
          <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: 0 }}>
            No event breakdown records available for this query.
          </p>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
              <thead>
                <tr style={{ borderBottom: '2px solid var(--ep-border)', color: 'var(--ep-text-secondary)' }}>
                  <th style={{ padding: '10px 12px', fontWeight: 600 }}>Event Title</th>
                  <th style={{ padding: '10px 12px', fontWeight: 600, textAlign: 'right' }}>Total Bookings</th>
                  <th style={{ padding: '10px 12px', fontWeight: 600, textAlign: 'right' }}>Confirmed</th>
                  <th style={{ padding: '10px 12px', fontWeight: 600, textAlign: 'right' }}>Cancelled</th>
                  <th style={{ padding: '10px 12px', fontWeight: 600, textAlign: 'right' }}>Churn Rate</th>
                  <th style={{ padding: '10px 12px', fontWeight: 600, textAlign: 'right' }}>Active Tickets</th>
                </tr>
              </thead>
              <tbody>
                {eventsBreakdown.map((evt, idx) => (
                  <tr
                    key={evt.eventId || idx}
                    style={{
                      borderBottom: '1px solid var(--ep-border)',
                      backgroundColor: idx % 2 === 0 ? '#ffffff' : 'var(--ep-canvas)',
                    }}
                  >
                    <td style={{ padding: '10px 12px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                      {evt.eventTitle || evt.eventId}
                    </td>
                    <td style={{ padding: '10px 12px', textAlign: 'right' }}>{formatReportCount(evt.totalBookings)}</td>
                    <td style={{ padding: '10px 12px', textAlign: 'right', color: '#16a34a', fontWeight: 600 }}>
                      {formatReportCount(evt.confirmedBookings)}
                    </td>
                    <td style={{ padding: '10px 12px', textAlign: 'right', color: '#dc2626' }}>
                      {formatReportCount(evt.cancelledBookings)}
                    </td>
                    <td style={{ padding: '10px 12px', textAlign: 'right' }}>
                      {formatReportRate(evt.cancellationRate)}
                    </td>
                    <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 600 }}>
                      {formatReportCount(evt.activeTickets)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {/* Ticket Tiers Breakdown Table */}
      {ticketTypesBreakdown.length > 0 && (
        <section
          style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            padding: '20px',
            boxShadow: 'var(--ep-shadow-card)',
          }}
        >
          <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 16px 0', color: 'var(--ep-text-primary)' }}>
            Ticket Tiers Performance
          </h3>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
              <thead>
                <tr style={{ borderBottom: '2px solid var(--ep-border)', color: 'var(--ep-text-secondary)' }}>
                  <th style={{ padding: '10px 12px', fontWeight: 600 }}>Tier Name</th>
                  <th style={{ padding: '10px 12px', fontWeight: 600, textAlign: 'right' }}>Unit Price</th>
                  <th style={{ padding: '10px 12px', fontWeight: 600, textAlign: 'right' }}>Total Sold</th>
                  <th style={{ padding: '10px 12px', fontWeight: 600, textAlign: 'right' }}>Active</th>
                  <th style={{ padding: '10px 12px', fontWeight: 600, textAlign: 'right' }}>Cancelled</th>
                  <th style={{ padding: '10px 12px', fontWeight: 600, textAlign: 'right' }}>Subtotal</th>
                </tr>
              </thead>
              <tbody>
                {ticketTypesBreakdown.map((tier, idx) => (
                  <tr
                    key={tier.ticketTypeId || idx}
                    style={{
                      borderBottom: '1px solid var(--ep-border)',
                      backgroundColor: idx % 2 === 0 ? '#ffffff' : 'var(--ep-canvas)',
                    }}
                  >
                    <td style={{ padding: '10px 12px', fontWeight: 600 }}>{tier.ticketName || 'General Admission'}</td>
                    <td style={{ padding: '10px 12px', textAlign: 'right' }}>{formatReportCurrency(tier.unitPrice)}</td>
                    <td style={{ padding: '10px 12px', textAlign: 'right' }}>{formatReportCount(tier.totalQuantity)}</td>
                    <td style={{ padding: '10px 12px', textAlign: 'right', color: '#16a34a', fontWeight: 600 }}>
                      {formatReportCount(tier.activeQuantity)}
                    </td>
                    <td style={{ padding: '10px 12px', textAlign: 'right', color: '#dc2626' }}>
                      {formatReportCount(tier.cancelledQuantity)}
                    </td>
                    <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 700 }}>
                      {formatReportCurrency(tier.totalSubtotal)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
    </div>
  );
}

/**
 * Revenue Report Subview
 */
function RevenueReportView({ data }) {
  const currencyBreakdown = Array.isArray(data.currencyBreakdown) ? data.currencyBreakdown : [];
  const eventsBreakdown = Array.isArray(data.eventsBreakdown) ? data.eventsBreakdown : [];
  const organizersBreakdown = Array.isArray(data.organizersBreakdown) ? data.organizersBreakdown : [];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '24px' }}>
      {/* Primary Financial Metric Cards */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
          gap: '16px',
        }}
      >
        <MetricCard
          title="Gross Revenue"
          value={formatReportCurrency(data.grossRevenue, data.currency)}
          subtitle={`${formatReportCount(data.successfulPurchaseCount)} successful purchases`}
          icon={Wallet}
        />
        <MetricCard
          title="Refunded Volume"
          value={formatReportCurrency(data.refundedAmount, data.currency)}
          subtitle={`Refund Rate: ${formatReportRate(data.refundRate)}`}
          icon={XCircle}
          color="#dc2626"
        />
        <MetricCard
          title="Net Revenue"
          value={formatReportCurrency(data.netRevenue, data.currency)}
          subtitle="Net realized cash flow"
          icon={TrendingUp}
          color="#16a34a"
        />
        <MetricCard
          title="Average Order Value"
          value={formatReportCurrency(data.averageOrderValue, data.currency)}
          subtitle="Revenue per successful order"
          icon={Briefcase}
        />
        <MetricCard
          title="Refund Counts"
          value={`${formatReportCount(data.fullRefundCount)} Full / ${formatReportCount(data.partialRefundCount)} Part`}
          subtitle="Total refunded transactions"
          icon={Clock}
        />
      </div>

      {/* Segregated Currency Summaries */}
      {currencyBreakdown.length > 0 && (
        <section
          style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            padding: '20px',
            boxShadow: 'var(--ep-shadow-card)',
          }}
        >
          <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 16px 0', color: 'var(--ep-text-primary)' }}>
            Revenue Reconciliation by Currency
          </h3>
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))',
              gap: '16px',
            }}
          >
            {currencyBreakdown.map((curr) => (
              <div
                key={curr.currency}
                style={{
                  padding: '16px',
                  borderRadius: 'var(--ep-radius-btn)',
                  backgroundColor: 'var(--ep-canvas)',
                  border: '1px solid var(--ep-border)',
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                  <span style={{ fontSize: '14px', fontWeight: 700, color: 'var(--ep-primary)' }}>
                    {curr.currency}
                  </span>
                  <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', fontWeight: 600 }}>
                    {curr.successfulPurchaseCount} Orders • {curr.ticketsSold} Tickets
                  </span>
                </div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '6px', fontSize: '13px' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span style={{ color: 'var(--ep-text-secondary)' }}>Gross:</span>
                    <span style={{ fontWeight: 600 }}>{formatReportCurrency(curr.grossRevenue, curr.currency)}</span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span style={{ color: 'var(--ep-text-secondary)' }}>Refunded:</span>
                    <span style={{ fontWeight: 600, color: '#dc2626' }}>
                      -{formatReportCurrency(curr.refundedAmount, curr.currency)}
                    </span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', borderTop: '1px solid var(--ep-border)', paddingTop: '6px' }}>
                    <span style={{ fontWeight: 700, color: 'var(--ep-text-primary)' }}>Net:</span>
                    <span style={{ fontWeight: 800, color: '#16a34a' }}>
                      {formatReportCurrency(curr.netRevenue, curr.currency)}
                    </span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </section>
      )}

      {/* Events Revenue Breakdown Table */}
      {eventsBreakdown.length > 0 && (
        <section
          style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            padding: '20px',
            boxShadow: 'var(--ep-shadow-card)',
          }}
        >
          <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 16px 0', color: 'var(--ep-text-primary)' }}>
            Revenue Breakdown by Event
          </h3>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
              <thead>
                <tr style={{ borderBottom: '2px solid var(--ep-border)', color: 'var(--ep-text-secondary)' }}>
                  <th style={{ padding: '10px 12px', fontWeight: 600 }}>Event ID</th>
                  <th style={{ padding: '10px 12px', fontWeight: 600 }}>Currency</th>
                  <th style={{ padding: '10px 12px', fontWeight: 600, textAlign: 'right' }}>Purchases</th>
                  <th style={{ padding: '10px 12px', fontWeight: 600, textAlign: 'right' }}>Gross Revenue</th>
                  <th style={{ padding: '10px 12px', fontWeight: 600, textAlign: 'right' }}>Refunded</th>
                  <th style={{ padding: '10px 12px', fontWeight: 600, textAlign: 'right' }}>Net Revenue</th>
                </tr>
              </thead>
              <tbody>
                {eventsBreakdown.map((evt, idx) => (
                  <tr
                    key={`${evt.eventId}-${evt.currency}-${idx}`}
                    style={{
                      borderBottom: '1px solid var(--ep-border)',
                      backgroundColor: idx % 2 === 0 ? '#ffffff' : 'var(--ep-canvas)',
                    }}
                  >
                    <td style={{ padding: '10px 12px', fontFamily: 'monospace', fontSize: '12px' }}>
                      {evt.eventId}
                    </td>
                    <td style={{ padding: '10px 12px', fontWeight: 600 }}>{evt.currency}</td>
                    <td style={{ padding: '10px 12px', textAlign: 'right' }}>{formatReportCount(evt.purchaseCount)}</td>
                    <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 600 }}>
                      {formatReportCurrency(evt.grossRevenue, evt.currency)}
                    </td>
                    <td style={{ padding: '10px 12px', textAlign: 'right', color: '#dc2626' }}>
                      {formatReportCurrency(evt.refundedAmount, evt.currency)}
                    </td>
                    <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 700, color: '#16a34a' }}>
                      {formatReportCurrency(evt.netRevenue, evt.currency)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {/* Organizers Revenue Breakdown Table */}
      {organizersBreakdown.length > 0 && (
        <section
          style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            padding: '20px',
            boxShadow: 'var(--ep-shadow-card)',
          }}
        >
          <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 16px 0', color: 'var(--ep-text-primary)' }}>
            Revenue Breakdown by Organizer
          </h3>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
              <thead>
                <tr style={{ borderBottom: '2px solid var(--ep-border)', color: 'var(--ep-text-secondary)' }}>
                  <th style={{ padding: '10px 12px', fontWeight: 600 }}>Organizer ID</th>
                  <th style={{ padding: '10px 12px', fontWeight: 600 }}>Currency</th>
                  <th style={{ padding: '10px 12px', fontWeight: 600, textAlign: 'right' }}>Purchases</th>
                  <th style={{ padding: '10px 12px', fontWeight: 600, textAlign: 'right' }}>Gross Revenue</th>
                  <th style={{ padding: '10px 12px', fontWeight: 600, textAlign: 'right' }}>Refunded</th>
                  <th style={{ padding: '10px 12px', fontWeight: 600, textAlign: 'right' }}>Net Revenue</th>
                </tr>
              </thead>
              <tbody>
                {organizersBreakdown.map((org, idx) => (
                  <tr
                    key={`${org.organizerId}-${org.currency}-${idx}`}
                    style={{
                      borderBottom: '1px solid var(--ep-border)',
                      backgroundColor: idx % 2 === 0 ? '#ffffff' : 'var(--ep-canvas)',
                    }}
                  >
                    <td style={{ padding: '10px 12px', fontFamily: 'monospace', fontSize: '12px' }}>
                      {org.organizerId}
                    </td>
                    <td style={{ padding: '10px 12px', fontWeight: 600 }}>{org.currency}</td>
                    <td style={{ padding: '10px 12px', textAlign: 'right' }}>{formatReportCount(org.purchaseCount)}</td>
                    <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 600 }}>
                      {formatReportCurrency(org.grossRevenue, org.currency)}
                    </td>
                    <td style={{ padding: '10px 12px', textAlign: 'right', color: '#dc2626' }}>
                      {formatReportCurrency(org.refundedAmount, org.currency)}
                    </td>
                    <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 700, color: '#16a34a' }}>
                      {formatReportCurrency(org.netRevenue, org.currency)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
    </div>
  );
}
