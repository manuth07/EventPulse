import React, { useState, useEffect, useCallback, useRef } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { useAuth } from '../../context/AuthContext';
import { getMyBookings, cancelBooking } from '../../services/bookingService';
import { createCheckoutSession } from '../../services/paymentService';
import {
  Ticket,
  Calendar,
  Clock,
  CheckCircle2,
  AlertCircle,
  XCircle,
  ChevronDown,
  ChevronUp,
  ArrowRight,
  RotateCcw,
  Loader2,
  Copy,
  Check,
  Receipt,
  ArrowLeft,
  CreditCard,
  AlertTriangle,
  X,
  Ban,
} from 'lucide-react';

const STATUS_TABS = [
  { label: 'All', value: 'All' },
  { label: 'Confirmed', value: 'Confirmed' },
  { label: 'Pending Payment', value: 'PendingPayment' },
  { label: 'Cancelled', value: 'Cancelled' },
];

function formatCurrency(amount) {
  const num = Number(amount);
  if (isNaN(num)) return 'LKR 0.00';
  return `LKR ${num.toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;
}

function formatDate(dateString) {
  if (!dateString) return 'Date unavailable';
  try {
    const d = new Date(dateString);
    const day = d.getDate();
    const month = d.toLocaleDateString('en-US', { month: 'short' });
    const year = d.getFullYear();
    const time = d.toLocaleTimeString('en-US', {
      hour: 'numeric',
      minute: '2-digit',
      hour12: true,
    });
    return `${day} ${month} ${year} · ${time}`;
  } catch {
    return dateString;
  }
}

function getStatusBadgeConfig(status) {
  switch (status?.toLowerCase()) {
    case 'confirmed':
      return {
        label: 'Confirmed',
        bg: '#ECFDF5',
        color: '#065F46',
        border: '#A7F3D0',
        icon: CheckCircle2,
      };
    case 'pendingpayment':
      return {
        label: 'Pending Payment',
        bg: '#FFFBEB',
        color: '#92400E',
        border: '#FDE68A',
        icon: Clock,
      };
    case 'cancelled':
      return {
        label: 'Cancelled',
        bg: '#FEF2F2',
        color: '#991B1B',
        border: '#FECACA',
        icon: XCircle,
      };
    case 'expired':
      return {
        label: 'Expired',
        bg: '#F1F5F9',
        color: '#475569',
        border: '#E2E8F0',
        icon: AlertCircle,
      };
    default:
      return {
        label: status || 'Unknown',
        bg: '#F3F4F6',
        color: '#374151',
        border: '#E5E7EB',
        icon: AlertCircle,
      };
  }
}

export const CountdownTimer = React.memo(
  function CountdownTimer({ expiresAt, onExpire, onExpired }) {
    const expireCallback = onExpire || onExpired;
    const onExpireRef = useRef(expireCallback);
    onExpireRef.current = expireCallback;
    const hasExpiredRef = useRef(false);

    const calculateTimeLeft = () => {
      if (!expiresAt) return null;
      const difference = new Date(expiresAt).getTime() - new Date().getTime();
      if (difference <= 0) return { total: 0, hours: 0, minutes: 0, seconds: 0 };

      return {
        total: difference,
        hours: Math.floor(difference / (1000 * 60 * 60)),
        minutes: Math.floor((difference / 1000 / 60) % 60),
        seconds: Math.floor((difference / 1000) % 60),
      };
    };

    const [timeLeft, setTimeLeft] = useState(calculateTimeLeft);

    useEffect(() => {
      const timer = setInterval(() => {
        const remaining = calculateTimeLeft();
        setTimeLeft(remaining);

        if (remaining && remaining.total <= 0) {
          clearInterval(timer);
          // Guarantee onExpire is only invoked ONCE, not on every render
          if (!hasExpiredRef.current && typeof onExpireRef.current === 'function') {
            hasExpiredRef.current = true;
            onExpireRef.current();
          }
        }
      }, 1000);

      return () => clearInterval(timer);
    }, [expiresAt]); // Do NOT put onExpire in the dependency array unless wrapped in useCallback

    if (!timeLeft) return null;

    if (timeLeft.total <= 0) {
      return (
        <span
          className="text-red-500 font-semibold text-xs"
          style={{ color: '#EF4444', fontWeight: 600, fontSize: '12px' }}
        >
          Reservation Expired
        </span>
      );
    }

    return (
      <span
        className="text-amber-600 font-mono text-xs"
        style={{
          display: 'inline-flex',
          alignItems: 'center',
          gap: '4px',
          color: '#B45309',
          fontFamily: 'monospace',
          fontSize: '12px',
          fontWeight: 600,
        }}
      >
        <Clock size={13} />
        Expires in: {timeLeft.hours}h {timeLeft.minutes}m {timeLeft.seconds}s
      </span>
    );
  },
  (prevProps, nextProps) => prevProps.expiresAt === nextProps.expiresAt
);

export function MyBookingsPage() {
  const navigate = useNavigate();
  const { accessToken } = useAuth();

  const [activeTab, setActiveTab] = useState('All');
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 10;

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [data, setData] = useState({
    items: [],
    page: 1,
    pageSize: 10,
    totalCount: 0,
    totalPages: 0,
    hasNextPage: false,
    hasPreviousPage: false,
  });

  const [expandedBookingIds, setExpandedBookingIds] = useState(new Set());
  const [copiedRef, setCopiedRef] = useState(null);
  const [payingBookingId, setPayingBookingId] = useState(null);
  const [paymentError, setPaymentError] = useState(null);

  const [bookingToCancel, setBookingToCancel] = useState(null);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelling, setCancelling] = useState(false);
  const [cancelError, setCancelError] = useState(null);
  const [toast, setToast] = useState(null);

  const handleOpenCancelModal = (booking) => {
    setBookingToCancel(booking);
    setCancelReason('');
    setCancelError(null);
  };

  const handleCloseCancelModal = () => {
    if (cancelling) return;
    setBookingToCancel(null);
    setCancelReason('');
    setCancelError(null);
  };

  const handleConfirmCancel = async () => {
    if (!bookingToCancel) return;
    setCancelling(true);
    setCancelError(null);
    try {
      await cancelBooking(bookingToCancel.id, cancelReason || undefined, accessToken);
      setBookingToCancel(null);
      setCancelReason('');
      setToast({ message: 'Booking successfully cancelled', type: 'success' });
      setTimeout(() => setToast(null), 5000);
      await fetchBookings(currentPage, activeTab);
    } catch (err) {
      console.error('Failed to cancel booking:', err);
      setCancelError(err?.message || 'Unable to cancel this booking. Please try again.');
    } finally {
      setCancelling(false);
    }
  };

  const handleResumePayment = async (bookingId) => {
    setPayingBookingId(bookingId);
    setPaymentError(null);
    try {
      const response = await createCheckoutSession(bookingId, accessToken);
      const checkoutUrl = response?.checkoutUrl || response?.data?.checkoutUrl;
      if (checkoutUrl) {
        window.location.href = checkoutUrl;
      } else {
        throw new Error('Checkout session did not return a valid URL.');
      }
    } catch (err) {
      console.error('Failed to initiate payment:', err);
      setPaymentError(err?.message || 'Unable to open checkout. Please try again.');
      setPayingBookingId(null);
    }
  };

  const fetchBookings = useCallback(
    async (page, statusFilter) => {
      setLoading(true);
      setError(null);
      try {
        const filter = statusFilter === 'All' ? undefined : statusFilter;
        const result = await getMyBookings(page, pageSize, filter, accessToken);
        if (result) {
          setData(result);
        }
      } catch (err) {
        console.error('Failed to load bookings:', err);
        setError(
          err?.message ||
            'Unable to load your booking history. Please check your network and try again.'
        );
      } finally {
        setLoading(false);
      }
    },
    [accessToken, pageSize]
  );

  useEffect(() => {
    fetchBookings(currentPage, activeTab);
  }, [fetchBookings, currentPage, activeTab]);

  const handleBookingExpire = useCallback(() => {
    fetchBookings(currentPage, activeTab);
  }, [fetchBookings, currentPage, activeTab]);

  const handleTabChange = (tabValue) => {
    setActiveTab(tabValue);
    setCurrentPage(1);
  };

  const toggleExpand = (id) => {
    setExpandedBookingIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const handleCopy = (ref) => {
    navigator.clipboard.writeText(ref);
    setCopiedRef(ref);
    setTimeout(() => setCopiedRef(null), 2000);
  };

  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column', backgroundColor: 'var(--ep-canvas)' }}>
      <Header />

      <main style={{ flex: 1, padding: '36px 16px 64px' }}>
        <div style={{ maxWidth: '960px', margin: '0 auto' }}>
          {/* Breadcrumb / Back Link */}
          <div style={{ marginBottom: '20px' }}>
            <button
              type="button"
              onClick={() => navigate('/')}
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                background: 'none',
                border: 'none',
                color: 'var(--ep-text-secondary)',
                fontSize: '13px',
                fontWeight: 500,
                cursor: 'pointer',
                padding: 0,
                transition: 'var(--ep-transition)',
              }}
              onMouseEnter={(e) => (e.currentTarget.style.color = 'var(--ep-primary)')}
              onMouseLeave={(e) => (e.currentTarget.style.color = 'var(--ep-text-secondary)')}
            >
              <ArrowLeft size={16} />
              <span>Back to Events</span>
            </button>
          </div>

          {/* Page Header */}
          <div style={{
            display: 'flex',
            flexDirection: 'column',
            gap: '8px',
            marginBottom: '28px',
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
              <div style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                width: '42px',
                height: '42px',
                borderRadius: '12px',
                backgroundColor: 'var(--ep-soft-accent)',
                color: 'var(--ep-primary)',
              }}>
                <Receipt size={22} />
              </div>
              <div>
                <h1 style={{
                  margin: 0,
                  fontSize: '28px',
                  fontWeight: 800,
                  color: 'var(--ep-text-primary)',
                  letterSpacing: '-0.5px',
                  fontFamily: 'var(--ep-font-heading)',
                }}>
                  My Booking History
                </h1>
                <p style={{
                  margin: '4px 0 0',
                  fontSize: '14px',
                  color: 'var(--ep-text-secondary)',
                }}>
                  Track your reservations, review payment status, and view digital tickets.
                </p>
              </div>
            </div>
          </div>

          {/* Status Filter Tabs */}
          <div style={{
            display: 'flex',
            gap: '8px',
            marginBottom: '24px',
            overflowX: 'auto',
            paddingBottom: '4px',
          }}>
            {STATUS_TABS.map((tab) => {
              const isActive = activeTab === tab.value;
              return (
                <button
                  key={tab.value}
                  type="button"
                  id={`filter-tab-${tab.value}`}
                  onClick={() => handleTabChange(tab.value)}
                  style={{
                    padding: '8px 18px',
                    borderRadius: 'var(--ep-radius-pill)',
                    fontSize: '13px',
                    fontWeight: isActive ? 600 : 500,
                    backgroundColor: isActive ? 'var(--ep-primary)' : '#ffffff',
                    color: isActive ? '#ffffff' : 'var(--ep-text-primary)',
                    border: `1px solid ${isActive ? 'var(--ep-primary)' : 'var(--ep-border)'}`,
                    cursor: 'pointer',
                    transition: 'var(--ep-transition)',
                    whiteSpace: 'nowrap',
                    boxShadow: isActive ? '0 2px 8px rgba(255, 91, 0, 0.25)' : 'none',
                  }}
                  onMouseEnter={(e) => {
                    if (!isActive) {
                      e.currentTarget.style.backgroundColor = 'var(--ep-canvas)';
                      e.currentTarget.style.borderColor = 'var(--ep-text-secondary)';
                    }
                  }}
                  onMouseLeave={(e) => {
                    if (!isActive) {
                      e.currentTarget.style.backgroundColor = '#ffffff';
                      e.currentTarget.style.borderColor = 'var(--ep-border)';
                    }
                  }}
                >
                  {tab.label}
                </button>
              );
            })}
          </div>

          {/* Loading State */}
          {loading && (
            <div style={{
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center',
              padding: '64px 20px',
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              boxShadow: 'var(--ep-shadow-card)',
            }}>
              <Loader2 size={36} color="var(--ep-primary)" className="spinner-border text-primary border-0" style={{ animation: 'spin 1s linear infinite' }} />
              <p style={{ marginTop: '16px', color: 'var(--ep-text-secondary)', fontSize: '14px', fontWeight: 500 }}>
                Loading your booking history...
              </p>
            </div>
          )}

          {/* Error State */}
          {!loading && error && (
            <div style={{
              padding: '32px 24px',
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-danger)',
              boxShadow: 'var(--ep-shadow-card)',
              textAlign: 'center',
            }}>
              <AlertCircle size={40} color="var(--ep-danger)" style={{ margin: '0 auto 12px' }} />
              <h3 style={{ fontSize: '18px', fontWeight: 700, color: 'var(--ep-text-primary)', marginBottom: '8px' }}>
                Failed to Load Bookings
              </h3>
              <p style={{ fontSize: '14px', color: 'var(--ep-text-secondary)', maxWidth: '440px', margin: '0 auto 20px' }}>
                {error}
              </p>
              <button
                type="button"
                onClick={() => fetchBookings(currentPage, activeTab)}
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '8px',
                  backgroundColor: 'var(--ep-primary)',
                  color: '#ffffff',
                  border: 'none',
                  padding: '10px 20px',
                  borderRadius: 'var(--ep-radius-btn)',
                  fontSize: '13px',
                  fontWeight: 600,
                  cursor: 'pointer',
                  transition: 'var(--ep-transition)',
                }}
                onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = 'var(--ep-primary-hover)')}
                onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = 'var(--ep-primary)')}
              >
                <RotateCcw size={15} />
                <span>Retry</span>
              </button>
            </div>
          )}

          {/* Empty State */}
          {!loading && !error && data.items.length === 0 && (
            <div style={{
              padding: '56px 24px',
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              boxShadow: 'var(--ep-shadow-card)',
              textAlign: 'center',
            }}>
              <div style={{
                width: '64px',
                height: '64px',
                borderRadius: '50%',
                backgroundColor: 'var(--ep-soft-accent)',
                color: 'var(--ep-primary)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                margin: '0 auto 20px',
              }}>
                <Ticket size={32} />
              </div>
              <h3 style={{
                fontSize: '20px',
                fontWeight: 700,
                color: 'var(--ep-text-primary)',
                marginBottom: '8px',
                fontFamily: 'var(--ep-font-heading)',
              }}>
                You haven&apos;t made any bookings yet.
              </h3>
              <p style={{
                fontSize: '14px',
                color: 'var(--ep-text-secondary)',
                maxWidth: '420px',
                margin: '0 auto 24px',
                lineHeight: 1.5,
              }}>
                {activeTab === 'All'
                  ? 'Ready to experience unforgettable events? Browse available events and book your tickets today.'
                  : `No bookings found matching status "${activeTab}".`}
              </p>
              <Link
                to="/"
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '8px',
                  backgroundColor: 'var(--ep-primary)',
                  color: '#ffffff',
                  textDecoration: 'none',
                  padding: '12px 24px',
                  borderRadius: 'var(--ep-radius-btn)',
                  fontSize: '14px',
                  fontWeight: 600,
                  boxShadow: '0 2px 8px rgba(255, 91, 0, 0.25)',
                  transition: 'var(--ep-transition)',
                }}
                onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = 'var(--ep-primary-hover)')}
                onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = 'var(--ep-primary)')}
              >
                <span>Explore Events</span>
                <ArrowRight size={16} />
              </Link>
            </div>
          )}

          {/* Payment Error Banner */}
          {paymentError && (
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                padding: '12px 16px',
                marginBottom: '16px',
                backgroundColor: '#FEF2F2',
                border: '1px solid #FECACA',
                borderRadius: 'var(--ep-radius-btn)',
                color: '#991B1B',
                fontSize: '13px',
                fontWeight: 500,
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <AlertCircle size={16} />
                <span>{paymentError}</span>
              </div>
              <button
                type="button"
                onClick={() => setPaymentError(null)}
                style={{
                  background: 'none',
                  border: 'none',
                  color: '#991B1B',
                  cursor: 'pointer',
                  fontWeight: 700,
                  fontSize: '14px',
                }}
              >
                ✕
              </button>
            </div>
          )}

          {/* Bookings List */}
          {!loading && !error && data.items.length > 0 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              {data.items.map((booking) => {
                const statusCfg = getStatusBadgeConfig(booking.status);
                const StatusIcon = statusCfg.icon;
                const isExpanded = expandedBookingIds.has(booking.id);
                const isConfirmed = booking.status?.toLowerCase() === 'confirmed';

                return (
                  <div
                    key={booking.id}
                    id={`booking-card-${booking.bookingReference}`}
                    style={{
                      backgroundColor: '#ffffff',
                      borderRadius: 'var(--ep-radius-card)',
                      border: '1px solid var(--ep-border)',
                      boxShadow: 'var(--ep-shadow-card)',
                      padding: '24px',
                      transition: 'var(--ep-transition)',
                    }}
                  >
                    {/* Header Row: Reference + Status Badge */}
                    <div style={{
                      display: 'flex',
                      flexWrap: 'wrap',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      gap: '12px',
                      paddingBottom: '16px',
                      borderBottom: '1px solid var(--ep-border)',
                    }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                        <span style={{
                          fontSize: '16px',
                          fontWeight: 700,
                          color: 'var(--ep-text-primary)',
                          fontFamily: 'monospace',
                          letterSpacing: '0.5px',
                        }}>
                          {booking.bookingReference}
                        </span>

                        <button
                          type="button"
                          onClick={() => handleCopy(booking.bookingReference)}
                          title="Copy booking reference"
                          aria-label="Copy booking reference"
                          style={{
                            background: 'none',
                            border: 'none',
                            color: copiedRef === booking.bookingReference ? 'var(--ep-success)' : 'var(--ep-text-secondary)',
                            cursor: 'pointer',
                            padding: '4px',
                            display: 'flex',
                            alignItems: 'center',
                            transition: 'var(--ep-transition)',
                          }}
                        >
                          {copiedRef === booking.bookingReference ? <Check size={14} /> : <Copy size={14} />}
                        </button>
                      </div>

                      <div style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '6px',
                        padding: '4px 12px',
                        borderRadius: 'var(--ep-radius-pill)',
                        backgroundColor: statusCfg.bg,
                        color: statusCfg.color,
                        border: `1px solid ${statusCfg.border}`,
                        fontSize: '12px',
                        fontWeight: 600,
                      }}>
                        <StatusIcon size={14} />
                        <span>{statusCfg.label}</span>
                      </div>
                    </div>

                    {/* Middle Details Grid */}
                    <div style={{
                      display: 'grid',
                      gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
                      gap: '16px',
                      padding: '16px 0',
                    }}>
                      <div>
                        <div style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', textTransform: 'uppercase', fontWeight: 600, marginBottom: '4px' }}>
                          Booking Date
                        </div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px', color: 'var(--ep-text-primary)', fontWeight: 500 }}>
                          <Calendar size={14} color="var(--ep-text-secondary)" />
                          <span>{formatDate(booking.createdAt)}</span>
                        </div>
                      </div>

                      <div>
                        <div style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', textTransform: 'uppercase', fontWeight: 600, marginBottom: '4px' }}>
                          Tickets Reserved
                        </div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px', color: 'var(--ep-text-primary)', fontWeight: 500 }}>
                          <Ticket size={14} color="var(--ep-text-secondary)" />
                          <span>{booking.totalTickets} {booking.totalTickets === 1 ? 'ticket' : 'tickets'}</span>
                        </div>
                      </div>

                      <div>
                        <div style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', textTransform: 'uppercase', fontWeight: 600, marginBottom: '4px' }}>
                          Total Amount
                        </div>
                        <div style={{ fontSize: '16px', fontWeight: 800, color: 'var(--ep-primary)', fontFamily: 'var(--ep-font-heading)' }}>
                          {formatCurrency(booking.totalAmount)}
                        </div>
                      </div>
                    </div>

                    {/* Expandable Items List */}
                    {booking.items && booking.items.length > 0 && (
                      <div style={{ marginTop: '8px', borderTop: '1px dashed var(--ep-border)', paddingTop: '12px' }}>
                        <button
                          type="button"
                          onClick={() => toggleExpand(booking.id)}
                          style={{
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'space-between',
                            width: '100%',
                            background: 'none',
                            border: 'none',
                            padding: '6px 0',
                            fontSize: '13px',
                            fontWeight: 600,
                            color: 'var(--ep-text-secondary)',
                            cursor: 'pointer',
                          }}
                        >
                          <span>{isExpanded ? 'Hide itemized breakdown' : `View itemized tickets (${booking.items.length})`}</span>
                          {isExpanded ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
                        </button>

                        {isExpanded && (
                          <div style={{
                            marginTop: '10px',
                            backgroundColor: 'var(--ep-canvas)',
                            borderRadius: 'var(--ep-radius-btn)',
                            padding: '12px 16px',
                            display: 'flex',
                            flexDirection: 'column',
                            gap: '8px',
                          }}>
                            {booking.items.map((item, idx) => (
                              <div
                                key={item.ticketTypeId || idx}
                                style={{
                                  display: 'flex',
                                  alignItems: 'center',
                                  justifyContent: 'space-between',
                                  fontSize: '13px',
                                  padding: '4px 0',
                                }}
                              >
                                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                  <span style={{ fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                                    {item.ticketName}
                                  </span>
                                  <span style={{ color: 'var(--ep-text-secondary)' }}>
                                    × {item.quantity}
                                  </span>
                                </div>
                                <div style={{ fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                                  {formatCurrency(item.subtotal)}
                                </div>
                              </div>
                            ))}
                            {isConfirmed && (
                              <div style={{ marginTop: '8px', paddingTop: '8px', borderTop: '1px dashed var(--ep-border)', display: 'flex', justifyContent: 'flex-end' }}>
                                <Link
                                  to={`/bookings/${booking.id}/tickets`}
                                  style={{
                                    fontSize: '12px',
                                    fontWeight: 600,
                                    color: 'var(--ep-primary)',
                                    textDecoration: 'none',
                                    display: 'inline-flex',
                                    alignItems: 'center',
                                    gap: '4px',
                                  }}
                                >
                                  <span>Manage & Cancel Individual Tickets →</span>
                                </Link>
                              </div>
                            )}
                          </div>
                        )}
                      </div>
                    )}

                    {/* Card Actions Footer */}
                    <div style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      marginTop: '16px',
                      paddingTop: '16px',
                      borderTop: '1px solid var(--ep-border)',
                      flexWrap: 'wrap',
                      gap: '12px',
                    }}>
                      <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                        {isConfirmed && booking.confirmedAt && (
                          <span>Confirmed on {formatDate(booking.confirmedAt)}</span>
                        )}
                        {!isConfirmed && booking.status === 'PendingPayment' && (
                          <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                            <span style={{ color: '#B45309', fontWeight: 500 }}>
                              Payment pending completion. Tickets are reserved.
                            </span>
                            {booking.expiresAt && (
                              <CountdownTimer expiresAt={booking.expiresAt} onExpire={handleBookingExpire} />
                            )}
                          </div>
                        )}
                        {booking.status === 'Cancelled' && (
                          <span style={{ color: '#B91C1C', fontWeight: 500 }}>
                            This booking was cancelled.
                          </span>
                        )}
                      </div>

                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
                        {/* Cancel Booking for Confirmed or PendingPayment */}
                        {(isConfirmed || booking.status === 'PendingPayment') && (
                          <button
                            type="button"
                            id={`cancel-booking-${booking.bookingReference}`}
                            onClick={() => handleOpenCancelModal(booking)}
                            style={{
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: '6px',
                              backgroundColor: 'transparent',
                              color: 'var(--ep-danger)',
                              border: '1px solid #FECACA',
                              padding: '8px 14px',
                              borderRadius: 'var(--ep-radius-btn)',
                              fontSize: '13px',
                              fontWeight: 600,
                              cursor: 'pointer',
                              transition: 'var(--ep-transition)',
                            }}
                            onMouseEnter={(e) => {
                              e.currentTarget.style.backgroundColor = '#FEF2F2';
                              e.currentTarget.style.borderColor = 'var(--ep-danger)';
                            }}
                            onMouseLeave={(e) => {
                              e.currentTarget.style.backgroundColor = 'transparent';
                              e.currentTarget.style.borderColor = '#FECACA';
                            }}
                          >
                            <Ban size={14} />
                            <span>Cancel Booking</span>
                          </button>
                        )}

                        {/* Complete Payment / Pay Now for Pending Bookings */}
                        {!isConfirmed && booking.status === 'PendingPayment' && (
                          <button
                            type="button"
                            id={`pay-now-${booking.bookingReference}`}
                            onClick={() => handleResumePayment(booking.id)}
                            disabled={payingBookingId === booking.id}
                            style={{
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: '8px',
                              backgroundColor: 'var(--ep-primary)',
                              color: '#ffffff',
                              border: 'none',
                              padding: '8px 18px',
                              borderRadius: 'var(--ep-radius-btn)',
                              fontSize: '13px',
                              fontWeight: 600,
                              boxShadow: '0 2px 6px rgba(255, 91, 0, 0.25)',
                              cursor: payingBookingId === booking.id ? 'not-allowed' : 'pointer',
                              opacity: payingBookingId === booking.id ? 0.75 : 1,
                              transition: 'var(--ep-transition)',
                            }}
                            onMouseEnter={(e) => {
                              if (payingBookingId !== booking.id) {
                                e.currentTarget.style.backgroundColor = 'var(--ep-primary-hover)';
                              }
                            }}
                            onMouseLeave={(e) => {
                              if (payingBookingId !== booking.id) {
                                e.currentTarget.style.backgroundColor = 'var(--ep-primary)';
                              }
                            }}
                          >
                            {payingBookingId === booking.id ? (
                              <>
                                <Loader2 size={15} style={{ animation: 'spin 1s linear infinite' }} />
                                <span>Redirecting...</span>
                              </>
                            ) : (
                              <>
                                <CreditCard size={15} />
                                <span>Pay Now</span>
                              </>
                            )}
                          </button>
                        )}

                        {isConfirmed && (
                          <Link
                            to={`/bookings/${booking.id}/tickets`}
                            id={`view-tickets-${booking.bookingReference}`}
                            style={{
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: '8px',
                              backgroundColor: 'var(--ep-primary)',
                              color: '#ffffff',
                              textDecoration: 'none',
                              padding: '8px 18px',
                              borderRadius: 'var(--ep-radius-btn)',
                              fontSize: '13px',
                              fontWeight: 600,
                              boxShadow: '0 2px 6px rgba(255, 91, 0, 0.25)',
                              transition: 'var(--ep-transition)',
                            }}
                            onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = 'var(--ep-primary-hover)')}
                            onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = 'var(--ep-primary)')}
                          >
                            <Ticket size={15} />
                            <span>View Digital Tickets</span>
                          </Link>
                        )}
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}

          {/* Pagination Controls */}
          {!loading && !error && data.totalPages > 1 && (
            <div style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: '16px',
              marginTop: '32px',
            }}>
              <button
                type="button"
                id="pagination-prev"
                disabled={!data.hasPreviousPage}
                onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
                style={{
                  padding: '8px 16px',
                  borderRadius: 'var(--ep-radius-btn)',
                  border: '1px solid var(--ep-border)',
                  backgroundColor: !data.hasPreviousPage ? 'var(--ep-canvas)' : '#ffffff',
                  color: !data.hasPreviousPage ? 'var(--ep-text-secondary)' : 'var(--ep-text-primary)',
                  fontSize: '13px',
                  fontWeight: 600,
                  cursor: !data.hasPreviousPage ? 'not-allowed' : 'pointer',
                  transition: 'var(--ep-transition)',
                }}
              >
                Previous
              </button>

              <span style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', fontWeight: 500 }}>
                Page {data.page} of {data.totalPages}
              </span>

              <button
                type="button"
                id="pagination-next"
                disabled={!data.hasNextPage}
                onClick={() => setCurrentPage((p) => p + 1)}
                style={{
                  padding: '8px 16px',
                  borderRadius: 'var(--ep-radius-btn)',
                  border: '1px solid var(--ep-border)',
                  backgroundColor: !data.hasNextPage ? 'var(--ep-canvas)' : '#ffffff',
                  color: !data.hasNextPage ? 'var(--ep-text-secondary)' : 'var(--ep-text-primary)',
                  fontSize: '13px',
                  fontWeight: 600,
                  cursor: !data.hasNextPage ? 'not-allowed' : 'pointer',
                  transition: 'var(--ep-transition)',
                }}
              >
                Next
              </button>
            </div>
          )}
        </div>
      </main>

      {/* Floating Toast Notification */}
      {toast && (
        <div
          role="status"
          style={{
            position: 'fixed',
            bottom: '24px',
            right: '24px',
            zIndex: 1100,
            display: 'flex',
            alignItems: 'center',
            gap: '10px',
            backgroundColor: toast.type === 'error' ? '#FEF2F2' : '#ECFDF5',
            color: toast.type === 'error' ? '#991B1B' : '#065F46',
            border: `1px solid ${toast.type === 'error' ? '#FECACA' : '#A7F3D0'}`,
            borderRadius: 'var(--ep-radius-btn)',
            padding: '12px 20px',
            boxShadow: '0 10px 25px rgba(0, 0, 0, 0.12)',
            fontSize: '14px',
            fontWeight: 600,
          }}
        >
          {toast.type === 'error' ? <AlertCircle size={18} /> : <CheckCircle2 size={18} />}
          <span>{toast.message}</span>
          <button
            type="button"
            onClick={() => setToast(null)}
            style={{
              background: 'none',
              border: 'none',
              cursor: 'pointer',
              padding: '2px',
              marginLeft: '8px',
              color: 'inherit',
              display: 'inline-flex',
            }}
          >
            <X size={16} />
          </button>
        </div>
      )}

      {/* Cancellation Confirmation Dialog */}
      {bookingToCancel && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="cancel-modal-title"
          style={{
            position: 'fixed',
            inset: 0,
            backgroundColor: 'rgba(0, 0, 0, 0.5)',
            backdropFilter: 'blur(4px)',
            zIndex: 1050,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            padding: '16px',
          }}
          onClick={handleCloseCancelModal}
        >
          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              maxWidth: '480px',
              width: '100%',
              boxShadow: '0 20px 40px rgba(0, 0, 0, 0.2)',
              overflow: 'hidden',
              display: 'flex',
              flexDirection: 'column',
            }}
            onClick={(e) => e.stopPropagation()}
          >
            {/* Modal Header */}
            <div style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              padding: '20px 24px',
              borderBottom: '1px solid var(--ep-border)',
            }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                <div style={{
                  width: '36px',
                  height: '36px',
                  borderRadius: '50%',
                  backgroundColor: '#FEF2F2',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: 'var(--ep-danger)',
                }}>
                  <AlertTriangle size={20} />
                </div>
                <h3 id="cancel-modal-title" style={{ margin: 0, fontSize: '18px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                  Cancel Booking
                </h3>
              </div>
              <button
                type="button"
                onClick={handleCloseCancelModal}
                disabled={cancelling}
                style={{
                  background: 'none',
                  border: 'none',
                  color: 'var(--ep-text-secondary)',
                  cursor: cancelling ? 'not-allowed' : 'pointer',
                  padding: '4px',
                  borderRadius: '6px',
                }}
              >
                <X size={20} />
              </button>
            </div>

            {/* Modal Body */}
            <div style={{ padding: '24px', display: 'flex', flexDirection: 'column', gap: '16px' }}>
              <div style={{
                backgroundColor: '#FEF2F2',
                border: '1px solid #FECACA',
                borderRadius: '10px',
                padding: '14px 16px',
                color: '#991B1B',
                fontSize: '13px',
                lineHeight: 1.5,
              }}>
                <strong>Warning:</strong> Cancelling booking{' '}
                <span style={{ fontFamily: 'monospace', fontWeight: 700 }}>{bookingToCancel.bookingReference}</span>{' '}
                will immediately release your reserved tickets back to the available inventory. Any issued digital tickets will be permanently invalidated.
              </div>

              {cancelError && (
                <div style={{
                  backgroundColor: '#FEF2F2',
                  border: '1px solid #FECACA',
                  borderRadius: '8px',
                  padding: '10px 14px',
                  color: '#B91C1C',
                  fontSize: '13px',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '8px',
                }}>
                  <AlertCircle size={16} />
                  <span>{cancelError}</span>
                </div>
              )}

              <div>
                <label
                  htmlFor="cancel-reason"
                  style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}
                >
                  Reason for cancellation (optional):
                </label>
                <textarea
                  id="cancel-reason"
                  rows={3}
                  value={cancelReason}
                  onChange={(e) => setCancelReason(e.target.value)}
                  placeholder="e.g., Unable to attend, scheduled conflict..."
                  disabled={cancelling}
                  style={{
                    width: '100%',
                    padding: '10px 12px',
                    borderRadius: '8px',
                    border: '1px solid var(--ep-border)',
                    fontSize: '13px',
                    fontFamily: 'inherit',
                    resize: 'vertical',
                    outline: 'none',
                    transition: 'var(--ep-transition)',
                  }}
                  onFocus={(e) => (e.target.style.borderColor = 'var(--ep-primary)')}
                  onBlur={(e) => (e.target.style.borderColor = 'var(--ep-border)')}
                />
              </div>
            </div>

            {/* Modal Footer */}
            <div style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'flex-end',
              gap: '12px',
              padding: '16px 24px',
              backgroundColor: '#FAFAFA',
              borderTop: '1px solid var(--ep-border)',
            }}>
              <button
                type="button"
                onClick={handleCloseCancelModal}
                disabled={cancelling}
                style={{
                  backgroundColor: '#ffffff',
                  color: 'var(--ep-text-primary)',
                  border: '1px solid var(--ep-border)',
                  padding: '8px 16px',
                  borderRadius: 'var(--ep-radius-btn)',
                  fontSize: '13px',
                  fontWeight: 600,
                  cursor: cancelling ? 'not-allowed' : 'pointer',
                  transition: 'var(--ep-transition)',
                }}
              >
                Keep Booking
              </button>
              <button
                type="button"
                id="confirm-cancel-booking-btn"
                onClick={handleConfirmCancel}
                disabled={cancelling}
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '8px',
                  backgroundColor: 'var(--ep-danger)',
                  color: '#ffffff',
                  border: 'none',
                  padding: '8px 18px',
                  borderRadius: 'var(--ep-radius-btn)',
                  fontSize: '13px',
                  fontWeight: 600,
                  boxShadow: '0 2px 6px rgba(239, 68, 68, 0.3)',
                  cursor: cancelling ? 'not-allowed' : 'pointer',
                  opacity: cancelling ? 0.75 : 1,
                  transition: 'var(--ep-transition)',
                }}
              >
                {cancelling ? (
                  <>
                    <Loader2 size={15} style={{ animation: 'spin 1s linear infinite' }} />
                    <span>Cancelling...</span>
                  </>
                ) : (
                  <>
                    <Ban size={15} />
                    <span>Yes, Cancel Booking</span>
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default MyBookingsPage;
