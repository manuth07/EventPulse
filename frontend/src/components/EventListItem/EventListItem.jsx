import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { Calendar, MapPin, Ticket } from 'lucide-react';
import { formatPrice } from '../../utils/currencyFormatter';
import { normalizeCategory } from '../../data/eventConstants';
import { getEventTicketInfo } from '../../utils/ticketHelper';

function formatEventDateTime(dateString) {
  if (!dateString) return 'Date & Time TBA';
  try {
    const d = new Date(dateString);
    if (isNaN(d.getTime())) return 'Date & Time TBA';
    const weekday = d.toLocaleDateString('en-US', { weekday: 'short' });
    const day = d.getDate();
    const month = d.toLocaleDateString('en-US', { month: 'short' });
    const year = d.getFullYear();
    const timeFormatted = d.toLocaleTimeString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: true,
    });
    return `${weekday}, ${day} ${month} ${year} • ${timeFormatted}`;
  } catch (e) {
    return 'Date & Time TBA';
  }
}

export function EventListItem({ event }) {
  const { id, title, venue, eventDate, price, category, venueType, imageUrl, imagePath } = event;
  const [imageError, setImageError] = useState(false);

  const posterUrl = imageUrl || imagePath;
  const hasPoster = Boolean(posterUrl) && !imageError;
  const formattedPrice = formatPrice(price);
  const formattedDateTime = formatEventDateTime(eventDate);
  const displayCategory = normalizeCategory(category) || category;
  const ticketInfo = getEventTicketInfo(event);

  return (
    <div className="bg-white border border-slate-200/90 rounded-xl p-3 sm:p-4 shadow-sm hover:shadow-md hover:border-orange-300 transition-all flex flex-col sm:flex-row items-stretch sm:items-center gap-3 sm:gap-6 justify-between group">
      {/* Top Part on Mobile / Left + Center on Desktop */}
      <div className="flex items-center gap-3 sm:gap-4 flex-1 min-w-0">
        {/* Left: Thumbnail (Fixed Compact Size) */}
        <Link
          to={`/events/${id}`}
          className="w-24 h-24 sm:w-28 sm:h-28 rounded-lg overflow-hidden flex-shrink-0 bg-slate-100 border border-slate-200 block text-decoration-none group-hover:border-orange-200 transition-colors"
          style={{ width: undefined }}
        >
          {hasPoster ? (
            <img
              src={posterUrl}
              alt={title}
              onError={() => setImageError(true)}
              className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
            />
          ) : (
            <div className="w-full h-full flex flex-col items-center justify-center gap-1 bg-gradient-to-br from-orange-50 to-orange-100 text-orange-600">
              <Ticket size={22} />
              <span className="text-[10px] font-semibold text-slate-500">EventPulse</span>
            </div>
          )}
        </Link>

        {/* Center: Event Details */}
        <div className="flex-1 min-w-0">
          {/* Top Pill: Category & Venue Badges */}
          <div className="flex items-center flex-wrap gap-1.5 mb-1">
            {displayCategory && (
              <span className="text-[11px] font-semibold text-orange-600 bg-orange-50 px-2 py-0.5 rounded-full inline-block mr-2">
                {displayCategory}
              </span>
            )}
            {venueType && (
              <span className="text-[11px] font-medium text-slate-600 bg-slate-100 px-2 py-0.5 rounded-full inline-block">
                {venueType}
              </span>
            )}
          </div>

          {/* Title */}
          <Link
            to={`/events/${id}`}
            className="text-base sm:text-lg font-bold text-slate-900 truncate hover:text-orange-600 transition-colors block mb-1 text-decoration-none"
          >
            {title}
          </Link>

          {/* Date & Venue Line */}
          <div className="space-y-0.5">
            <div className="text-xs text-slate-600 font-medium flex items-center gap-1.5">
              <Calendar className="w-3.5 h-3.5 text-orange-600 shrink-0" />
              <span>{formattedDateTime}</span>
            </div>

            <div className="text-xs text-slate-500 truncate mt-0.5 flex items-center gap-1.5">
              <MapPin className="w-3.5 h-3.5 text-slate-400 shrink-0" />
              <span className="truncate">{venue || 'Location TBA'}</span>
            </div>
          </div>

          {/* Ticket Capacity / Inventory Status */}
          <div className="mt-1">
            {ticketInfo.isUrgent ? (
              <span className="text-[11px] font-semibold text-amber-600 flex items-center gap-1">
                <span className="text-amber-500">●</span> Tickets selling fast ({ticketInfo.count} left)
              </span>
            ) : (
              <span className="text-[11px] font-semibold text-emerald-600 flex items-center gap-1">
                <span className="text-emerald-500">●</span> Available ({ticketInfo.count} tickets)
              </span>
            )}
          </div>
        </div>
      </div>

      {/* Right (Price & CTA Button - flex-shrink-0) */}
      <div className="flex sm:flex-col items-center sm:items-end justify-between w-full sm:w-auto gap-2 pt-2.5 sm:pt-0 border-t sm:border-t-0 border-slate-100 flex-shrink-0">
        <div className="text-left sm:text-right">
          <span className="text-[10px] text-slate-400 uppercase tracking-wider block">
            Tickets from
          </span>
          <div className="text-base font-bold text-slate-900 leading-tight">
            {formattedPrice}
          </div>
        </div>

        <Link
          to={`/events/${id}`}
          className="bg-orange-600 hover:bg-orange-700 text-white font-medium text-xs px-4 py-2 rounded-lg transition-colors shadow-sm inline-flex items-center justify-center text-decoration-none whitespace-nowrap"
        >
          Get Tickets
        </Link>
      </div>
    </div>
  );
}
