import React, { useState, useEffect, useCallback, useRef } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { Layout } from '../../components/layout/Layout';
import { useAuth } from '../../context/AuthContext';
import { getMyPayments } from '../../services/paymentService';
import {
  formatPaymentDate,
  formatCurrency,
  getPaymentStatusBadgeConfig,
} from '../../utils/paymentHistoryHelper';
import {
  CreditCard,
  Calendar,
  Clock,
  ArrowRight,
  ArrowLeft,
  Loader2,
  AlertTriangle,
  Receipt,
  Ticket,
  ChevronLeft,
  ChevronRight,
  CheckCircle2,
  XCircle,
  RefreshCw,
} from 'lucide-react';

const STATUS_TABS = [
  { label: 'All', value: 'All' },
  { label: 'Successful', value: 'Succeeded' },
  { label: 'Pending', value: 'Pending' },
  { label: 'Failed', value: 'Failed' },
];

export function PaymentHistoryPage() {
  const { accessToken } = useAuth();
  const navigate = useNavigate();

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [payments, setPayments] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [hasNextPage, setHasNextPage] = useState(false);
  const [hasPreviousPage, setHasPreviousPage] = useState(false);
  const [activeStatus, setActiveStatus] = useState('All');

  const abortControllerRef = useRef(null);

  const fetchPayments = useCallback(
    async (pageToLoad = 1, statusFilter = activeStatus) => {
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
      const controller = new AbortController();
      abortControllerRef.current = controller;

      setLoading(true);
      setError(null);

      try {
        const queryParams = {
          page: pageToLoad,
          pageSize: 10,
          status: statusFilter === 'All' ? undefined : statusFilter,
        };

        const res = await getMyPayments(queryParams, accessToken);

        if (res && Array.isArray(res.items)) {
          setPayments(res.items);
          setTotalCount(res.totalCount || 0);
          setCurrentPage(res.page || pageToLoad);
          setTotalPages(res.totalPages || 1);
          setHasNextPage(Boolean(res.hasNextPage));
          setHasPreviousPage(Boolean(res.hasPreviousPage));
        } else if (Array.isArray(res)) {
          // Fallback if backend returns unpaged array
          setPayments(res);
          setTotalCount(res.length);
          setCurrentPage(1);
          setTotalPages(1);
          setHasNextPage(false);
          setHasPreviousPage(false);
        } else {
          setPayments([]);
          setTotalCount(0);
        }
      } catch (err) {
        if (err.name === 'AbortError') return;
        console.error('Failed to load payment history:', err);
        setError(
          err?.data?.message ||
          err?.message ||
          'Failed to load payment history. Please try again.'
        );
      } finally {
        setLoading(false);
      }
    },
    [accessToken, activeStatus]
  );

  useEffect(() => {
    fetchPayments(currentPage, activeStatus);
    return () => {
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
    };
  }, [fetchPayments, currentPage, activeStatus]);

  const handleTabChange = (status) => {
    setActiveStatus(status);
    setCurrentPage(1);
  };

  const handlePageChange = (newPage) => {
    if (newPage < 1 || (totalPages && newPage > totalPages)) return;
    setCurrentPage(newPage);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  return (
    <Layout>
      <div
        className="container"
        style={{
          maxWidth: '1000px',
          margin: '0 auto',
          padding: '32px 16px 64px 16px',
          fontFamily: 'var(--ep-font-body)',
        }}
      >
        {/* Breadcrumb Navigation */}
        <div style={{ marginBottom: '16px' }}>
          <Link
            to="/my-bookings"
            data-testid="back-to-bookings-link"
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '6px',
              fontSize: '13px',
              fontWeight: 600,
              color: 'var(--ep-text-secondary)',
              textDecoration: 'none',
              transition: 'var(--ep-transition)',
            }}
            onMouseEnter={(e) => (e.currentTarget.style.color = 'var(--ep-primary)')}
            onMouseLeave={(e) => (e.currentTarget.style.color = 'var(--ep-text-secondary)')}
          >
            <ArrowLeft size={14} />
            <span>Back to My Bookings</span>
          </Link>
        </div>

        {/* Page Header */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            flexWrap: 'wrap',
            gap: '16px',
            marginBottom: '28px',
          }}
        >
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
              <div
                style={{
                  width: '40px',
                  height: '40px',
                  borderRadius: '10px',
                  backgroundColor: 'var(--ep-soft-accent)',
                  color: 'var(--ep-primary)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                }}
              >
                <CreditCard size={22} />
              </div>
              <h1
                data-testid="payment-history-title"
                style={{
                  margin: 0,
                  fontSize: '28px',
                  fontWeight: 800,
                  color: 'var(--ep-text-primary)',
                  fontFamily: 'var(--ep-font-heading)',
                }}
              >
                Payment History
              </h1>
            </div>
            <p
              style={{
                margin: '8px 0 0 0',
                fontSize: '14px',
                color: 'var(--ep-text-secondary)',
              }}
            >
              View your transaction receipts, payment statuses, and linked booking records.
            </p>
          </div>

          {/* Quick link to Profile */}
          <Link
            to="/profile"
            data-testid="header-profile-link"
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '6px',
              fontSize: '13px',
              fontWeight: 600,
              padding: '8px 16px',
              borderRadius: 'var(--ep-radius-btn)',
              backgroundColor: '#ffffff',
              color: 'var(--ep-text-primary)',
              border: '1px solid var(--ep-border)',
              textDecoration: 'none',
              transition: 'var(--ep-transition)',
            }}
            onMouseEnter={(e) => (e.currentTarget.style.borderColor = 'var(--ep-primary)')}
            onMouseLeave={(e) => (e.currentTarget.style.borderColor = 'var(--ep-border)')}
          >
            <span>User Profile</span>
            <ChevronRight size={14} />
          </Link>
        </div>

        {/* Filter Tabs */}
        <div
          data-testid="payment-status-tabs"
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            marginBottom: '24px',
            overflowX: 'auto',
            paddingBottom: '4px',
          }}
        >
          {STATUS_TABS.map((tab) => {
            const isActive = activeStatus === tab.value;
            return (
              <button
                key={tab.value}
                type="button"
                data-testid={`tab-${tab.value.toLowerCase()}`}
                onClick={() => handleTabChange(tab.value)}
                style={{
                  padding: '8px 18px',
                  borderRadius: 'var(--ep-radius-pill)',
                  fontSize: '13px',
                  fontWeight: 600,
                  border: isActive ? '1px solid var(--ep-primary)' : '1px solid var(--ep-border)',
                  backgroundColor: isActive ? 'var(--ep-primary)' : '#ffffff',
                  color: isActive ? '#ffffff' : 'var(--ep-text-primary)',
                  cursor: 'pointer',
                  whiteSpace: 'nowrap',
                  transition: 'var(--ep-transition)',
                }}
              >
                {tab.label}
              </button>
            );
          })}
        </div>

        {/* Main Content Area */}
        {loading ? (
          /* Loading State */
          <div
            data-testid="payment-history-loading"
            style={{
              padding: '64px 16px',
              textAlign: 'center',
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
            }}
          >
            <Loader2
              size={32}
              style={{
                animation: 'spin 1s linear infinite',
                color: 'var(--ep-primary)',
                margin: '0 auto 12px',
              }}
            />
            <div style={{ fontSize: '15px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>
              Loading payment history...
            </div>
            <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: '4px 0 0' }}>
              Fetching verified transactions from the Payment Service.
            </p>
          </div>
        ) : error ? (
          /* Error State */
          <div
            data-testid="payment-history-error"
            style={{
              padding: '24px',
              backgroundColor: '#FEF2F2',
              border: '1px solid #FECACA',
              borderRadius: 'var(--ep-radius-card)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              flexWrap: 'wrap',
              gap: '16px',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
              <AlertTriangle size={24} color="#991B1B" />
              <div>
                <div style={{ fontSize: '15px', fontWeight: 700, color: '#991B1B' }}>
                  Unable to load payment history
                </div>
                <div style={{ fontSize: '13px', color: '#B91C1C', marginTop: '2px' }}>
                  {error}
                </div>
              </div>
            </div>
            <button
              type="button"
              data-testid="payment-history-retry"
              onClick={() => fetchPayments(currentPage, activeStatus)}
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                padding: '8px 16px',
                backgroundColor: '#ffffff',
                border: '1px solid #FECACA',
                borderRadius: 'var(--ep-radius-btn)',
                fontSize: '13px',
                fontWeight: 600,
                color: '#991B1B',
                cursor: 'pointer',
              }}
            >
              <RefreshCw size={14} />
              <span>Retry</span>
            </button>
          </div>
        ) : payments.length === 0 ? (
          /* Empty State */
          <div
            data-testid="payment-history-empty"
            style={{
              padding: '64px 24px',
              textAlign: 'center',
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              boxShadow: 'var(--ep-shadow-card)',
            }}
          >
            <div
              style={{
                width: '64px',
                height: '64px',
                borderRadius: '50%',
                backgroundColor: 'var(--ep-canvas)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                margin: '0 auto 16px',
                color: 'var(--ep-text-secondary)',
              }}
            >
              <Receipt size={32} />
            </div>

            <h3
              data-testid="empty-heading"
              style={{
                fontSize: '18px',
                fontWeight: 700,
                color: 'var(--ep-text-primary)',
                margin: '0 0 8px',
              }}
            >
              {activeStatus === 'All'
                ? "You don't have any payment history yet."
                : `No ${activeStatus.toLowerCase()} payments found.`}
            </h3>

            <p
              style={{
                fontSize: '14px',
                color: 'var(--ep-text-secondary)',
                margin: '0 auto 24px',
                maxWidth: '420px',
                lineHeight: 1.5,
              }}
            >
              {activeStatus === 'All'
                ? 'Once you purchase tickets for an event, your payment records, receipts, and statuses will appear here.'
                : `There are currently no transaction records matching the "${activeStatus}" filter.`}
            </p>

            {activeStatus === 'All' ? (
              <button
                type="button"
                data-testid="explore-events-btn"
                onClick={() => navigate('/')}
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '8px',
                  padding: '10px 22px',
                  backgroundColor: 'var(--ep-primary)',
                  color: '#ffffff',
                  border: 'none',
                  borderRadius: 'var(--ep-radius-btn)',
                  fontSize: '14px',
                  fontWeight: 600,
                  cursor: 'pointer',
                  boxShadow: '0 2px 6px rgba(255, 91, 0, 0.25)',
                  transition: 'var(--ep-transition)',
                }}
              >
                <span>Explore Events</span>
                <ArrowRight size={15} />
              </button>
            ) : (
              <button
                type="button"
                data-testid="clear-filter-btn"
                onClick={() => handleTabChange('All')}
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '6px',
                  padding: '8px 18px',
                  backgroundColor: '#ffffff',
                  color: 'var(--ep-primary)',
                  border: '1px solid var(--ep-border)',
                  borderRadius: 'var(--ep-radius-btn)',
                  fontSize: '13px',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                <span>View All Payments</span>
              </button>
            )}
          </div>
        ) : (
          /* Payment List View */
          <div
            data-testid="payment-history-list"
            style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}
          >
            {payments.map((payment) => {
              const statusCfg = getPaymentStatusBadgeConfig(payment.status);
              const formattedDate = formatPaymentDate(payment.paymentDate || payment.createdAt);
              const formattedAmt = formatCurrency(payment.amount, payment.currency);

              return (
                <div
                  key={payment.id}
                  data-testid={`payment-card-${payment.id}`}
                  style={{
                    backgroundColor: '#ffffff',
                    borderRadius: 'var(--ep-radius-card)',
                    border: '1px solid var(--ep-border)',
                    boxShadow: 'var(--ep-shadow-card)',
                    padding: '20px 24px',
                    transition: 'var(--ep-transition)',
                  }}
                  onMouseEnter={(e) => (e.currentTarget.style.borderColor = 'var(--ep-primary)')}
                  onMouseLeave={(e) => (e.currentTarget.style.borderColor = 'var(--ep-border)')}
                >
                  {/* Top Bar: Booking Reference & Status Badge */}
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      flexWrap: 'wrap',
                      gap: '12px',
                      marginBottom: '14px',
                      paddingBottom: '12px',
                      borderBottom: '1px solid var(--ep-border)',
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <Ticket size={16} color="var(--ep-primary)" />
                      <span
                        data-testid={`booking-ref-${payment.bookingReference}`}
                        style={{
                          fontSize: '14px',
                          fontWeight: 700,
                          color: 'var(--ep-text-primary)',
                        }}
                      >
                        Booking: {payment.bookingReference || 'N/A'}
                      </span>
                    </div>

                    {/* Status Badge */}
                    <div
                      data-testid={`payment-status-badge-${payment.id}`}
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '6px',
                        padding: '4px 12px',
                        borderRadius: 'var(--ep-radius-pill)',
                        fontSize: '12px',
                        fontWeight: 700,
                        backgroundColor: statusCfg.bg,
                        color: statusCfg.color,
                        border: `1px solid ${statusCfg.border}`,
                      }}
                    >
                      {statusCfg.label === 'Successful' ? (
                        <CheckCircle2 size={13} />
                      ) : statusCfg.label === 'Failed' ? (
                        <XCircle size={13} />
                      ) : (
                        <Clock size={13} />
                      )}
                      <span>Status: {statusCfg.label}</span>
                    </div>
                  </div>

                  {/* Body Content Grid */}
                  <div
                    style={{
                      display: 'grid',
                      gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
                      gap: '16px',
                      marginBottom: '16px',
                    }}
                  >
                    {/* Event Name */}
                    <div>
                      <div
                        style={{
                          fontSize: '12px',
                          fontWeight: 600,
                          color: 'var(--ep-text-secondary)',
                          textTransform: 'uppercase',
                          marginBottom: '2px',
                        }}
                      >
                        Event
                      </div>
                      <div
                        data-testid={`event-name-${payment.id}`}
                        style={{
                          fontSize: '15px',
                          fontWeight: 700,
                          color: 'var(--ep-text-primary)',
                        }}
                      >
                        {payment.eventName || 'Event Admission'}
                      </div>
                    </div>

                    {/* Amount */}
                    <div>
                      <div
                        style={{
                          fontSize: '12px',
                          fontWeight: 600,
                          color: 'var(--ep-text-secondary)',
                          textTransform: 'uppercase',
                          marginBottom: '2px',
                        }}
                      >
                        Amount
                      </div>
                      <div
                        data-testid={`payment-amount-${payment.id}`}
                        style={{
                          fontSize: '16px',
                          fontWeight: 800,
                          color: 'var(--ep-text-primary)',
                        }}
                      >
                        {formattedAmt}
                      </div>
                    </div>

                    {/* Date */}
                    <div>
                      <div
                        style={{
                          fontSize: '12px',
                          fontWeight: 600,
                          color: 'var(--ep-text-secondary)',
                          textTransform: 'uppercase',
                          marginBottom: '2px',
                        }}
                      >
                        Date
                      </div>
                      <div
                        data-testid={`payment-date-${payment.id}`}
                        style={{
                          fontSize: '14px',
                          fontWeight: 500,
                          color: 'var(--ep-text-primary)',
                          display: 'flex',
                          alignItems: 'center',
                          gap: '5px',
                        }}
                      >
                        <Calendar size={14} color="var(--ep-text-secondary)" />
                        <span>{formattedDate}</span>
                      </div>
                    </div>
                  </div>

                  {/* Actions Row */}
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'flex-end',
                      gap: '12px',
                      borderTop: '1px solid var(--ep-border)',
                      paddingTop: '12px',
                    }}
                  >
                    <button
                      type="button"
                      data-testid={`view-booking-btn-${payment.id}`}
                      onClick={() => navigate(`/my-bookings/${payment.bookingId}`)}
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '6px',
                        padding: '6px 14px',
                        backgroundColor: '#ffffff',
                        color: 'var(--ep-primary)',
                        border: '1px solid var(--ep-border)',
                        borderRadius: 'var(--ep-radius-btn)',
                        fontSize: '13px',
                        fontWeight: 600,
                        cursor: 'pointer',
                        transition: 'var(--ep-transition)',
                      }}
                      onMouseEnter={(e) => {
                        e.currentTarget.style.borderColor = 'var(--ep-primary)';
                        e.currentTarget.style.backgroundColor = 'var(--ep-soft-accent)';
                      }}
                      onMouseLeave={(e) => {
                        e.currentTarget.style.borderColor = 'var(--ep-border)';
                        e.currentTarget.style.backgroundColor = '#ffffff';
                      }}
                    >
                      <span>View Related Booking</span>
                      <ArrowRight size={14} />
                    </button>
                  </div>
                </div>
              );
            })}

            {/* Pagination Controls */}
            {totalPages > 1 && (
              <div
                data-testid="payment-history-pagination"
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: '16px',
                  marginTop: '16px',
                  padding: '16px 0',
                }}
              >
                <button
                  type="button"
                  data-testid="payment-history-prev-page"
                  onClick={() => handlePageChange(currentPage - 1)}
                  disabled={!hasPreviousPage && currentPage <= 1}
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '4px',
                    padding: '8px 14px',
                    backgroundColor: '#ffffff',
                    border: '1px solid var(--ep-border)',
                    borderRadius: 'var(--ep-radius-btn)',
                    fontSize: '13px',
                    fontWeight: 600,
                    color: !hasPreviousPage && currentPage <= 1 ? 'var(--ep-text-secondary)' : 'var(--ep-text-primary)',
                    cursor: !hasPreviousPage && currentPage <= 1 ? 'not-allowed' : 'pointer',
                    opacity: !hasPreviousPage && currentPage <= 1 ? 0.6 : 1,
                  }}
                >
                  <ChevronLeft size={16} />
                  <span>Previous</span>
                </button>

                <span
                  data-testid="payment-history-page-indicator"
                  style={{
                    fontSize: '13px',
                    fontWeight: 600,
                    color: 'var(--ep-text-secondary)',
                  }}
                >
                  Page {currentPage} of {totalPages} ({totalCount} {totalCount === 1 ? 'record' : 'records'})
                </span>

                <button
                  type="button"
                  data-testid="payment-history-next-page"
                  onClick={() => handlePageChange(currentPage + 1)}
                  disabled={!hasNextPage && currentPage >= totalPages}
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '4px',
                    padding: '8px 14px',
                    backgroundColor: '#ffffff',
                    border: '1px solid var(--ep-border)',
                    borderRadius: 'var(--ep-radius-btn)',
                    fontSize: '13px',
                    fontWeight: 600,
                    color: !hasNextPage && currentPage >= totalPages ? 'var(--ep-text-secondary)' : 'var(--ep-text-primary)',
                    cursor: !hasNextPage && currentPage >= totalPages ? 'not-allowed' : 'pointer',
                    opacity: !hasNextPage && currentPage >= totalPages ? 0.6 : 1,
                  }}
                >
                  <span>Next</span>
                  <ChevronRight size={16} />
                </button>
              </div>
            )}
          </div>
        )}
      </div>
    </Layout>
  );
}

export default PaymentHistoryPage;
