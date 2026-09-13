import React, { useState, useEffect } from 'react';
import { Ticket, AlertCircle } from 'lucide-react';
import { getPublicTicketTypes } from '../../services/ticketTypeService';
import { formatPrice } from '../../utils/currencyFormatter';

export function PublicTicketList({ eventId }) {
  const [ticketTypes, setTicketTypes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    getPublicTicketTypes(eventId)
      .then((data) => { if (!cancelled) setTicketTypes(Array.isArray(data) ? data : []); })
      .catch((err) => { if (!cancelled) setError(err.message || 'Unable to load ticket information.'); })
      .finally(() => { if (!cancelled) setLoading(false); });

    return () => { cancelled = true; };
  }, [eventId]);

  if (loading) {
    return <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)' }}>Loading ticket information…</p>;
  }

  if (error) {
    return (
      <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', color: 'var(--ep-danger)' }}>
        <AlertCircle size={14} /><span>{error}</span>
      </div>
    );
  }

  if (ticketTypes.length === 0) {
    return (
      <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: 0 }}>
        Ticket information is not available for this event yet.
      </p>
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
      {ticketTypes.map((t) => (
        <div
          key={t.id}
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '14px 16px',
            borderRadius: '12px',
            border: '1px solid var(--ep-border)',
            backgroundColor: t.isSoldOut ? '#F5F5F7' : '#ffffff',
            opacity: t.isSoldOut ? 0.7 : 1,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <Ticket size={16} color={t.isSoldOut ? 'var(--ep-text-secondary)' : 'var(--ep-primary)'} />
            <div>
              <div style={{ fontSize: '14px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                {t.name}
              </div>
              {!t.isSoldOut && (
                <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                  {t.availableQuantity} {t.availableQuantity === 1 ? 'ticket' : 'tickets'} left
                </div>
              )}
            </div>
          </div>

          <div style={{ textAlign: 'right' }}>
            <div style={{ fontSize: '15px', fontWeight: 700, color: t.isSoldOut ? 'var(--ep-text-secondary)' : 'var(--ep-primary)' }}>
              {formatPrice(t.price)}
            </div>
            {t.isSoldOut && (
              <span style={{
                display: 'inline-block',
                marginTop: '4px',
                fontSize: '10px',
                fontWeight: 700,
                letterSpacing: '0.04em',
                textTransform: 'uppercase',
                color: '#FFFFFF',
                backgroundColor: 'var(--ep-text-secondary)',
                padding: '2px 8px',
                borderRadius: 'var(--ep-radius-pill)',
              }}>
                Sold Out
              </span>
            )}
          </div>
        </div>
      ))}
    </div>
  );
}