import React, { useState, useEffect, useCallback, useRef } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { Layout } from '../../components/layout/Layout';
import { useAuth } from '../../context/AuthContext';
import { getBookingDetail, cancelBooking } from '../../services/bookingService';
import { createCheckoutSession } from '../../services/paymentService';
import {
  formatTicketHistoryDate,
  formatCurrency,
  getStatusBadgeConfig,
} from '../../utils/ticketHistoryHelper';
import {
  Ticket,
  Calendar,
  Clock,
  MapPin,
  ArrowLeft,
  RotateCcw,
  Loader2,
  Copy,
  Check,
  Receipt,
  CreditCard,
  Ban,
  AlertTriangle,
  X,
  ExternalLink,
  ShieldCheck,
  ChevronRight,
} from 'lucide-react';

/**
 * Live countdown timer for reservations pending payment.
 */
function CountdownTimer({ expiresAt, onExpire }) {
  const [timeLeft, setTimeLeft] = useState('');
  const [isExpired, setIsExpired] = useState(false);

  useEffect(() => {
    if (!expiresAt) return;

    function calculate() {
      const diff = new Date(expiresAt).getTime() - Date.now();
      if (diff <= 0) {
        setTimeLeft('Expired');
        setIsExpired(true);
        if (onExpire) onExpire();
        return;
      }
      const mins = Math.floor(diff / 60000);
      const secs = Math.floor((diff % 60000) / 1000);
      setTimeLeft(`${mins}m ${secs.toString().padStart(2, '0')}s remaining`);
    }

    calculate();
    const timer = setInterval(calculate, 1000);
    return () => clearInterval(timer);
  }, [expiresAt, onExpire]);

  if (!expiresAt) return null;

  return (
    <span
      data-testid="booking-expiry-timer"
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: '4px',
        fontSize: '12px',
        fontWeight: 600,
        color: isExpired ? '#B91C1C' : '#B45309',
        backgroundColor: isExpired ? '#FEF2F2' : '#FFFBEB',
        padding: '2px 8px',
        borderRadius: '6px',
        border: `1px solid ${isExpired ? '#FECACA' : '#FDE68A'}`,
      }}
    >
      <Clock size={12} />
      <span>{timeLeft}</span>
    </span>
  );
}

export function BookingDetailPage() {
  const { bookingId } = useParams();
  const { accessToken } = useAuth();
  const navigate = useNavigate();

  const [loading, setLoading] = useState(true);
  const [booking, setBooking] = useState(null);
  const [error, setError] = useState(null);
  const [errorCode, setErrorCode] = useState(null); // 'FORBIDDEN' | 'NOT_FOUND' | 'ERROR'
  const [copiedRef, setCopiedRef] = useState(false);
  const [copiedToken, setCopiedToken] = useState(null);

  // Cancellation modal state
  const [cancelModalOpen, setCancelModalOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [isCancelling, setIsCancelling] = useState(false);
  const [cancelError, setCancelError] = useState(null);
  const [toastMessage, setToastMessage] = useState(null);

  // Pay now state
  const [isRedirectingPayment, setIsRedirectingPayment] = useState(false);
  const [paymentError, setPaymentError] = useState(null);

  const fetchDetail = useCallback(async () => {
    if (!bookingId) return;

    setLoading(true);
    setError(null);
    setErrorCode(null);

    try {
      const data = await getBookingDetail(bookingId, accessToken);
      setBooking(data);
    } catch (err) {
      console.error('Failed to load booking detail:', err);
      const status = err?.status || err?.response?.status;
      if (status === 403) {
        setErrorCode('FORBIDDEN');
        setError('You are not authorized to view this booking.');
      } else if (status === 404) {
        setErrorCode('NOT_FOUND');
        setError('Booking not found or has been removed.');
      } else {
        setErrorCode('ERROR');
        setError('Unable to load booking details at this time. Please check your connection.');
      }
    } finally {
      setLoading(false);
    }
  }, [bookingId, accessToken]);

  useEffect(() => {
    fetchDetail();
  }, [fetchDetail]);

  const handleCopy = (text, type = 'ref') => {
    if (!text) return;
    navigator.clipboard.writeText(text).then(() => {
      if (type === 'ref') {
        setCopiedRef(true);
        setTimeout(() => setCopiedRef(false), 2000);
      } else {
        setCopiedToken(text);
        setTimeout(() => setCopiedToken(null), 2000);
      }
    });
  };

  const handleOpenCancelModal = () => {
    setCancelReason('');
    setCancelError(null);
    setCancelModalOpen(true);
  };

  const handleCloseCancelModal = () => {
    if (isCancelling) return;
    setCancelModalOpen(false);
    setCancelReason('');
    setCancelError(null);
  };

  const handleConfirmCancel = async () => {
    if (!booking) return;

    setIsCancelling(true);
    setCancelError(null);

    try {
      const result = await cancelBooking(booking.id, cancelReason.trim() || undefined, accessToken);
      setBooking((prev) => (prev ? { ...prev, status: result?.newStatus || 'Cancelled' } : null));
      setToastMessage(`Booking #${booking.bookingReference} has been successfully cancelled.`);
      setCancelModalOpen(false);
    } catch (err) {
      console.error('Failed to cancel booking:', err);
      const msg =
        err?.response?.data?.message ||
        err?.data?.message ||
        err?.message ||
        'Failed to cancel booking. Please try again.';
      setCancelError(msg);
    } finally {
      setIsCancelling(false);
    }
  };

  const handleResumePayment = async () => {
    if (!booking) return;

    setIsRedirectingPayment(true);
    setPaymentError(null);

    try {
      const session = await createCheckoutSession(booking.id, accessToken);
      if (session?.checkoutUrl) {
        window.location.href = session.checkoutUrl;
      } else {
        setPaymentError('Unable to initialize payment session. Please try again.');
        setIsRedirectingPayment(false);
      }
    } catch (err) {
      console.error('Payment redirect failed:', err);
      const msg =
        err?.response?.data?.message ||
        err?.data?.message ||
        err?.message ||
        'Unable to initialize payment session. Please try again.';
      setPaymentError(msg);
      setIsRedirectingPayment(false);
    }
  };

  const isConfirmed = booking?.status?.toLowerCase() === 'confirmed';
  const isPending = booking?.status?.toLowerCase() === 'pendingpayment';
  const isCancelled = booking?.status?.toLowerCase() === 'cancelled';
  const statusCfg = booking ? getStatusBadgeConfig(booking.status) : null;
  const StatusIcon = statusCfg?.icon || ShieldCheck;

  return (
    <Layout>
      <div
        data-testid="booking-detail-page"
        style={{
          minHeight: '100vh',
          backgroundColor: 'var(--ep-canvas)',
          padding: '32px 16px 64px',
        }}
      >
        <div style={{ maxWidth: '900px', margin: '0 auto', width: '100%' }}>
          {/* Breadcrumb Navigation */}
          <div style={{ marginBottom: '24px' }}>
            <button
              type="button"
              data-testid="back-to-bookings-btn"
              onClick={() => navigate('/my-bookings')}
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '8px',
                background: 'none',
                border: 'none',
                color: 'var(--ep-text-secondary)',
                fontSize: '14px',
                fontWeight: 600,
                cursor: 'pointer',
                padding: 0,
                transition: 'var(--ep-transition)',
              }}
              onMouseEnter={(e) => (e.currentTarget.style.color = 'var(--ep-primary)')}
              onMouseLeave={(e) => (e.currentTarget.style.color = 'var(--ep-text-secondary)')}
            >
              <ArrowLeft size={16} />
              <span>Back to Booked Tickets</span>
            </button>
          </div>

          {/* Toast Notification */}
          {toastMessage && (
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                padding: '12px 16px',
                marginBottom: '20px',
                backgroundColor: '#ECFDF5',
                border: '1px solid #A7F3D0',
                borderRadius: '8px',
                color: '#065F46',
                fontSize: '14px',
                fontWeight: 500,
              }}
            >
              <span>{toastMessage}</span>
              <button
                type="button"
                onClick={() => setToastMessage(null)}
                style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#065F46' }}
              >
                <X size={16} />
              </button>
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
                marginBottom: '20px',
                backgroundColor: '#FEF2F2',
                border: '1px solid #FECACA',
                borderRadius: '8px',
                color: '#991B1B',
                fontSize: '14px',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <AlertTriangle size={16} />
                <span>{paymentError}</span>
              </div>
              <button
                type="button"
                onClick={() => setPaymentError(null)}
                style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#991B1B' }}
              >
                <X size={16} />
              </button>
            </div>
          )}

          {/* ==================== STATE 1: LOADING ==================== */}
          {loading && (
            <div
              data-testid="booking-detail-loading"
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card)',
                border: '1px solid var(--ep-border)',
                padding: '64px 24px',
                textAlign: 'center',
                boxShadow: 'var(--ep-shadow-card)',
              }}
            >
              <div
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  width: '56px',
                  height: '56px',
                  borderRadius: '50%',
                  backgroundColor: 'rgba(255, 91, 0, 0.08)',
                  marginBottom: '16px',
                }}
              >
                <Loader2 size={28} style={{ color: 'var(--ep-primary)', animation: 'spin 1s linear infinite' }} />
              </div>
              <h2 style={{ fontSize: '18px', fontWeight: 700, color: 'var(--ep-text-primary)', margin: '0 0 8px' }}>
                Loading Booking Details…
              </h2>
              <p style={{ fontSize: '14px', color: 'var(--ep-text-secondary)', margin: 0 }}>
                Retrieving full authoritative booking records from EventPulse.
              </p>
            </div>
          )}

          {/* ==================== STATE 2: 403 FORBIDDEN ==================== */}
          {!loading && errorCode === 'FORBIDDEN' && (
            <div
              data-testid="booking-forbidden"
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card)',
                border: '1px solid var(--ep-border)',
                padding: '56px 24px',
                textAlign: 'center',
                boxShadow: 'var(--ep-shadow-card)',
              }}
            >
              <div
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  width: '64px',
                  height: '64px',
                  borderRadius: '50%',
                  backgroundColor: '#FEF2F2',
                  color: 'var(--ep-danger)',
                  marginBottom: '16px',
                }}
              >
                <AlertTriangle size={32} />
              </div>
              <h2 style={{ fontSize: '22px', fontWeight: 800, color: 'var(--ep-text-primary)', marginBottom: '8px' }}>
                Access Denied
              </h2>
              <p style={{ fontSize: '14px', color: 'var(--ep-text-secondary)', maxWidth: '440px', margin: '0 auto 24px' }}>
                You do not have authorization to view this booking. It belongs to another customer account.
              </p>
              <button
                type="button"
                onClick={() => navigate('/my-bookings')}
                style={{
                  padding: '10px 20px',
                  backgroundColor: 'var(--ep-primary)',
                  color: '#ffffff',
                  border: 'none',
                  borderRadius: 'var(--ep-radius-btn)',
                  fontSize: '14px',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                Return to My Bookings
              </button>
            </div>
          )}

          {/* ==================== STATE 3: 404 NOT FOUND ==================== */}
          {!loading && errorCode === 'NOT_FOUND' && (
            <div
              data-testid="booking-not-found"
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card)',
                border: '1px solid var(--ep-border)',
                padding: '56px 24px',
                textAlign: 'center',
                boxShadow: 'var(--ep-shadow-card)',
              }}
            >
              <div
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  width: '64px',
                  height: '64px',
                  borderRadius: '50%',
                  backgroundColor: 'var(--ep-soft-accent)',
                  color: 'var(--ep-primary)',
                  marginBottom: '16px',
                }}
              >
                <Receipt size={32} />
              </div>
              <h2 style={{ fontSize: '22px', fontWeight: 800, color: 'var(--ep-text-primary)', marginBottom: '8px' }}>
                Booking Not Found
              </h2>
              <p style={{ fontSize: '14px', color: 'var(--ep-text-secondary)', maxWidth: '440px', margin: '0 auto 24px' }}>
                The requested booking reference could not be found. It may have expired or been removed.
              </p>
              <button
                type="button"
                onClick={() => navigate('/my-bookings')}
                style={{
                  padding: '10px 20px',
                  backgroundColor: 'var(--ep-primary)',
                  color: '#ffffff',
                  border: 'none',
                  borderRadius: 'var(--ep-radius-btn)',
                  fontSize: '14px',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                Back to My Bookings
              </button>
            </div>
          )}

          {/* ==================== STATE 4: GENERIC ERROR ==================== */}
          {!loading && error && !errorCode && (
            <div
              data-testid="booking-detail-error"
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card)',
                border: '1px solid var(--ep-border)',
                padding: '48px 24px',
                textAlign: 'center',
                boxShadow: 'var(--ep-shadow-card)',
              }}
            >
              <div
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  width: '56px',
                  height: '56px',
                  borderRadius: '50%',
                  backgroundColor: '#FFF8F2',
                  color: 'var(--ep-primary)',
                  marginBottom: '16px',
                }}
              >
                <AlertTriangle size={28} />
              </div>
              <h2 style={{ fontSize: '20px', fontWeight: 800, color: 'var(--ep-text-primary)', marginBottom: '8px' }}>
                Unable to Load Booking
              </h2>
              <p style={{ fontSize: '14px', color: 'var(--ep-text-secondary)', marginBottom: '20px' }}>
                {error}
              </p>
              <button
                type="button"
                onClick={fetchDetail}
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '8px',
                  padding: '10px 20px',
                  backgroundColor: 'var(--ep-primary)',
                  color: '#ffffff',
                  border: 'none',
                  borderRadius: 'var(--ep-radius-btn)',
                  fontSize: '14px',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                <RotateCcw size={15} />
                <span>Retry</span>
              </button>
            </div>
          )}

          {/* ==================== STATE 5: AUTHORITATIVE DETAIL VIEW ==================== */}
          {!loading && !error && booking && (
            <div
              data-testid="booking-detail-content"
              style={{
                display: 'flex',
                flexDirection: 'column',
                gap: '24px',
              }}
            >
              {/* Header Card */}
              <div
                style={{
                  backgroundColor: '#ffffff',
                  borderRadius: 'var(--ep-radius-card)',
                  border: '1px solid var(--ep-border)',
                  padding: '28px',
                  boxShadow: 'var(--ep-shadow-card)',
                }}
              >
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'flex-start',
                    justifyContent: 'space-between',
                    gap: '16px',
                    flexWrap: 'wrap',
                    marginBottom: '16px',
                  }}
                >
                  <div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '8px' }}>
                      <span
                        style={{
                          fontSize: '13px',
                          fontWeight: 600,
                          color: 'var(--ep-text-secondary)',
                          textTransform: 'uppercase',
                          letterSpacing: '0.5px',
                        }}
                      >
                        Booking Reference:
                      </span>
                      <span
                        data-testid="booking-reference"
                        style={{
                          fontFamily: 'monospace',
                          fontSize: '15px',
                          fontWeight: 700,
                          color: 'var(--ep-text-primary)',
                          backgroundColor: 'var(--ep-canvas)',
                          padding: '3px 8px',
                          borderRadius: '6px',
                          border: '1px solid var(--ep-border)',
                        }}
                      >
                        #{booking.bookingReference}
                      </span>
                      <button
                        type="button"
                        data-testid="copy-reference-btn"
                        onClick={() => handleCopy(booking.bookingReference, 'ref')}
                        title="Copy Booking Reference"
                        style={{
                          background: 'none',
                          border: 'none',
                          cursor: 'pointer',
                          color: copiedRef ? '#059669' : 'var(--ep-text-secondary)',
                          padding: '4px',
                          display: 'flex',
                          alignItems: 'center',
                        }}
                      >
                        {copiedRef ? <Check size={16} /> : <Copy size={16} />}
                      </button>
                    </div>

                    <h1
                      data-testid="booking-event-name"
                      style={{
                        margin: 0,
                        fontSize: '26px',
                        fontWeight: 800,
                        color: 'var(--ep-text-primary)',
                        fontFamily: 'var(--ep-font-heading)',
                        letterSpacing: '-0.5px',
                      }}
                    >
                      {booking.eventName || booking.eventTitle || 'Event Admission'}
                    </h1>
                  </div>

                  {/* Status Badge */}
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <div
                      data-testid="booking-status-badge"
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '6px',
                        padding: '6px 14px',
                        borderRadius: 'var(--ep-radius-pill)',
                        fontSize: '13px',
                        fontWeight: 700,
                        backgroundColor: statusCfg?.bg || '#F3F4F6',
                        color: statusCfg?.color || '#374151',
                        border: `1px solid ${statusCfg?.border || '#E5E7EB'}`,
                      }}
                    >
                      <StatusIcon size={15} />
                      <span>{statusCfg?.label || booking.status}</span>
                    </div>
                  </div>
                </div>

                {/* Event Meta Sub-row */}
                <div
                  style={{
                    display: 'flex',
                    flexWrap: 'wrap',
                    gap: '20px',
                    paddingTop: '16px',
                    borderTop: '1px solid var(--ep-border)',
                    fontSize: '14px',
                    color: 'var(--ep-text-secondary)',
                  }}
                >
                  {(booking.eventDate || booking.eventTime) && (
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <Calendar size={16} color="var(--ep-primary)" />
                      <span data-testid="booking-event-date" style={{ color: 'var(--ep-text-primary)', fontWeight: 500 }}>
                        {formatTicketHistoryDate(booking.eventDate)}
                      </span>
                    </div>
                  )}

                  {booking.eventVenue && (
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <MapPin size={16} color="var(--ep-primary)" />
                      <span data-testid="booking-event-venue" style={{ color: 'var(--ep-text-primary)', fontWeight: 500 }}>
                        {booking.eventVenue}
                      </span>
                    </div>
                  )}

                  {booking.eventId && (
                    <div style={{ marginLeft: 'auto' }}>
                      <Link
                        to={`/events/${booking.eventId}`}
                        data-testid="view-event-page-link"
                        style={{
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '4px',
                          color: 'var(--ep-primary)',
                          textDecoration: 'none',
                          fontSize: '13px',
                          fontWeight: 600,
                        }}
                      >
                        <span>View Event Details</span>
                        <ChevronRight size={14} />
                      </Link>
                    </div>
                  )}
                </div>
              </div>

              {/* Booking Summary & Dates Grid */}
              <div
                style={{
                  display: 'grid',
                  gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
                  gap: '16px',
                }}
              >
                <div
                  style={{
                    backgroundColor: '#ffffff',
                    borderRadius: 'var(--ep-radius-card)',
                    border: '1px solid var(--ep-border)',
                    padding: '20px',
                    boxShadow: 'var(--ep-shadow-card)',
                  }}
                >
                  <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', textTransform: 'uppercase', fontWeight: 600, marginBottom: '6px' }}>
                    Booking Placed
                  </div>
                  <div data-testid="booking-created-at" style={{ fontSize: '14px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                    {formatTicketHistoryDate(booking.createdAt)}
                  </div>
                </div>

                <div
                  style={{
                    backgroundColor: '#ffffff',
                    borderRadius: 'var(--ep-radius-card)',
                    border: '1px solid var(--ep-border)',
                    padding: '20px',
                    boxShadow: 'var(--ep-shadow-card)',
                  }}
                >
                  <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', textTransform: 'uppercase', fontWeight: 600, marginBottom: '6px' }}>
                    {isConfirmed ? 'Confirmation Date' : isPending ? 'Reservation Expiry' : 'Status'}
                  </div>
                  <div data-testid="booking-status-timestamp" style={{ fontSize: '14px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                    {isConfirmed && booking.confirmedAt ? (
                      formatTicketHistoryDate(booking.confirmedAt)
                    ) : isPending && booking.expiresAt ? (
                      <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                        <span>{formatTicketHistoryDate(booking.expiresAt)}</span>
                        <CountdownTimer expiresAt={booking.expiresAt} onExpire={fetchDetail} />
                      </div>
                    ) : (
                      <span>{booking.status}</span>
                    )}
                  </div>
                </div>

                <div
                  style={{
                    backgroundColor: '#ffffff',
                    borderRadius: 'var(--ep-radius-card)',
                    border: '1px solid var(--ep-border)',
                    padding: '20px',
                    boxShadow: 'var(--ep-shadow-card)',
                  }}
                >
                  <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', textTransform: 'uppercase', fontWeight: 600, marginBottom: '6px' }}>
                    Total Tickets
                  </div>
                  <div data-testid="booking-total-tickets" style={{ fontSize: '16px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                    {booking.totalTickets} {booking.totalTickets === 1 ? 'Ticket' : 'Tickets'}
                  </div>
                </div>

                <div
                  style={{
                    backgroundColor: '#ffffff',
                    borderRadius: 'var(--ep-radius-card)',
                    border: '1px solid var(--ep-border)',
                    padding: '20px',
                    boxShadow: 'var(--ep-shadow-card)',
                  }}
                >
                  <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', textTransform: 'uppercase', fontWeight: 600, marginBottom: '6px' }}>
                    Total Paid / Due
                  </div>
                  <div
                    data-testid="booking-total-amount"
                    style={{
                      fontSize: '20px',
                      fontWeight: 800,
                      color: 'var(--ep-primary)',
                      fontFamily: 'var(--ep-font-heading)',
                    }}
                  >
                    {formatCurrency(booking.totalAmount)}
                  </div>
                </div>
              </div>

              {/* Itemized Breakdown Card */}
              <div
                style={{
                  backgroundColor: '#ffffff',
                  borderRadius: 'var(--ep-radius-card)',
                  border: '1px solid var(--ep-border)',
                  padding: '24px',
                  boxShadow: 'var(--ep-shadow-card)',
                }}
              >
                <h3
                  style={{
                    margin: '0 0 16px',
                    fontSize: '18px',
                    fontWeight: 700,
                    color: 'var(--ep-text-primary)',
                    fontFamily: 'var(--ep-font-heading)',
                  }}
                >
                  Itemized Ticket Breakdown
                </h3>

                <div style={{ overflowX: 'auto' }}>
                  <table
                    data-testid="booking-items-table"
                    style={{
                      width: '100%',
                      borderCollapse: 'collapse',
                      fontSize: '14px',
                      textAlign: 'left',
                    }}
                  >
                    <thead>
                      <tr style={{ borderBottom: '2px solid var(--ep-border)', color: 'var(--ep-text-secondary)' }}>
                        <th style={{ padding: '10px 12px', fontWeight: 600 }}>Ticket Type</th>
                        <th style={{ padding: '10px 12px', fontWeight: 600, textAlign: 'center' }}>Quantity</th>
                        <th style={{ padding: '10px 12px', fontWeight: 600, textAlign: 'right' }}>Unit Price</th>
                        <th style={{ padding: '10px 12px', fontWeight: 600, textAlign: 'right' }}>Subtotal</th>
                      </tr>
                    </thead>
                    <tbody>
                      {booking.items && booking.items.length > 0 ? (
                        booking.items.map((item, idx) => (
                          <tr
                            key={item.ticketTypeId || idx}
                            data-testid={`booking-item-row-${idx}`}
                            style={{
                              borderBottom: '1px solid var(--ep-border)',
                            }}
                          >
                            <td style={{ padding: '12px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                              {item.ticketName}
                            </td>
                            <td style={{ padding: '12px', textAlign: 'center', color: 'var(--ep-text-primary)' }}>
                              {item.quantity}
                            </td>
                            <td style={{ padding: '12px', textAlign: 'right', color: 'var(--ep-text-secondary)' }}>
                              {formatCurrency(item.unitPrice)}
                            </td>
                            <td style={{ padding: '12px', textAlign: 'right', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                              {formatCurrency(item.subtotal)}
                            </td>
                          </tr>
                        ))
                      ) : (
                        <tr>
                          <td colSpan={4} style={{ padding: '16px', textAlign: 'center', color: 'var(--ep-text-secondary)' }}>
                            No item breakdown available.
                          </td>
                        </tr>
                      )}
                    </tbody>
                    <tfoot>
                      <tr>
                        <td colSpan={3} style={{ padding: '16px 12px 8px', textAlign: 'right', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                          Total Amount:
                        </td>
                        <td
                          style={{
                            padding: '16px 12px 8px',
                            textAlign: 'right',
                            fontWeight: 800,
                            fontSize: '16px',
                            color: 'var(--ep-primary)',
                            fontFamily: 'var(--ep-font-heading)',
                          }}
                        >
                          {formatCurrency(booking.totalAmount)}
                        </td>
                      </tr>
                    </tfoot>
                  </table>
                </div>
              </div>

              {/* Digital Tickets Gateway & Actions Card */}
              <div
                style={{
                  backgroundColor: '#ffffff',
                  borderRadius: 'var(--ep-radius-card)',
                  border: '1px solid var(--ep-border)',
                  padding: '24px',
                  boxShadow: 'var(--ep-shadow-card)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  flexWrap: 'wrap',
                  gap: '16px',
                }}
              >
                <div>
                  <h4 style={{ margin: '0 0 4px', fontSize: '16px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                    {isConfirmed ? 'Digital Admission Passes' : 'Booking Management'}
                  </h4>
                  <p style={{ margin: 0, fontSize: '13px', color: 'var(--ep-text-secondary)' }}>
                    {isConfirmed
                      ? 'Access unique QR codes, gate entry passes, and individual ticket management.'
                      : isPending
                      ? 'Complete payment before expiry to generate and claim your admission passes.'
                      : 'This booking has been cancelled and its admission passes are invalidated.'}
                  </p>
                </div>

                <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' }}>
                  {/* Cancel Booking Action */}
                  {(isConfirmed || isPending) && (
                    <button
                      type="button"
                      data-testid="cancel-booking-detail-btn"
                      onClick={handleOpenCancelModal}
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '6px',
                        backgroundColor: 'transparent',
                        color: 'var(--ep-danger)',
                        border: '1px solid #FECACA',
                        padding: '10px 16px',
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
                      <Ban size={15} />
                      <span>Cancel Entire Booking</span>
                    </button>
                  )}

                  {/* Pay Now for Pending */}
                  {isPending && (
                    <button
                      type="button"
                      data-testid="pay-now-detail-btn"
                      onClick={handleResumePayment}
                      disabled={isRedirectingPayment}
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
                        boxShadow: '0 2px 6px rgba(255, 91, 0, 0.25)',
                        cursor: isRedirectingPayment ? 'not-allowed' : 'pointer',
                        opacity: isRedirectingPayment ? 0.75 : 1,
                        transition: 'var(--ep-transition)',
                      }}
                    >
                      {isRedirectingPayment ? (
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

                  {/* View Digital Tickets (Confirmed) */}
                  {isConfirmed && (
                    <Link
                      to={`/bookings/${booking.id}/tickets`}
                      data-testid="view-digital-tickets-btn"
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '8px',
                        backgroundColor: 'var(--ep-primary)',
                        color: '#ffffff',
                        textDecoration: 'none',
                        padding: '10px 20px',
                        borderRadius: 'var(--ep-radius-btn)',
                        fontSize: '13px',
                        fontWeight: 600,
                        boxShadow: '0 2px 6px rgba(255, 91, 0, 0.25)',
                        transition: 'var(--ep-transition)',
                      }}
                      onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = 'var(--ep-primary-hover)')}
                      onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = 'var(--ep-primary)')}
                    >
                      <Ticket size={16} />
                      <span>View Digital Tickets</span>
                    </Link>
                  )}
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Cancellation Modal */}
        {cancelModalOpen && (
          <div
            style={{
              position: 'fixed',
              top: 0,
              left: 0,
              right: 0,
              bottom: 0,
              backgroundColor: 'rgba(0, 0, 0, 0.5)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              zIndex: 9999,
              padding: '16px',
            }}
          >
            <div
              data-testid="cancel-booking-modal"
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card)',
                width: '100%',
                maxWidth: '480px',
                padding: '24px',
                boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1)',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '16px' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                  <div
                    style={{
                      width: '36px',
                      height: '36px',
                      borderRadius: '50%',
                      backgroundColor: '#FEF2F2',
                      color: 'var(--ep-danger)',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                    }}
                  >
                    <Ban size={20} />
                  </div>
                  <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                    Cancel Booking
                  </h3>
                </div>
                <button
                  type="button"
                  onClick={handleCloseCancelModal}
                  style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'var(--ep-text-secondary)' }}
                >
                  <X size={18} />
                </button>
              </div>

              <p style={{ fontSize: '14px', color: 'var(--ep-text-secondary)', marginBottom: '16px', lineHeight: 1.5 }}>
                Are you sure you want to cancel booking <strong>#{booking?.bookingReference}</strong>? All reserved tickets and admissions will be invalidated and released back to event capacity.
              </p>

              {cancelError && (
                <div
                  style={{
                    padding: '10px 12px',
                    backgroundColor: '#FEF2F2',
                    border: '1px solid #FECACA',
                    borderRadius: '6px',
                    color: '#991B1B',
                    fontSize: '13px',
                    marginBottom: '16px',
                  }}
                >
                  {cancelError}
                </div>
              )}

              <div style={{ marginBottom: '20px' }}>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}>
                  Reason for Cancellation (Optional)
                </label>
                <textarea
                  data-testid="cancel-reason-input"
                  value={cancelReason}
                  onChange={(e) => setCancelReason(e.target.value)}
                  placeholder="e.g. Schedule conflict, unable to attend..."
                  rows={3}
                  style={{
                    width: '100%',
                    padding: '10px',
                    borderRadius: '8px',
                    border: '1px solid var(--ep-border)',
                    fontSize: '13px',
                    fontFamily: 'inherit',
                    resize: 'vertical',
                    boxSizing: 'border-box',
                  }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  onClick={handleCloseCancelModal}
                  disabled={isCancelling}
                  style={{
                    padding: '8px 16px',
                    backgroundColor: '#ffffff',
                    border: '1px solid var(--ep-border)',
                    borderRadius: 'var(--ep-radius-btn)',
                    fontSize: '13px',
                    fontWeight: 600,
                    cursor: 'pointer',
                    color: 'var(--ep-text-primary)',
                  }}
                >
                  Keep Booking
                </button>
                <button
                  type="button"
                  data-testid="confirm-cancel-booking-btn"
                  onClick={handleConfirmCancel}
                  disabled={isCancelling}
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '6px',
                    padding: '8px 18px',
                    backgroundColor: 'var(--ep-danger)',
                    border: 'none',
                    borderRadius: 'var(--ep-radius-btn)',
                    fontSize: '13px',
                    fontWeight: 600,
                    cursor: isCancelling ? 'not-allowed' : 'pointer',
                    color: '#ffffff',
                    opacity: isCancelling ? 0.75 : 1,
                  }}
                >
                  {isCancelling ? (
                    <>
                      <Loader2 size={14} style={{ animation: 'spin 1s linear infinite' }} />
                      <span>Cancelling...</span>
                    </>
                  ) : (
                    <span>Confirm Cancellation</span>
                  )}
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </Layout>
  );
}

export default BookingDetailPage;
