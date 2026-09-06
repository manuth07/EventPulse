import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { Crown, Ticket } from 'lucide-react';
import { formatPrice } from '../../utils/currencyFormatter';

function parseDateParts(dateString) {
  if (!dateString) return { day: '--', month: 'TBA', time: '--:--', period: '' };
  try {
    const d = new Date(dateString);
    if (isNaN(d.getTime())) return { day: '--', month: 'TBA', time: '--:--', period: '' };
    const day = String(d.getDate()).padStart(2, '0');
    const month = d.toLocaleDateString('en-US', { month: 'short' }).toUpperCase();
    const timeFormatted = d.toLocaleTimeString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: true,
    });
    const parts = timeFormatted.split(' ');
    const time = parts[0] || '';
    const period = (parts[1] || '').toUpperCase();
    return { day, month, time, period };
  } catch (e) {
    return { day: '--', month: 'TBA', time: '--:--', period: '' };
  }
}

export function EventCard({ event }) {
  const { id, title, venue, eventDate, price, category, imageUrl, imagePath } = event;
  const [imageError, setImageError] = useState(false);
  const [isHovered, setIsHovered] = useState(false);

  const { day, month, time, period } = parseDateParts(eventDate);
  const formattedPrice = formatPrice(price);

  const posterUrl = imageUrl || imagePath;
  const hasPoster = Boolean(posterUrl) && !imageError;

  return (
    <Link
      to={`/events/${id}`}
      style={{
        textDecoration: 'none',
        display: 'block',
        height: '100%',
        color: 'inherit',
      }}
    >
      <div
        className="ep-card"
        onMouseEnter={() => setIsHovered(true)}
        onMouseLeave={() => setIsHovered(false)}
        style={{
          maxWidth: '400px',
          width: '100%',
          margin: '0 auto',
          backgroundColor: '#FFFFFF',
          borderRadius: '18px',
          overflow: 'hidden',
          display: 'flex',
          flexDirection: 'column',
          height: '100%',
          boxShadow: isHovered
            ? '0 12px 28px rgba(0, 0, 0, 0.12)'
            : '0 2px 10px rgba(0, 0, 0, 0.05)',
          transform: isHovered ? 'translateY(-4px)' : 'none',
          transition: 'transform 0.25s ease, box-shadow 0.25s ease',
          border: '1px solid #E5E7EB',
          cursor: 'pointer',
        }}
      >
        {/* Top Section: Edge-to-Edge Poster Image (Cover, No sidebars) */}
        <div style={{
          height: '290px',
          width: '100%',
          position: 'relative',
          overflow: 'hidden',
          backgroundColor: '#F3F4F6',
          flexShrink: 0,
        }}>
          {hasPoster ? (
            <img
              src={posterUrl}
              alt={title}
              onError={() => setImageError(true)}
              style={{
                width: '100%',
                height: '100%',
                objectFit: 'cover',
                objectPosition: 'top center',
                display: 'block',
                transform: isHovered ? 'scale(1.02)' : 'none',
                transition: 'transform 0.4s ease',
              }}
            />
          ) : (
            <div style={{
              height: '100%',
              width: '100%',
              background: 'linear-gradient(135deg, #FFF0E6 0%, #FFE0CC 100%)',
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center',
              gap: '12px',
              padding: '24px',
            }}>
              <div style={{
                width: '52px',
                height: '52px',
                borderRadius: '50%',
                backgroundColor: 'rgba(255, 91, 0, 0.15)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: '#FF5B00',
              }}>
                <Ticket size={26} />
              </div>
              <span style={{ color: '#86868B', fontSize: '13px', fontWeight: 500, letterSpacing: '0.02em' }}>
                EventPulse Experience
              </span>
            </div>
          )}
        </div>

        {/* Content Section Below Image */}
        <div style={{
          padding: '20px 22px 22px 22px',
          display: 'flex',
          flexDirection: 'column',
          flex: 1,
        }}>
          {/* Event Title */}
          <h3 style={{
            fontSize: '18px',
            fontWeight: 800,
            color: '#111827',
            textTransform: 'uppercase',
            letterSpacing: '0.01em',
            lineHeight: 1.3,
            margin: '0 0 6px 0',
            overflow: 'hidden',
            textOverflow: 'ellipsis',
            display: '-webkit-box',
            WebkitLineClamp: 2,
            WebkitBoxOrient: 'vertical',
            minHeight: '46px',
          }}>
            {title}
          </h3>

          {/* Venue Name */}
          <div style={{
            fontSize: '14px',
            color: '#6B7280',
            fontWeight: 500,
            marginBottom: '16px',
            overflow: 'hidden',
            textOverflow: 'ellipsis',
            whiteSpace: 'nowrap',
          }}>
            {venue || 'Location TBA'}
          </div>

          {/* Category Chip & Small Crown Icon */}
          <div style={{
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            marginBottom: '22px',
          }}>
            <span style={{
              backgroundColor: '#EEF2FF',
              color: '#6366F1',
              borderRadius: '9999px',
              padding: '5px 14px',
              fontSize: '12px',
              fontWeight: 600,
              letterSpacing: '0.01em',
            }}>
              {category || 'Indoor Musical Concert'}
            </span>
            <span style={{
              width: '26px',
              height: '26px',
              borderRadius: '50%',
              backgroundColor: '#DCFCE7',
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: '#16A34A',
              flexShrink: 0,
            }}>
              <Crown size={14} />
            </span>
          </div>

          {/* Date / Time & Price Row */}
          <div style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            marginTop: 'auto',
          }}>
            {/* Date & Time */}
            <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
              {/* Day */}
              <div style={{ textAlign: 'center', minWidth: '28px' }}>
                <div style={{
                  fontSize: '20px',
                  fontWeight: 800,
                  color: '#FF5B00',
                  lineHeight: 1.1,
                }}>
                  {day}
                </div>
                <div style={{
                  fontSize: '12px',
                  fontWeight: 800,
                  color: '#111827',
                  letterSpacing: '0.04em',
                  marginTop: '3px',
                }}>
                  {month}
                </div>
              </div>

              {/* Time */}
              <div style={{ textAlign: 'center', minWidth: '44px' }}>
                <div style={{
                  fontSize: '20px',
                  fontWeight: 800,
                  color: '#FF5B00',
                  lineHeight: 1.1,
                }}>
                  {time}
                </div>
                <div style={{
                  fontSize: '12px',
                  fontWeight: 800,
                  color: '#111827',
                  letterSpacing: '0.04em',
                  marginTop: '3px',
                }}>
                  {period}
                </div>
              </div>
            </div>

            {/* Vertical Divider */}
            <div style={{
              width: '1px',
              height: '34px',
              backgroundColor: '#E5E7EB',
              margin: '0 12px',
            }} />

            {/* Price */}
            <div style={{ textAlign: 'right', flex: 1, minWidth: 0 }}>
              <div style={{
                fontSize: '19px',
                fontWeight: 800,
                color: '#6366F1',
                lineHeight: 1.2,
                letterSpacing: '-0.01em',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
                whiteSpace: 'nowrap',
              }}>
                {formattedPrice}
              </div>
              <div style={{
                fontSize: '13px',
                color: '#111827',
                fontWeight: 500,
                marginTop: '3px',
              }}>
                onwards
              </div>
            </div>
          </div>
        </div>
      </div>
    </Link>
  );
}
