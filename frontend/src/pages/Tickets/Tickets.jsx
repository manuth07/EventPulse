import React, { useState, useEffect, useCallback, useRef } from 'react';
import { useParams, Link } from 'react-router-dom';
import { QRCodeSVG } from 'qrcode.react';
import { Header } from '../../components/Header/Header';
import { useAuth } from '../../context/AuthContext';
import { getBookingTickets, cancelTicket } from '../../services/bookingService';
import { fetchEventById } from '../../services/eventService';
import {
  Ticket as TicketIcon,
  Calendar,
  MapPin,
  CheckCircle2,
  AlertTriangle,
  RotateCcw,
  Loader2,
  ArrowLeft,
  Copy,
  Check,
  ShieldCheck,
  Clock,
  ExternalLink,
  Ban,
  X,
  AlertCircle,
} from 'lucide-react';

function formatDate(dateString) {
  if (!dateString) return null;
  try {
    const d = new Date(dateString);
    const day = d.getDate();
    const month = d.toLocaleDateString('en-US', { month: 'short' });
    const year = d.getFullYear();
    const time = d.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit', hour12: true });
    return `${day} ${month} ${year} · ${time}`;
  } catch (_e) {
    return dateString;
  }
}

export function Tickets() {
  const { bookingId } = useParams();
  const { accessToken } = useAuth();

  const [loading, setLoading] = useState(true);
  const [bookingData, setBookingData] = useState(null);
  const [eventData, setEventData] = useState(null);
  const [error, setError] = useState(null);
  const [errorCode, setErrorCode] = useState(null);
  const [copiedCode, setCopiedCode] = useState(null);
  const [isRefreshing, setIsRefreshing] = useState(false);

  // Partial ticket cancellation modal state
  const [cancellingTicket, setCancellingTicket] = useState(null);
  const [cancelReason, setCancelReason] = useState('');
  const [isSubmittingCancel, setIsSubmittingCancel] = useState(false);
  const [cancelError, setCancelError] = useState(null);
  const [successToast, setSuccessToast] = useState(null);

  const pollCountRef = useRef(0);
  const maxPolls = 10;

  // Retrieve cached event title/venue from last booking session if available
  const cachedBookingInfo = (() => {
    try {
      const raw = sessionStorage.getItem('ep_last_booking');
      if (raw) return JSON.parse(raw);
    } catch (_e) {
      // Ignore
    }
    return null;
  })();

  const loadTickets = useCallback(
    async (isManualRefresh = false) => {
      if (!bookingId) return;

      if (isManualRefresh) {
        setIsRefreshing(true);
      }
      setError(null);
      setErrorCode(null);

      try {
        const data = await getBookingTickets(bookingId, accessToken);
        setBookingData(data);

        // Fetch complementary event details if available
        if (data?.eventId && !eventData) {
          try {
            const ev = await fetchEventById(data.eventId);
            if (ev) setEventData(ev);
          } catch (_evErr) {
            // Non-critical: event snapshot fallback from cache will be used
          }
        }
      } catch (err) {
        console.error('Failed to fetch tickets:', err);
        const status = err?.status || err?.response?.status;
        if (status === 403) {
          setErrorCode('FORBIDDEN');
          setError('You are not authorized to view tickets for this booking.');
        } else if (status === 404) {
          setErrorCode('NOT_FOUND');
          setError('Booking not found or no tickets associated with this reference.');
        } else {
          setErrorCode('ERROR');
          setError('Unable to load your tickets at this time. Please check your connection and try again.');
        }
      } finally {
        setLoading(false);
        setIsRefreshing(false);
      }
    },
    [bookingId, accessToken, eventData]
  );

  // Initial load
  useEffect(() => {
    loadTickets();
  }, [loadTickets]);

  // Polling effect when tickets are still pending confirmation or being generated
  useEffect(() => {
    if (!bookingData) return;

    const needsPolling =
      bookingData.bookingStatus === 'PendingPayment' ||
      (bookingData.bookingStatus === 'Confirmed' && (!bookingData.tickets || bookingData.tickets.length === 0));

    if (!needsPolling || pollCountRef.current >= maxPolls) {
      return;
    }

    const timer = setTimeout(() => {
      pollCountRef.current += 1;
      loadTickets();
    }, 3000);

    return () => clearTimeout(timer);
  }, [bookingData, loadTickets]);

  const handleCopyCode = (code) => {
    if (!code) return;
    navigator.clipboard.writeText(code).then(() => {
      setCopiedCode(code);
      setTimeout(() => setCopiedCode(null), 2000);
    });
  };

  const handleOpenCancelModal = (ticket) => {
    setCancellingTicket(ticket);
    setCancelReason('');
    setCancelError(null);
  };

  const handleCloseCancelModal = () => {
    if (isSubmittingCancel) return;
    setCancellingTicket(null);
    setCancelReason('');
    setCancelError(null);
  };

  const handleConfirmCancelTicket = async () => {
    if (!cancellingTicket) return;
    setIsSubmittingCancel(true);
    setCancelError(null);

    try {
      const result = await cancelTicket(
        cancellingTicket.ticketId,
        cancelReason.trim() || undefined,
        accessToken
      );

      // Update state locally without wiping out sibling tickets
      setBookingData((prev) => {
        if (!prev) return prev;
        const updatedTickets = prev.tickets.map((t) =>
          t.ticketId === cancellingTicket.ticketId
            ? { ...t, status: 'Cancelled' }
            : t
        );
        return {
          ...prev,
          bookingStatus: result?.parentBookingStatus || prev.bookingStatus,
          tickets: updatedTickets,
        };
      });

      setSuccessToast(`Ticket #${cancellingTicket.ticketCode} was cancelled successfully. 1 seat capacity released.`);
      setCancellingTicket(null);
      setCancelReason('');
    } catch (err) {
      console.error('Failed to cancel ticket:', err);
      const msg =
        err?.response?.data?.message ||
        err?.data?.message ||
        err?.message ||
        'Failed to cancel the ticket. Please try again.';
      setCancelError(msg);
    } finally {
      setIsSubmittingCancel(false);
    }
  };

  const eventTitle =
    eventData?.title ||
    cachedBookingInfo?.eventTitle ||
    'EventPulse Admission';

  const eventDate =
    eventData?.date ||
    cachedBookingInfo?.eventDate;

  const eventVenue =
    eventData?.venue ||
    eventData?.location ||
    cachedBookingInfo?.eventVenue;

  const totalTickets = bookingData?.tickets?.length || 0;

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

      <main style={{ flex: 1, padding: '32px 16px', maxWidth: '880px', width: '100%', margin: '0 auto' }}>
        {/* Navigation & Breadcrumb */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            marginBottom: '24px',
          }}
        >
          <Link
            to="/"
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '6px',
              fontSize: '14px',
              fontWeight: 600,
              color: 'var(--ep-text-secondary)',
              textDecoration: 'none',
              transition: 'color 0.15s ease',
            }}
          >
            <ArrowLeft size={16} />
            <span>Back to Events</span>
          </Link>

          {bookingData && (
            <button
              type="button"
              onClick={() => loadTickets(true)}
              disabled={isRefreshing}
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                background: 'none',
                border: '1px solid var(--ep-border)',
                borderRadius: '8px',
                padding: '6px 12px',
                fontSize: '13px',
                fontWeight: 600,
                color: 'var(--ep-text-secondary)',
                cursor: isRefreshing ? 'not-allowed' : 'pointer',
                backgroundColor: '#ffffff',
              }}
            >
              <RotateCcw size={13} className={isRefreshing ? 'ep-spin' : ''} />
              <span>{isRefreshing ? 'Refreshing…' : 'Refresh'}</span>
            </button>
          )}
        </div>

        {/* Success / Status Toast Notification */}
        {successToast && (
          <div
            style={{
              marginBottom: '20px',
              padding: '12px 16px',
              backgroundColor: '#ECFDF5',
              border: '1px solid #A7F3D0',
              borderRadius: '8px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: '12px',
              color: '#065F46',
              fontSize: '14px',
              fontWeight: 500,
              boxShadow: '0 2px 4px rgba(0, 0, 0, 0.05)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <CheckCircle2 size={18} color="#059669" />
              <span>{successToast}</span>
            </div>
            <button
              type="button"
              onClick={() => setSuccessToast(null)}
              style={{
                background: 'none',
                border: 'none',
                cursor: 'pointer',
                color: '#065F46',
                padding: '2px',
              }}
            >
              <X size={16} />
            </button>
          </div>
        )}

        {/* ==================== STATE 1: INITIAL LOADING ==================== */}
        {loading && (
          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              padding: '64px 24px',
              textAlign: 'center',
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
              <Loader2 size={28} className="ep-spin" style={{ color: 'var(--ep-primary)' }} />
            </div>
            <h2 style={{ fontSize: '18px', fontWeight: 700, color: 'var(--ep-text-primary)', margin: '0 0 8px 0' }}>
              Loading Your Tickets…
            </h2>
            <p style={{ fontSize: '14px', color: 'var(--ep-text-secondary)', margin: 0 }}>
              Retrieving secure admission passes from EventPulse.
            </p>
          </div>
        )}

        {/* ==================== STATE 2: ERROR ==================== */}
        {!loading && error && (
          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              padding: '48px 32px',
              textAlign: 'center',
              boxShadow: 'var(--ep-shadow-hover)',
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
                backgroundColor: errorCode === 'FORBIDDEN' ? '#FEF2F2' : '#FFF8F2',
                marginBottom: '16px',
              }}
            >
              <AlertTriangle
                size={28}
                style={{ color: errorCode === 'FORBIDDEN' ? 'var(--ep-danger)' : 'var(--ep-primary)' }}
              />
            </div>
            <h2 style={{ fontSize: '20px', fontWeight: 800, color: 'var(--ep-text-primary)', marginBottom: '8px' }}>
              {errorCode === 'FORBIDDEN'
                ? 'Access Denied'
                : errorCode === 'NOT_FOUND'
                ? 'Booking Not Found'
                : 'Unable to Load Tickets'}
            </h2>
            <p style={{ fontSize: '14px', color: 'var(--ep-text-secondary)', marginBottom: '24px', lineHeight: 1.5 }}>
              {error}
            </p>
            <div style={{ display: 'inline-flex', gap: '12px' }}>
              <button
                type="button"
                onClick={() => loadTickets(true)}
                className="ep-btn-primary"
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '8px',
                  padding: '10px 20px',
                  fontSize: '14px',
                  fontWeight: 600,
                  borderRadius: 'var(--ep-radius-btn)',
                  cursor: 'pointer',
                  border: 'none',
                }}
              >
                <RotateCcw size={14} />
                <span>Try Again</span>
              </button>
              <Link
                to="/"
                className="ep-btn-secondary"
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '8px',
                  padding: '10px 20px',
                  fontSize: '14px',
                  fontWeight: 600,
                  borderRadius: 'var(--ep-radius-btn)',
                  textDecoration: 'none',
                }}
              >
                <span>Go to Home</span>
              </Link>
            </div>
          </div>
        )}

        {/* ==================== STATE 3: PENDING PAYMENT ==================== */}
        {!loading && !error && bookingData && bookingData.bookingStatus === 'PendingPayment' && (
          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              padding: '48px 32px',
              textAlign: 'center',
              boxShadow: 'var(--ep-shadow-hover)',
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
                backgroundColor: 'rgba(255, 91, 0, 0.08)',
                marginBottom: '20px',
              }}
            >
              <Loader2 size={32} className="ep-spin" style={{ color: 'var(--ep-primary)' }} />
            </div>

            <h1 style={{ fontSize: '22px', fontWeight: 800, color: 'var(--ep-text-primary)', marginBottom: '8px' }}>
              Booking Pending Confirmation
            </h1>

            <p style={{ fontSize: '15px', fontWeight: 600, color: 'var(--ep-primary)', marginBottom: '12px' }}>
              Tickets will be available once your booking is confirmed.
            </p>

            <p style={{ fontSize: '14px', color: 'var(--ep-text-secondary)', marginBottom: '24px', lineHeight: 1.5, maxWidth: '480px', margin: '0 auto 24px auto' }}>
              Your payment is currently being processed. Once confirmed by the system, your admission tickets and QR codes will appear right here.
            </p>

            {bookingData.bookingReference && (
              <div
                style={{
                  display: 'inline-block',
                  backgroundColor: 'var(--ep-canvas)',
                  border: '1px solid var(--ep-border)',
                  borderRadius: '8px',
                  padding: '8px 16px',
                  fontSize: '13px',
                  color: 'var(--ep-text-primary)',
                  marginBottom: '24px',
                }}
              >
                Reference: <strong style={{ fontFamily: 'monospace' }}>#{bookingData.bookingReference}</strong>
              </div>
            )}

            <div>
              <button
                type="button"
                onClick={() => loadTickets(true)}
                disabled={isRefreshing}
                className="ep-btn-primary"
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '8px',
                  padding: '10px 24px',
                  fontSize: '14px',
                  fontWeight: 600,
                  borderRadius: 'var(--ep-radius-btn)',
                  cursor: isRefreshing ? 'not-allowed' : 'pointer',
                  border: 'none',
                }}
              >
                <RotateCcw size={14} className={isRefreshing ? 'ep-spin' : ''} />
                <span>{isRefreshing ? 'Checking…' : 'Check Status Again'}</span>
              </button>
            </div>
          </div>
        )}

        {/* ==================== STATE 4: CONFIRMED BUT GENERATING TICKETS ==================== */}
        {!loading &&
          !error &&
          bookingData &&
          bookingData.bookingStatus === 'Confirmed' &&
          totalTickets === 0 && (
            <div
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card)',
                border: '1px solid var(--ep-border)',
                padding: '48px 32px',
                textAlign: 'center',
                boxShadow: 'var(--ep-shadow-hover)',
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
                  backgroundColor: '#F0FDF4',
                  border: '2px solid #BBF7D0',
                  marginBottom: '20px',
                }}
              >
                <Clock size={32} color="#16A34A" />
              </div>

              <h1 style={{ fontSize: '22px', fontWeight: 800, color: 'var(--ep-text-primary)', marginBottom: '8px' }}>
                Booking Confirmed!
              </h1>

              <p style={{ fontSize: '15px', fontWeight: 600, color: '#15803D', marginBottom: '12px' }}>
                Preparing your tickets…
              </p>

              <p style={{ fontSize: '14px', color: 'var(--ep-text-secondary)', marginBottom: '24px', lineHeight: 1.5, maxWidth: '460px', margin: '0 auto 24px auto' }}>
                Your order is confirmed. Digital tickets and validation tokens are being generated now. They should be ready in just a few seconds.
              </p>

              <button
                type="button"
                onClick={() => loadTickets(true)}
                disabled={isRefreshing}
                className="ep-btn-primary"
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '8px',
                  padding: '10px 24px',
                  fontSize: '14px',
                  fontWeight: 600,
                  borderRadius: 'var(--ep-radius-btn)',
                  cursor: isRefreshing ? 'not-allowed' : 'pointer',
                  border: 'none',
                }}
              >
                <RotateCcw size={14} className={isRefreshing ? 'ep-spin' : ''} />
                <span>{isRefreshing ? 'Checking…' : 'Refresh Tickets'}</span>
              </button>
            </div>
          )}

        {/* ==================== STATE 5: TICKETS READY ==================== */}
        {!loading && !error && bookingData && totalTickets > 0 && (
          <>
            {/* Event & Booking Header Summary */}
            <div
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card)',
                border: '1px solid var(--ep-border)',
                padding: '24px 28px',
                marginBottom: '28px',
                boxShadow: 'var(--ep-shadow-hover)',
              }}
            >
              <div
                style={{
                  display: 'flex',
                  flexWrap: 'wrap',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  gap: '12px',
                  borderBottom: '1px solid var(--ep-border)',
                  paddingBottom: '16px',
                  marginBottom: '16px',
                }}
              >
                <div>
                  <span
                    style={{
                      fontSize: '11px',
                      fontWeight: 800,
                      textTransform: 'uppercase',
                      letterSpacing: '0.05em',
                      color: 'var(--ep-primary)',
                      display: 'inline-block',
                      marginBottom: '4px',
                    }}
                  >
                    EventPulse Digital Passes
                  </span>
                  <h1
                    style={{
                      fontSize: '22px',
                      fontWeight: 800,
                      color: 'var(--ep-text-primary)',
                      margin: 0,
                      letterSpacing: '-0.01em',
                    }}
                  >
                    {eventTitle}
                  </h1>
                </div>

                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <span
                    style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '4px',
                      fontSize: '12px',
                      fontWeight: 700,
                      color: bookingData.bookingStatus === 'Cancelled' ? '#DC2626' : '#16A34A',
                      backgroundColor: bookingData.bookingStatus === 'Cancelled' ? '#FEF2F2' : '#F0FDF4',
                      border: bookingData.bookingStatus === 'Cancelled' ? '1px solid #FECACA' : '1px solid #BBF7D0',
                      padding: '4px 10px',
                      borderRadius: 'var(--ep-radius-pill)',
                    }}
                  >
                    {bookingData.bookingStatus === 'Cancelled' ? (
                      <>
                        <Ban size={13} />
                        <span>Booking Cancelled</span>
                      </>
                    ) : (
                      <>
                        <CheckCircle2 size={13} />
                        <span>Booking Confirmed</span>
                      </>
                    )}
                  </span>
                </div>
              </div>

              {/* Event Metadata row */}
              <div
                style={{
                  display: 'flex',
                  flexWrap: 'wrap',
                  gap: '20px',
                  fontSize: '13px',
                  color: 'var(--ep-text-secondary)',
                }}
              >
                {eventDate && (
                  <span style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
                    <Calendar size={14} color="var(--ep-primary)" />
                    <span>{formatDate(eventDate)}</span>
                  </span>
                )}
                {eventVenue && (
                  <span style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
                    <MapPin size={14} color="var(--ep-primary)" />
                    <span>{eventVenue}</span>
                  </span>
                )}
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
                  <TicketIcon size={14} color="var(--ep-primary)" />
                  <span>
                    {totalTickets} {totalTickets === 1 ? 'Admission Pass' : 'Admission Passes'}
                  </span>
                </span>
                {bookingData.bookingReference && (
                  <span style={{ marginLeft: 'auto', fontSize: '13px' }}>
                    Order: <strong style={{ color: 'var(--ep-text-primary)', fontFamily: 'monospace' }}>#{bookingData.bookingReference}</strong>
                  </span>
                )}
              </div>
            </div>

            {/* Individual Ticket Cards */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: '24px' }}>
              {bookingData.tickets.map((ticket, index) => {
                const isValid = ticket.status === 'Valid';
                const isCancelled = ticket.status === 'Cancelled';
                const isUsed = ticket.status === 'Used';

                return (
                  <div
                    key={ticket.ticketId || index}
                    style={{
                      backgroundColor: '#ffffff',
                      borderRadius: 'var(--ep-radius-card)',
                      border: '1px solid var(--ep-border)',
                      boxShadow: 'var(--ep-shadow-hover)',
                      overflow: 'hidden',
                      display: 'flex',
                      flexDirection: 'column',
                    }}
                  >
                    {/* Top Accent Strip */}
                    <div
                      style={{
                        height: '4px',
                        backgroundColor: isValid
                          ? 'var(--ep-primary)'
                          : isCancelled
                          ? 'var(--ep-danger)'
                          : '#94A3B8',
                      }}
                    />

                    {/* Card Body */}
                    <div
                      style={{
                        display: 'grid',
                        gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))',
                        alignItems: 'stretch',
                      }}
                    >
                      {/* Left: Ticket Information */}
                      <div
                        style={{
                          padding: '28px',
                          display: 'flex',
                          flexDirection: 'column',
                          justifyContent: 'space-between',
                          borderRight: '1px dashed var(--ep-border)',
                        }}
                      >
                        <div>
                          {/* Ticket Header & Status */}
                          <div
                            style={{
                              display: 'flex',
                              alignItems: 'center',
                              justifyContent: 'space-between',
                              marginBottom: '16px',
                            }}
                          >
                            <span
                              style={{
                                fontSize: '12px',
                                fontWeight: 700,
                                color: 'var(--ep-text-secondary)',
                                textTransform: 'uppercase',
                                letterSpacing: '0.04em',
                              }}
                            >
                              Ticket {index + 1} of {totalTickets}
                            </span>

                            {isValid && (
                              <span
                                style={{
                                  fontSize: '11px',
                                  fontWeight: 700,
                                  color: '#15803D',
                                  backgroundColor: '#F0FDF4',
                                  border: '1px solid #BBF7D0',
                                  padding: '3px 10px',
                                  borderRadius: 'var(--ep-radius-pill)',
                                  display: 'inline-flex',
                                  alignItems: 'center',
                                  gap: '4px',
                                }}
                              >
                                <CheckCircle2 size={12} />
                                <span>Valid Admission</span>
                              </span>
                            )}

                            {isUsed && (
                              <span
                                style={{
                                  fontSize: '11px',
                                  fontWeight: 700,
                                  color: '#475569',
                                  backgroundColor: '#F1F5F9',
                                  border: '1px solid #CBD5E1',
                                  padding: '3px 10px',
                                  borderRadius: 'var(--ep-radius-pill)',
                                }}
                              >
                                Checked In / Used
                              </span>
                            )}

                            {isCancelled && (
                              <span
                                style={{
                                  fontSize: '11px',
                                  fontWeight: 700,
                                  color: '#DC2626',
                                  backgroundColor: '#FEF2F2',
                                  border: '1px solid #FECACA',
                                  padding: '3px 10px',
                                  borderRadius: 'var(--ep-radius-pill)',
                                }}
                              >
                                Cancelled
                              </span>
                            )}
                          </div>

                          {/* Ticket Name / Type */}
                          <h3
                            style={{
                              fontSize: '20px',
                              fontWeight: 800,
                              color: 'var(--ep-text-primary)',
                              margin: '0 0 6px 0',
                            }}
                          >
                            {ticket.ticketName || 'General Admission'}
                          </h3>

                          <div
                            style={{
                              fontSize: '13px',
                              color: 'var(--ep-text-secondary)',
                              marginBottom: '20px',
                            }}
                          >
                            {eventTitle}
                          </div>

                          {/* Ticket Code Box */}
                          <div
                            style={{
                              backgroundColor: 'var(--ep-canvas)',
                              border: '1px solid var(--ep-border)',
                              borderRadius: '8px',
                              padding: '12px 14px',
                              display: 'flex',
                              alignItems: 'center',
                              justifyContent: 'space-between',
                              gap: '8px',
                              marginBottom: '16px',
                            }}
                          >
                            <div>
                              <div
                                style={{
                                  fontSize: '10px',
                                  fontWeight: 700,
                                  textTransform: 'uppercase',
                                  color: 'var(--ep-text-secondary)',
                                  marginBottom: '2px',
                                  letterSpacing: '0.05em',
                                }}
                              >
                                Ticket Code
                              </div>
                              <div
                                style={{
                                  fontSize: '15px',
                                  fontWeight: 800,
                                  fontFamily: 'monospace',
                                  color: 'var(--ep-text-primary)',
                                  letterSpacing: '0.04em',
                                }}
                              >
                                {ticket.ticketCode}
                              </div>
                            </div>

                            <button
                              type="button"
                              onClick={() => handleCopyCode(ticket.ticketCode)}
                              title="Copy Ticket Code"
                              style={{
                                background: '#ffffff',
                                border: '1px solid var(--ep-border)',
                                borderRadius: '6px',
                                padding: '6px 10px',
                                cursor: 'pointer',
                                display: 'inline-flex',
                                alignItems: 'center',
                                gap: '4px',
                                fontSize: '11px',
                                fontWeight: 700,
                                color: copiedCode === ticket.ticketCode ? 'var(--ep-primary)' : 'var(--ep-text-secondary)',
                              }}
                            >
                              {copiedCode === ticket.ticketCode ? (
                                <>
                                  <Check size={12} color="var(--ep-primary)" />
                                  <span>Copied</span>
                                </>
                              ) : (
                                <>
                                  <Copy size={12} />
                                  <span>Copy</span>
                                </>
                              )}
                            </button>
                          </div>
                        </div>

                        {/* Cancel Single Ticket Action */}
                        {isValid && (
                          <div style={{ marginBottom: '16px' }}>
                            <button
                              type="button"
                              id={`cancel-ticket-${ticket.ticketId || ticket.ticketCode}`}
                              onClick={() => handleOpenCancelModal(ticket)}
                              style={{
                                display: 'inline-flex',
                                alignItems: 'center',
                                gap: '6px',
                                backgroundColor: '#FEF2F2',
                                color: '#DC2626',
                                border: '1px solid #FECACA',
                                borderRadius: '6px',
                                padding: '6px 12px',
                                fontSize: '12px',
                                fontWeight: 600,
                                cursor: 'pointer',
                                transition: 'all 0.15s ease',
                              }}
                              onMouseOver={(e) => {
                                e.currentTarget.style.backgroundColor = '#FEE2E2';
                              }}
                              onMouseOut={(e) => {
                                e.currentTarget.style.backgroundColor = '#FEF2F2';
                              }}
                            >
                              <Ban size={13} />
                              <span>Cancel This Ticket</span>
                            </button>
                          </div>
                        )}

                        {/* Security notice */}
                        <div
                          style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: '6px',
                            fontSize: '11px',
                            color: 'var(--ep-text-secondary)',
                            paddingTop: '12px',
                            borderTop: '1px solid var(--ep-border)',
                          }}
                        >
                          <ShieldCheck size={14} color="var(--ep-primary)" />
                          <span>Verified by EventPulse Security · Present at entry</span>
                        </div>
                      </div>

                      {/* Right: QR Code presentation */}
                      <div
                        style={{
                          padding: '28px',
                          display: 'flex',
                          flexDirection: 'column',
                          alignItems: 'center',
                          justifyContent: 'center',
                          backgroundColor: '#fafafa',
                          textAlign: 'center',
                        }}
                      >
                        {isValid ? (
                          <>
                            <div
                              style={{
                                padding: '12px',
                                backgroundColor: '#ffffff',
                                borderRadius: '12px',
                                border: '1px solid var(--ep-border)',
                                boxShadow: '0 2px 8px rgba(0, 0, 0, 0.04)',
                                marginBottom: '12px',
                                display: 'inline-flex',
                              }}
                            >
                              <QRCodeSVG
                                value={ticket.qrPayload}
                                size={156}
                                level="M"
                                includeMargin={false}
                              />
                            </div>
                            <span
                              style={{
                                fontSize: '12px',
                                fontWeight: 700,
                                color: 'var(--ep-text-primary)',
                                marginBottom: '4px',
                              }}
                            >
                              Gate Admission QR
                            </span>
                            <span
                              style={{
                                fontSize: '11px',
                                color: 'var(--ep-text-secondary)',
                                maxWidth: '190px',
                                lineHeight: 1.4,
                              }}
                            >
                              Scan at venue entrance for authorized validation
                            </span>
                          </>
                        ) : (
                          <div
                            style={{
                              padding: '24px 16px',
                              backgroundColor: '#ffffff',
                              borderRadius: '12px',
                              border: '1px dashed var(--ep-border)',
                              maxWidth: '200px',
                            }}
                          >
                            <AlertTriangle size={32} color="#94A3B8" style={{ marginBottom: '8px' }} />
                            <div style={{ fontSize: '13px', fontWeight: 700, color: 'var(--ep-text-secondary)' }}>
                              {isCancelled ? 'Ticket Cancelled' : 'Pass Already Used'}
                            </div>
                            <div style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', marginTop: '4px' }}>
                              This QR code is no longer active for admission.
                            </div>
                          </div>
                        )}
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>

            {/* Bottom Actions */}
            <div
              style={{
                marginTop: '32px',
                textAlign: 'center',
                display: 'flex',
                justifyContent: 'center',
                gap: '16px',
              }}
            >
              <Link
                to="/"
                className="ep-btn-secondary"
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '8px',
                  padding: '12px 24px',
                  fontSize: '14px',
                  fontWeight: 600,
                  textDecoration: 'none',
                  borderRadius: 'var(--ep-radius-btn)',
                }}
              >
                <span>Browse More Events</span>
              </Link>
            </div>
          </>
        )}
      </main>

      {/* Confirmation Modal for Individual Ticket Cancellation */}
      {cancellingTicket && (
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
            zIndex: 1000,
            padding: '16px',
            backdropFilter: 'blur(3px)',
          }}
          onClick={(e) => {
            if (e.target === e.currentTarget) handleCloseCancelModal();
          }}
        >
          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: '16px',
              padding: '24px 28px',
              maxWidth: '480px',
              width: '100%',
              boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '16px' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <div
                  style={{
                    width: '36px',
                    height: '36px',
                    borderRadius: '50%',
                    backgroundColor: '#FEF2F2',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    color: '#DC2626',
                  }}
                >
                  <AlertTriangle size={20} />
                </div>
                <h3 style={{ fontSize: '18px', fontWeight: 700, color: 'var(--ep-text-primary)', margin: 0 }}>
                  Cancel Admission Ticket?
                </h3>
              </div>
              <button
                type="button"
                onClick={handleCloseCancelModal}
                disabled={isSubmittingCancel}
                style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#9CA3AF' }}
              >
                <X size={20} />
              </button>
            </div>

            <p style={{ fontSize: '14px', color: 'var(--ep-text-secondary)', lineHeight: 1.5, margin: '0 0 16px 0' }}>
              Are you sure you want to cancel ticket{' '}
              <strong style={{ fontFamily: 'monospace', color: 'var(--ep-text-primary)' }}>
                #{cancellingTicket.ticketCode}
              </strong>{' '}
              ({cancellingTicket.ticketName || 'General Admission'})?
            </p>

            <div
              style={{
                backgroundColor: '#FFFBEB',
                border: '1px solid #FDE68A',
                borderRadius: '8px',
                padding: '12px 14px',
                fontSize: '13px',
                color: '#92400E',
                marginBottom: '16px',
                lineHeight: 1.4,
              }}
            >
              <strong>Note:</strong> This action will deactivate this single pass and restore 1 ticket unit back to the event inventory. Any other tickets in this order remain valid.
            </div>

            <div style={{ marginBottom: '20px' }}>
              <label
                htmlFor="cancel-ticket-reason"
                style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}
              >
                Reason for cancellation (optional)
              </label>
              <textarea
                id="cancel-ticket-reason"
                value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)}
                placeholder="e.g. Guest cannot attend, duplicate purchase, etc."
                rows={3}
                disabled={isSubmittingCancel}
                style={{
                  width: '100%',
                  padding: '10px 12px',
                  border: '1px solid var(--ep-border)',
                  borderRadius: '8px',
                  fontSize: '13px',
                  fontFamily: 'inherit',
                  resize: 'vertical',
                  boxSizing: 'border-box',
                }}
              />
            </div>

            {cancelError && (
              <div
                style={{
                  backgroundColor: '#FEF2F2',
                  border: '1px solid #FECACA',
                  borderRadius: '8px',
                  padding: '10px 14px',
                  fontSize: '13px',
                  color: '#DC2626',
                  marginBottom: '16px',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '8px',
                }}
              >
                <AlertCircle size={16} />
                <span>{cancelError}</span>
              </div>
            )}

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
              <button
                type="button"
                onClick={handleCloseCancelModal}
                disabled={isSubmittingCancel}
                style={{
                  padding: '10px 18px',
                  fontSize: '13px',
                  fontWeight: 600,
                  borderRadius: '8px',
                  border: '1px solid var(--ep-border)',
                  backgroundColor: '#ffffff',
                  color: 'var(--ep-text-primary)',
                  cursor: isSubmittingCancel ? 'not-allowed' : 'pointer',
                }}
              >
                Keep Ticket
              </button>
              <button
                type="button"
                onClick={handleConfirmCancelTicket}
                disabled={isSubmittingCancel}
                style={{
                  padding: '10px 18px',
                  fontSize: '13px',
                  fontWeight: 600,
                  borderRadius: '8px',
                  border: 'none',
                  backgroundColor: '#DC2626',
                  color: '#ffffff',
                  cursor: isSubmittingCancel ? 'not-allowed' : 'pointer',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '6px',
                }}
              >
                {isSubmittingCancel ? (
                  <>
                    <Loader2 size={14} className="ep-spin" />
                    <span>Cancelling…</span>
                  </>
                ) : (
                  <>
                    <Ban size={14} />
                    <span>Confirm Cancellation</span>
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

export default Tickets;
