import React from 'react';
import { Ticket, MapPin } from 'lucide-react';

function formatDate(dateString) {
  if (!dateString) return 'TBA';
  try {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-GB', {
      day: 'numeric',
      month: 'short',
      year: 'numeric'
    });
  } catch (e) {
    return dateString;
  }
}

function formatPrice(price) {
  const numPrice = Number(price);
  if (isNaN(numPrice) || numPrice === 0) {
    return 'Free';
  }
  return `LKR ${numPrice.toLocaleString('en-US')}`;
}

export function EventCard({ event }) {
  const { id, title, description, venue, eventDate, price } = event;

  const formattedDate = formatDate(eventDate);
  const formattedPrice = formatPrice(price);

  return (
    <div className="ep-card h-100">
      {/* Decorative Visual Header */}
      <div style={{
        height: '140px',
        background: 'linear-gradient(135deg, #FFF0E6 0%, #FFE0CC 100%)',
        position: 'relative',
        padding: '16px',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'space-between',
        borderBottom: '1px solid var(--ep-border)'
      }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
          <span className="ep-badge-pill ep-badge-soft-orange">
            Upcoming
          </span>
          <Ticket size={20} color="var(--ep-primary)" />
        </div>

        <div>
          <div style={{
            fontSize: '11px',
            textTransform: 'uppercase',
            fontWeight: 700,
            letterSpacing: '0.05em',
            color: 'var(--ep-primary)'
          }}>
            Date
          </div>
          <div style={{
            fontSize: '16px',
            fontWeight: 700,
            color: 'var(--ep-text-primary)'
          }}>
            {formattedDate}
          </div>
        </div>
      </div>

      {/* Body Content */}
      <div style={{ padding: '20px', display: 'flex', flexDirection: 'column', flex: 1 }}>
        <h3 className="ep-h3 mb-2" style={{
          fontSize: '18px',
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          display: '-webkit-box',
          WebkitLineClamp: 2,
          WebkitBoxOrient: 'vertical',
          minHeight: '50px'
        }}>
          {title}
        </h3>

        <div style={{
          display: 'flex',
          alignItems: 'center',
          gap: '6px',
          color: 'var(--ep-text-secondary)',
          fontSize: '13px',
          marginBottom: '12px'
        }}>
          <MapPin size={14} color="var(--ep-text-secondary)" style={{ flexShrink: 0 }} />
          <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {venue || 'Location TBA'}
          </span>
        </div>

        <p className="ep-body" style={{
          fontSize: '13px',
          color: 'var(--ep-text-secondary)',
          marginBottom: '20px',
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          display: '-webkit-box',
          WebkitLineClamp: 2,
          WebkitBoxOrient: 'vertical',
          flex: 1
        }}>
          {description}
        </p>

        {/* Footer: Price & CTA */}
        <div style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          paddingTop: '16px',
          borderTop: '1px solid var(--ep-border)',
          marginTop: 'auto'
        }}>
          <div>
            <div className="ep-caption" style={{ color: 'var(--ep-text-secondary)' }}>Price</div>
            <div style={{ fontSize: '16px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
              {formattedPrice}
            </div>
          </div>

          <a
            href={`#/events/${id}`}
            className="ep-btn-secondary"
            style={{ fontSize: '13px', padding: '6px 14px' }}
            onClick={(e) => {
              e.preventDefault();
              alert(`Event Details for "${title}" will be available in EP-29.`);
            }}
          >
            View Details →
          </a>
        </div>
      </div>
    </div>
  );
}
