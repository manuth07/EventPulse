import React, { useState, useEffect, useCallback } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { useAuth } from '../../context/AuthContext';
import { getBookingSummary } from '../../services/bookingService';
import { formatPrice } from '../../utils/currencyFormatter';
import {
  CheckCircle2,
  AlertTriangle,
  XCircle,
  ArrowRight,
  RotateCcw,
  Loader2,
  Ticket,
  Calendar,
  MapPin,
  RefreshCw,
} from 'lucide-react';

function formatDate(dateString) {
  if (!dateString) return null;
  try {
    const d = new Date(dateString);
    const day = d.getDate();
    const month = d.toLocaleDateString('en-US', { month: 'short' });
    const time = d.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit', hour12: true });
    return `${day} ${month} · ${time}`;
  } catch (e) {
    return dateString;
  }
}

export function PaymentSuccess() {
  const [searchParams] = useSearchParams();
  const sessionId = searchParams.get('session_id');
  const queryBookingId = searchParams.get('booking_id') || searchParams.get('bookingId');
  const { accessToken } = useAuth();

  const [bookingInfo, setBookingInfo] = useState(() => {
    try {
      const rawPending = sessionStorage.getItem('ep_pending_booking');
      const rawLast = sessionStorage.getItem('ep_last_booking');
      if (rawPending) {
        const parsed = JSON.parse(rawPending);
        sessionStorage.setItem('ep_last_booking', rawPending);
        sessionStorage.removeItem('ep_pending_booking');
        return parsed;
      }
      if (rawLast) {
        return JSON.parse(rawLast);
      }
    } catch (e) {
      console.warn('Failed to parse booking from sessionStorage', e);
    }
    return null;
  });

  const [status, setStatus] = useState(() => bookingInfo?.status || 'PendingPayment');
  const [isPolling, setIsPolling] = useState(true);
  const [pollTimedOut, setPollTimedOut] = useState(false);
  const [manualChecking, setManualChecking] = useState(false);
  const [pollError, setPollError] = useState(null);

  const targetBookingId = queryBookingId || bookingInfo?.bookingId;

  // Poll for booking confirmation while status is PendingPayment
  useEffect(() => {
    if (!targetBookingId) {
      setIsPolling(false);
      return;
    }

    let isMounted = true;
    let pollCount = 0;
    const MAX_POLLS = 20; // 20 * 3000ms = 60s
    let intervalId = null;

    const checkStatus = async () => {
      try {
        const summary = await getBookingSummary(targetBookingId, accessToken);
        if (!isMounted || !summary) return false;

        setPollError(null);
        if (summary.status) {
          setStatus(summary.status);
          setBookingInfo((prev) => ({
            ...prev,
            bookingReference: summary.bookingReference || prev?.bookingReference,
            totalAmount: summary.totalAmount ?? prev?.totalAmount,
            status: summary.status,
          }));

          // Terminal statuses: stop polling
          if (
            summary.status === 'Confirmed' ||
            summary.status === 'PaymentFailed' ||
            summary.status === 'Cancelled'
          ) {
            setIsPolling(false);
            return false;
          }
        }
      } catch (err) {
        if (!isMounted) return false;
        console.warn('Booking status poll error:', err);
      }

      pollCount++;
      if (pollCount >= MAX_POLLS) {
        if (isMounted) {
          setIsPolling(false);
          setPollTimedOut(true);
        }
        return false;
      }

      return true;
    };

    // Execute first check, then set interval if still pending
    checkStatus().then((shouldContinue) => {
      if (!isMounted || !shouldContinue) return;

      intervalId = setInterval(async () => {
        const cont = await checkStatus();
        if (!cont && intervalId) {
          clearInterval(intervalId);
        }
      }, 3000);
    });

    return () => {
      isMounted = false;
      if (intervalId) {
        clearInterval(intervalId);
      }
    };
  }, [targetBookingId, accessToken]);

  const handleManualCheck = useCallback(async () => {
    if (!targetBookingId || manualChecking) return;
    setManualChecking(true);
    setPollError(null);
    try {
      const summary = await getBookingSummary(targetBookingId, accessToken);
      if (summary?.status) {
        setStatus(summary.status);
        setBookingInfo((prev) => ({
          ...prev,
          bookingReference: summary.bookingReference || prev?.bookingReference,
          totalAmount: summary.totalAmount ?? prev?.totalAmount,
          status: summary.status,
        }));
      }
    } catch (e) {
      setPollError('Unable to refresh status. Please try again.');
    } finally {
      setManualChecking(false);
    }
  }, [targetBookingId, accessToken, manualChecking]);

  const ticketCount =
    bookingInfo?.totalTicketCount ||
    (bookingInfo?.items || []).reduce((acc, it) => acc + (it.quantity || 0), 0) ||
    null;

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
        style={{
          flex: 1,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          padding: '40px 16px',
        }}
      >
        <div
          style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            boxShadow: 'var(--ep-shadow-hover)',
            padding: '48px 32px',
            maxWidth: '540px',
            width: '100%',
            textAlign: 'center',
          }}
        >
          {/* ==================== STATE 1: PENDING PAYMENT ==================== */}
          {status === 'PendingPayment' && (
            <>
              {/* Subtle Loading Icon */}
              <div
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  width: '64px',
                  height: '64px',
                  borderRadius: '50%',
                  backgroundColor: 'rgba(255, 91, 0, 0.08)',
                  marginBottom: '20px',
                }}
              >
                <Loader2 size={32} className="ep-spin" style={{ color: 'var(--ep-primary)', flexShrink: 0 }} />
              </div>

              <h1
                style={{
                  fontSize: '24px',
                  fontWeight: 800,
                  color: 'var(--ep-text-primary)',
                  marginBottom: '8px',
                  letterSpacing: '-0.01em',
                }}
              >
                Payment Submitted
              </h1>

              <p
                style={{
                  fontSize: '15px',
                  fontWeight: 600,
                  color: 'var(--ep-primary)',
                  marginBottom: '12px',
                }}
              >
                Your booking is being confirmed.
              </p>

              <p
                style={{
                  fontSize: '14px',
                  color: 'var(--ep-text-secondary)',
                  marginBottom: '24px',
                  lineHeight: 1.5,
                }}
              >
                Your payment has been received and we are confirming your booking.
              </p>

              {/* Booking Context Card (if available) */}
              {bookingInfo && (
                <div
                  style={{
                    backgroundColor: 'var(--ep-canvas)',
                    border: '1px solid var(--ep-border)',
                    borderRadius: '12px',
                    padding: '16px 20px',
                    marginBottom: '24px',
                    textAlign: 'left',
                  }}
                >
                  {bookingInfo.bookingReference && (
                    <div
                      style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        marginBottom: '8px',
                      }}
                    >
                      <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                        Booking Reference
                      </span>
                      <span
                        style={{
                          fontSize: '13px',
                          fontWeight: 700,
                          color: 'var(--ep-text-primary)',
                          fontFamily: 'monospace',
                        }}
                      >
                        #{bookingInfo.bookingReference}
                      </span>
                    </div>
                  )}

                  {bookingInfo.eventTitle && (
                    <div
                      style={{
                        fontSize: '14px',
                        fontWeight: 700,
                        color: 'var(--ep-text-primary)',
                        marginBottom: '4px',
                      }}
                    >
                      {bookingInfo.eventTitle}
                    </div>
                  )}

                  <div
                    style={{
                      display: 'flex',
                      justifyContent: 'space-between',
                      fontSize: '13px',
                      color: 'var(--ep-text-secondary)',
                      marginTop: '8px',
                    }}
                  >
                    <span>
                      {ticketCount
                        ? `${ticketCount} ${ticketCount === 1 ? 'ticket' : 'tickets'}`
                        : 'Tickets reserved'}
                    </span>
                    {bookingInfo.totalAmount > 0 && (
                      <span style={{ fontWeight: 700, color: 'var(--ep-primary)' }}>
                        {formatPrice(bookingInfo.totalAmount)}
                      </span>
                    )}
                  </div>
                </div>
              )}

              {/* Status polling note or timeout */}
              {isPolling && (
                <div
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '6px',
                    fontSize: '12px',
                    color: 'var(--ep-text-secondary)',
                    marginBottom: '24px',
                  }}
                >
                  <Loader2 size={13} className="ep-spin" style={{ flexShrink: 0 }} />
                  <span>Checking for confirmation…</span>
                </div>
              )}

              {pollTimedOut && (
                <div
                  style={{
                    padding: '12px',
                    backgroundColor: '#FFF8F2',
                    border: '1px solid #FFE4CC',
                    borderRadius: '8px',
                    marginBottom: '20px',
                    fontSize: '13px',
                    color: 'var(--ep-text-secondary)',
                    lineHeight: 1.4,
                  }}
                >
                  <div>
                    Confirmation is taking a little longer than usual. Your order is safely recorded.
                  </div>
                  <button
                    type="button"
                    onClick={handleManualCheck}
                    disabled={manualChecking}
                    style={{
                      marginTop: '8px',
                      background: 'none',
                      border: 'none',
                      color: 'var(--ep-primary)',
                      fontSize: '12px',
                      fontWeight: 700,
                      cursor: manualChecking ? 'not-allowed' : 'pointer',
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '4px',
                    }}
                  >
                    <RefreshCw size={12} className={manualChecking ? 'ep-spin' : ''} />
                    <span>{manualChecking ? 'Checking…' : 'Check Status Again'}</span>
                  </button>
                </div>
              )}

              {pollError && (
                <div style={{ fontSize: '12px', color: 'var(--ep-danger)', marginBottom: '16px' }}>
                  {pollError}
                </div>
              )}

              <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                <Link
                  to="/"
                  className="ep-btn-primary"
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: '8px',
                    padding: '12px 24px',
                    fontSize: '14px',
                    fontWeight: 600,
                    textDecoration: 'none',
                    borderRadius: 'var(--ep-radius-btn)',
                  }}
                >
                  <span>Browse More Events</span>
                  <ArrowRight size={16} />
                </Link>
              </div>
            </>
          )}

          {/* ==================== STATE 2: CONFIRMED ==================== */}
          {status === 'Confirmed' && (
            <>
              {/* Confirmed Success Icon */}
              <div
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  width: '64px',
                  height: '64px',
                  borderRadius: '50%',
                  backgroundColor: '#F0FDF4',
                  border: '2px solid #BBF7D0',
                  marginBottom: '20px',
                }}
              >
                <CheckCircle2 size={36} color="#16A34A" />
              </div>

              <h1
                style={{
                  fontSize: '24px',
                  fontWeight: 800,
                  color: 'var(--ep-text-primary)',
                  marginBottom: '8px',
                  letterSpacing: '-0.01em',
                }}
              >
                Booking Confirmed
              </h1>

              <p
                style={{
                  fontSize: '15px',
                  fontWeight: 600,
                  color: '#15803D',
                  marginBottom: '12px',
                }}
              >
                You're all set!
              </p>

              <p
                style={{
                  fontSize: '14px',
                  color: 'var(--ep-text-secondary)',
                  marginBottom: '24px',
                  lineHeight: 1.5,
                }}
              >
                Your payment has been verified and your booking is confirmed.
              </p>

              {/* Order Summary Card */}
              <div
                style={{
                  backgroundColor: 'var(--ep-canvas)',
                  border: '1px solid var(--ep-border)',
                  borderRadius: '12px',
                  padding: '20px',
                  marginBottom: '28px',
                  textAlign: 'left',
                }}
              >
                {bookingInfo?.bookingReference && (
                  <div
                    style={{
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'center',
                      paddingBottom: '12px',
                      marginBottom: '12px',
                      borderBottom: '1px solid var(--ep-border)',
                    }}
                  >
                    <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                      Booking Reference
                    </span>
                    <span
                      style={{
                        fontSize: '14px',
                        fontWeight: 700,
                        color: 'var(--ep-text-primary)',
                        fontFamily: 'monospace',
                      }}
                    >
                      #{bookingInfo.bookingReference}
                    </span>
                  </div>
                )}

                {bookingInfo?.eventTitle && (
                  <div style={{ marginBottom: '10px' }}>
                    <div
                      style={{
                        fontSize: '11px',
                        fontWeight: 700,
                        textTransform: 'uppercase',
                        color: 'var(--ep-primary)',
                        marginBottom: '2px',
                      }}
                    >
                      Event
                    </div>
                    <div
                      style={{
                        fontSize: '15px',
                        fontWeight: 700,
                        color: 'var(--ep-text-primary)',
                      }}
                    >
                      {bookingInfo.eventTitle}
                    </div>
                  </div>
                )}

                {(bookingInfo?.eventVenue || bookingInfo?.eventDate) && (
                  <div
                    style={{
                      display: 'flex',
                      flexWrap: 'wrap',
                      gap: '12px',
                      fontSize: '12px',
                      color: 'var(--ep-text-secondary)',
                      marginBottom: '12px',
                    }}
                  >
                    {bookingInfo.eventVenue && (
                      <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                        <MapPin size={12} />
                        <span>{bookingInfo.eventVenue}</span>
                      </span>
                    )}
                    {bookingInfo.eventDate && (
                      <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                        <Calendar size={12} />
                        <span>{formatDate(bookingInfo.eventDate)}</span>
                      </span>
                    )}
                  </div>
                )}

                <div
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    paddingTop: '10px',
                    borderTop: '1px solid var(--ep-border)',
                  }}
                >
                  <span style={{ fontSize: '13px', color: 'var(--ep-text-secondary)' }}>
                    {ticketCount
                      ? `${ticketCount} ${ticketCount === 1 ? 'Ticket' : 'Tickets'} Paid`
                      : 'Tickets Paid'}
                  </span>
                  {bookingInfo?.totalAmount > 0 && (
                    <span style={{ fontSize: '18px', fontWeight: 800, color: 'var(--ep-primary)' }}>
                      {formatPrice(bookingInfo.totalAmount)}
                    </span>
                  )}
                </div>
              </div>

              {/* Action Buttons */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                <Link
                  to={`/bookings/${targetBookingId}/tickets`}
                  className="ep-btn-primary"
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: '8px',
                    padding: '12px 24px',
                    fontSize: '14px',
                    fontWeight: 600,
                    textDecoration: 'none',
                    borderRadius: 'var(--ep-radius-btn)',
                  }}
                >
                  <Ticket size={16} />
                  <span>View Tickets</span>
                </Link>

                <Link
                  to="/"
                  className="ep-btn-secondary"
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: '8px',
                    padding: '12px 24px',
                    fontSize: '14px',
                    fontWeight: 600,
                    textDecoration: 'none',
                    borderRadius: 'var(--ep-radius-btn)',
                  }}
                >
                  <span>Browse More Events</span>
                  <ArrowRight size={16} />
                </Link>
              </div>
            </>
          )}

          {/* ==================== STATE 3: PAYMENT FAILED ==================== */}
          {status === 'PaymentFailed' && (
            <>
              {/* Failure Icon */}
              <div
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  width: '64px',
                  height: '64px',
                  borderRadius: '50%',
                  backgroundColor: '#FEF2F2',
                  border: '2px solid #FECACA',
                  marginBottom: '20px',
                }}
              >
                <AlertTriangle size={36} color="var(--ep-danger)" />
              </div>

              <h1
                style={{
                  fontSize: '24px',
                  fontWeight: 800,
                  color: 'var(--ep-text-primary)',
                  marginBottom: '8px',
                  letterSpacing: '-0.01em',
                }}
              >
                Payment Failed
              </h1>

              <p
                style={{
                  fontSize: '15px',
                  fontWeight: 600,
                  color: 'var(--ep-danger)',
                  marginBottom: '12px',
                }}
              >
                We were unable to complete your payment.
              </p>

              <p
                style={{
                  fontSize: '14px',
                  color: 'var(--ep-text-secondary)',
                  marginBottom: '24px',
                  lineHeight: 1.5,
                }}
              >
                The payment processor could not process the transaction. You can return to your cart
                to retry payment for your booking.
              </p>

              {bookingInfo?.bookingReference && (
                <div
                  style={{
                    backgroundColor: 'var(--ep-canvas)',
                    border: '1px solid var(--ep-border)',
                    borderRadius: '8px',
                    padding: '12px',
                    marginBottom: '24px',
                    fontSize: '13px',
                    color: 'var(--ep-text-secondary)',
                  }}
                >
                  <span>Booking Reference: </span>
                  <span style={{ fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                    #{bookingInfo.bookingReference}
                  </span>
                </div>
              )}

              <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                <Link
                  to="/cart"
                  className="ep-btn-primary"
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: '8px',
                    padding: '12px 24px',
                    fontSize: '14px',
                    fontWeight: 600,
                    textDecoration: 'none',
                    borderRadius: 'var(--ep-radius-btn)',
                  }}
                >
                  <RotateCcw size={16} />
                  <span>Return to Cart & Retry Payment</span>
                </Link>

                <Link
                  to="/"
                  className="ep-btn-secondary"
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: '8px',
                    padding: '12px 24px',
                    fontSize: '14px',
                    fontWeight: 600,
                    textDecoration: 'none',
                    borderRadius: 'var(--ep-radius-btn)',
                  }}
                >
                  <span>Browse Other Events</span>
                </Link>
              </div>
            </>
          )}

          {/* ==================== STATE 4: CANCELLED ==================== */}
          {status === 'Cancelled' && (
            <>
              {/* Cancelled Icon */}
              <div
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  width: '64px',
                  height: '64px',
                  borderRadius: '50%',
                  backgroundColor: '#F5F5F7',
                  border: '2px solid var(--ep-border)',
                  marginBottom: '20px',
                }}
              >
                <XCircle size={36} color="var(--ep-text-secondary)" />
              </div>

              <h1
                style={{
                  fontSize: '24px',
                  fontWeight: 800,
                  color: 'var(--ep-text-primary)',
                  marginBottom: '8px',
                  letterSpacing: '-0.01em',
                }}
              >
                Booking Cancelled
              </h1>

              <p
                style={{
                  fontSize: '15px',
                  fontWeight: 600,
                  color: 'var(--ep-text-secondary)',
                  marginBottom: '12px',
                }}
              >
                This booking has been cancelled.
              </p>

              <p
                style={{
                  fontSize: '14px',
                  color: 'var(--ep-text-secondary)',
                  marginBottom: '24px',
                  lineHeight: 1.5,
                }}
              >
                The reservation was cancelled and no charges were completed. You can select tickets
                again at any time.
              </p>

              <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                <Link
                  to="/"
                  className="ep-btn-primary"
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: '8px',
                    padding: '12px 24px',
                    fontSize: '14px',
                    fontWeight: 600,
                    textDecoration: 'none',
                    borderRadius: 'var(--ep-radius-btn)',
                  }}
                >
                  <span>Browse Events</span>
                  <ArrowRight size={16} />
                </Link>
              </div>
            </>
          )}

          {/* De-emphasized Session ID Footnote */}
          {sessionId && (
            <div
              style={{
                marginTop: '24px',
                fontSize: '11px',
                color: 'var(--ep-text-secondary)',
                fontFamily: 'monospace',
                opacity: 0.6,
                wordBreak: 'break-all',
              }}
            >
              Session: {sessionId}
            </div>
          )}
        </div>
      </main>
    </div>
  );
}

export default PaymentSuccess;
