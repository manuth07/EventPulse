import React, { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { ArrowLeft, Calendar, MapPin, Ticket, RotateCcw, CalendarX, AlertCircle } from 'lucide-react';
import { Header } from '../../components/Header/Header';
import { fetchEventById } from '../../services/eventService';
import { formatPrice } from '../../utils/currencyFormatter';

function formatDate(dateString) {
  if (!dateString) return 'Date TBA';
  try {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-GB', {
      weekday: 'short',
      day: 'numeric',
      month: 'long',
      year: 'numeric'
    });
  } catch (e) {
    return dateString;
  }
}

export function EventDetails() {
  const { id } = useParams();
  const [event, setEvent] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const loadEventDetails = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchEventById(id);
      setEvent(data);
    } catch (err) {
      console.error('Error loading event details:', err);
      setError(err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadEventDetails();
  }, [id]);

  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column', backgroundColor: 'var(--ep-canvas)' }}>
      <Header />

      <main style={{ flex: 1, paddingBottom: '64px' }}>
        <div className="container" style={{ maxWidth: '1080px', paddingLeft: '16px', paddingRight: '16px', paddingTop: '24px' }}>
          
          {/* Back Navigation */}
          <div style={{ marginBottom: '24px' }}>
            <Link
              to="/"
              className="ep-btn-secondary"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '8px',
                fontSize: '13px',
                padding: '8px 16px',
                textDecoration: 'none'
              }}
            >
              <ArrowLeft size={16} color="var(--ep-text-primary)" />
              Back to events
            </Link>
          </div>

          {/* Loading Skeleton */}
          {loading && (
            <div className="ep-card" style={{ padding: '32px', minHeight: '400px' }}>
              <div className="ep-skeleton mb-4" style={{ height: '160px', borderRadius: '12px' }} />
              <div className="ep-skeleton mb-3" style={{ height: '36px', width: '60%' }} />
              <div className="ep-skeleton mb-2" style={{ height: '20px', width: '40%' }} />
              <div className="ep-skeleton mb-4" style={{ height: '20px', width: '30%' }} />
              <div className="ep-skeleton" style={{ height: '100px', width: '100%' }} />
            </div>
          )}

          {/* 404 Not Found / Non-Public Event State (EP-118 & EP-119) */}
          {!loading && error && error.status === 404 && (
            <div style={{
              textAlign: 'center',
              padding: '64px 24px',
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              maxWidth: '560px',
              margin: '32px auto'
            }}>
              <div style={{ display: 'flex', justifyContent: 'center', marginBottom: '16px' }}>
                <CalendarX size={48} color="var(--ep-text-secondary)" />
              </div>
              <h2 className="ep-h2 mb-2">Event not available</h2>
              <p className="ep-body mb-4" style={{ maxWidth: '420px', margin: '0 auto 24px' }}>
                This event could not be found or is no longer available to public visitors.
              </p>
              <Link to="/" className="ep-btn-primary" style={{ textDecoration: 'none' }}>
                <ArrowLeft size={16} />
                Back to events
              </Link>
            </div>
          )}

          {/* Network / Server Error State */}
          {!loading && error && error.status !== 404 && (
            <div style={{
              textAlign: 'center',
              padding: '56px 24px',
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              border: '1px solid var(--ep-border)',
              maxWidth: '560px',
              margin: '32px auto'
            }}>
              <div style={{ display: 'flex', justifyContent: 'center', marginBottom: '16px' }}>
                <AlertCircle size={44} color="var(--ep-danger)" />
              </div>
              <h3 className="ep-h3 mb-2" style={{ color: 'var(--ep-danger)' }}>
                We couldn't load this event right now
              </h3>
              <p className="ep-body mb-4">
                Please check your network connection or backend services and try again.
              </p>
              <div style={{ display: 'flex', justifyContent: 'center', gap: '12px' }}>
                <button type="button" className="ep-btn-primary" onClick={loadEventDetails}>
                  <RotateCcw size={15} style={{ marginRight: '6px' }} />
                  Try Again
                </button>
                <Link to="/" className="ep-btn-secondary" style={{ textDecoration: 'none' }}>
                  Back to events
                </Link>
              </div>
            </div>
          )}

          {/* Published/Approved Event Details View (EP-113 & EP-117) */}
          {!loading && !error && event && (
            <div className="row g-4">
              {/* Left Column: Event Visual & Information */}
              <div className="col-12 col-lg-8">
                {/* Visual Header Banner */}
                <div style={{
                  height: '220px',
                  background: 'linear-gradient(135deg, #FFF0E6 0%, #FFE0CC 100%)',
                  borderRadius: 'var(--ep-radius-card)',
                  padding: '24px',
                  display: 'flex',
                  flexDirection: 'column',
                  justifyContent: 'space-between',
                  border: '1px solid var(--ep-border)',
                  marginBottom: '24px'
                }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                    <span className="ep-badge-pill ep-badge-soft-orange">
                      Upcoming Experience
                    </span>
                    <Ticket size={28} color="var(--ep-primary)" />
                  </div>

                  <div>
                    <div className="ep-caption" style={{ color: 'var(--ep-primary)', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 700 }}>
                      Event Details
                    </div>
                    <div style={{ fontSize: '20px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                      {formatDate(event.eventDate)}
                    </div>
                  </div>
                </div>

                {/* Event Main Info */}
                <div style={{
                  backgroundColor: '#ffffff',
                  borderRadius: 'var(--ep-radius-card)',
                  border: '1px solid var(--ep-border)',
                  padding: '32px',
                  boxShadow: 'var(--ep-shadow-card)'
                }}>
                  <h1 className="ep-h1 mb-4" style={{ fontSize: '32px' }}>
                    {event.title}
                  </h1>

                  {/* Metadata Row: Date & Venue */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginBottom: '32px' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '10px', color: 'var(--ep-text-primary)', fontSize: '15px' }}>
                      <Calendar size={18} color="var(--ep-primary)" style={{ flexShrink: 0 }} />
                      <span><strong>Date & Time:</strong> {formatDate(event.eventDate)}</span>
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', gap: '10px', color: 'var(--ep-text-primary)', fontSize: '15px' }}>
                      <MapPin size={18} color="var(--ep-primary)" style={{ flexShrink: 0 }} />
                      <span><strong>Venue:</strong> {event.venue || 'Location TBA'}</span>
                    </div>
                  </div>

                  {/* Divider */}
                  <hr style={{ borderColor: 'var(--ep-border)', margin: '32px 0' }} />

                  {/* Description */}
                  <div>
                    <h3 className="ep-h3 mb-3">About this Event</h3>
                    <p className="ep-body-large" style={{ color: 'var(--ep-text-primary)', lineHeight: 1.7, whiteSpace: 'pre-line' }}>
                      {event.description}
                    </p>
                  </div>
                </div>
              </div>

              {/* Right Column: Ticket Summary & Pricing Card */}
              <div className="col-12 col-lg-4">
                <div className="ep-card" style={{ padding: '24px', position: 'sticky', top: '96px' }}>
                  <h3 className="ep-h3 mb-3" style={{ fontSize: '18px' }}>
                    Ticket Information
                  </h3>

                  <div style={{
                    backgroundColor: 'var(--ep-canvas)',
                    borderRadius: '12px',
                    padding: '16px',
                    marginBottom: '20px',
                    border: '1px solid var(--ep-border)'
                  }}>
                    <div className="ep-caption" style={{ color: 'var(--ep-text-secondary)', marginBottom: '4px' }}>
                      Starting Price
                    </div>
                    <div style={{ fontSize: '28px', fontWeight: 800, color: 'var(--ep-text-primary)', fontFamily: 'var(--ep-font-heading)' }}>
                      {formatPrice(event.price)}
                    </div>
                  </div>

                  <button
                    type="button"
                    className="ep-btn-primary w-100"
                    style={{ padding: '14px', fontSize: '15px' }}
                    onClick={() => alert('Ticket booking will be available in Sprint 2.')}
                  >
                    Select Tickets
                  </button>

                  <p className="ep-caption text-center mt-3 mb-0" style={{ color: 'var(--ep-text-secondary)' }}>
                    Guaranteed entry • Instant confirmation
                  </p>
                </div>
              </div>
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
