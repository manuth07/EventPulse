import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { useAuth } from '../../context/AuthContext';
import { useCart } from '../../context/CartContext';
import { fetchEventById } from '../../services/eventService';
import { getPublicTicketTypes } from '../../services/ticketTypeService';
import { formatPrice } from '../../utils/currencyFormatter';
import { ArrowLeft, Ticket, AlertCircle, ShoppingCart, Minus, Plus } from 'lucide-react';

function formatDateShort(dateString) {
  if (!dateString) return 'Date TBA';
  try {
    const d = new Date(dateString);
    const day = d.getDate();
    const month = d.toLocaleDateString('en-US', { month: 'short' }).toUpperCase();
    const time = d.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit', hour12: true });
    return `${day} ${month}  ${time}`;
  } catch (e) {
    return dateString;
  }
}

export function SelectTickets() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { isAuthenticated } = useAuth();
  const { cart, addOrUpdateItem, refreshCart } = useCart();

  const [event, setEvent] = useState(null);
  const [ticketTypes, setTicketTypes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // quantities[ticketTypeId] = desired quantity
  const [quantities, setQuantities] = useState({});
  const [sortBy, setSortBy] = useState('availability');

  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState(null);
  const [conflictModal, setConflictModal] = useState(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [eventData, ticketData] = await Promise.all([
        fetchEventById(id),
        getPublicTicketTypes(id),
      ]);
      setEvent(eventData);
      setTicketTypes(Array.isArray(ticketData) ? ticketData : []);
    } catch (err) {
      setError(err.message || 'Unable to load ticket information.');
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  // Synchronize local quantities with active server cart when visiting this event
  useEffect(() => {
    if (cart && cart.eventId === id && Array.isArray(cart.items) && cart.items.length > 0) {
      const initial = {};
      for (const item of cart.items) {
        initial[item.ticketTypeId] = item.quantity;
      }
      setQuantities(initial);
    }
  }, [cart, id]);

  const sortedTicketTypes = useMemo(() => {
    const list = [...ticketTypes];
    if (sortBy === 'availability') {
      list.sort((a, b) => b.availableQuantity - a.availableQuantity);
    } else if (sortBy === 'priceLow') {
      list.sort((a, b) => a.price - b.price);
    } else if (sortBy === 'priceHigh') {
      list.sort((a, b) => b.price - a.price);
    }
    return list;
  }, [ticketTypes, sortBy]);

  const priceRange = useMemo(() => {
    if (ticketTypes.length === 0) return { min: 0, max: 0 };
    const prices = ticketTypes.map((t) => t.price);
    return { min: Math.min(...prices), max: Math.max(...prices) };
  }, [ticketTypes]);

  const selectedItems = useMemo(() => {
    return ticketTypes
      .filter((t) => (quantities[t.id] || 0) > 0)
      .map((t) => ({ ...t, qty: quantities[t.id] }));
  }, [ticketTypes, quantities]);

  const cartTotal = selectedItems.reduce((sum, t) => sum + t.price * t.qty, 0);
  const cartCount = selectedItems.reduce((sum, t) => sum + t.qty, 0);

  const handleAdd = (ticketTypeId) => {
    setQuantities((prev) => ({ ...prev, [ticketTypeId]: 1 }));
  };

  const handleStep = (ticketTypeId, delta, max) => {
    setQuantities((prev) => {
      const current = prev[ticketTypeId] || 0;
      const next = Math.max(0, Math.min(current + delta, max));
      return { ...prev, [ticketTypeId]: next };
    });
  };

  const handleCheckout = async (clearExisting = false) => {
    if (!isAuthenticated) {
      navigate('/login', { state: { returnTo: `/events/${id}/tickets` } });
      return;
    }
    if (selectedItems.length === 0) return;

    setSubmitError(null);
    setSubmitting(true);

    try {
      // If updating, submit items with their desired final quantities
      let isFirst = true;
      for (const item of selectedItems) {
        await addOrUpdateItem(id, item.id, item.qty, isFirst && clearExisting, false);
        isFirst = false;
      }

      // Also remove any items that were previously in this cart but now have qty 0
      if (cart && cart.eventId === id && Array.isArray(cart.items)) {
        for (const previousItem of cart.items) {
          if (!quantities[previousItem.ticketTypeId] || quantities[previousItem.ticketTypeId] === 0) {
            await addOrUpdateItem(id, previousItem.ticketTypeId, 0, false, false);
          }
        }
      }

      await refreshCart();
      navigate('/cart');
    } catch (err) {
      if (err.status === 409 && err.conflictData) {
        setConflictModal({
          currentEventTitle: err.conflictData.currentEventTitle || 'another event',
          currentEventId: err.conflictData.currentEventId,
          attemptedEventId: id,
        });
      } else {
        setSubmitError(err.message || 'Failed to add tickets to cart.');
      }
    } finally {
      setSubmitting(false);
    }
  };

  const handleConfirmClearAndContinue = async () => {
    setConflictModal(null);
    await handleCheckout(true);
  };

  return (
    <div style={{ minHeight: '100vh', backgroundColor: 'var(--ep-canvas)', display: 'flex', flexDirection: 'column' }}>
      <Header />
      <main className="container" style={{ flex: 1, paddingTop: '20px', paddingBottom: '48px' }}>
        <Link
          to={`/events/${id}`}
          style={{ display: 'inline-flex', alignItems: 'center', gap: '6px', fontSize: '13px', fontWeight: 500, color: 'var(--ep-text-secondary)', textDecoration: 'none', marginBottom: '16px' }}
        >
          <ArrowLeft size={14} />
          <span>Back to event</span>
        </Link>

        {loading && <p style={{ color: 'var(--ep-text-secondary)' }}>Loading ticket options…</p>}

        {!loading && error && (
          <div style={{ padding: '20px', backgroundColor: '#FFF5F5', borderRadius: '12px', border: '1px solid #FED7D7', display: 'flex', gap: '12px' }}>
            <AlertCircle size={20} color="var(--ep-danger)" />
            <span style={{ fontSize: '14px', color: 'var(--ep-text-primary)' }}>{error}</span>
          </div>
        )}

        {!loading && !error && event && (
          <div style={{ display: 'grid', gridTemplateColumns: 'minmax(280px, 340px) 1fr', gap: '24px', alignItems: 'flex-start' }}>

            {/* ===== LEFT SIDEBAR ===== */}
            <div className="ep-card" style={{ padding: 0, position: 'sticky', top: '96px', overflow: 'hidden' }}>
              {/* Event header */}
              <div style={{ padding: '18px', display: 'flex', gap: '12px', borderBottom: '1px solid var(--ep-border)' }}>
                <div style={{ width: '52px', height: '52px', borderRadius: '10px', overflow: 'hidden', flexShrink: 0, backgroundColor: 'var(--ep-canvas)' }}>
                  {(event.imageUrl || event.coverUrl) && (
                    <img src={event.imageUrl || event.coverUrl} alt={event.title} style={{ width: '100%', height: '100%', objectFit: 'cover' }} />
                  )}
                </div>
                <div style={{ minWidth: 0 }}>
                  <div style={{ fontSize: '13px', fontWeight: 700, color: 'var(--ep-text-primary)', textTransform: 'uppercase', letterSpacing: '0.02em', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {event.title}
                  </div>
                  <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {event.venue}
                  </div>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--ep-primary)', marginTop: '2px' }}>
                    {formatDateShort(event.eventDate)}
                  </div>
                </div>
              </div>

              {/* Price range */}
              {ticketTypes.length > 0 && (
                <div style={{ padding: '16px 18px', borderBottom: '1px solid var(--ep-border)', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '10px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--ep-text-primary)', padding: '6px 10px', border: '1px solid var(--ep-border)', borderRadius: '8px' }}>
                    {formatPrice(priceRange.min)}
                  </div>
                  <div style={{ flex: 1, height: '2px', backgroundColor: 'var(--ep-primary)', borderRadius: '2px', position: 'relative' }}>
                    <div style={{ position: 'absolute', left: 0, top: '50%', transform: 'translate(-50%, -50%)', width: '10px', height: '10px', borderRadius: '50%', backgroundColor: 'var(--ep-primary)' }} />
                    <div style={{ position: 'absolute', right: 0, top: '50%', transform: 'translate(50%, -50%)', width: '10px', height: '10px', borderRadius: '50%', backgroundColor: 'var(--ep-primary)' }} />
                  </div>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--ep-text-primary)', padding: '6px 10px', border: '1px solid var(--ep-border)', borderRadius: '8px' }}>
                    {formatPrice(priceRange.max)}
                  </div>
                </div>
              )}

              {/* Selected items list */}
              <div style={{ maxHeight: '360px', overflowY: 'auto' }}>
                {selectedItems.length === 0 ? (
                  <p style={{ padding: '20px 18px', fontSize: '13px', color: 'var(--ep-text-secondary)', margin: 0 }}>
                    No tickets selected yet.
                  </p>
                ) : (
                  selectedItems.map((item) => (
                    <div key={item.id} style={{ padding: '14px 18px', borderBottom: '1px solid var(--ep-border)', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '10px' }}>
                      <div style={{ minWidth: 0 }}>
                        <div style={{ fontSize: '14px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>{item.name}</div>
                        <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>{item.qty} ticket{item.qty > 1 ? 's' : ''} selected</div>
                      </div>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexShrink: 0 }}>
                        <span style={{ fontSize: '11px', fontWeight: 700, backgroundColor: 'var(--ep-soft-accent)', color: 'var(--ep-primary)', padding: '2px 8px', borderRadius: 'var(--ep-radius-pill)' }}>
                          {item.qty}x
                        </span>
                        <span style={{ fontSize: '13px', fontWeight: 700, color: 'var(--ep-primary)' }}>
                          {formatPrice(item.price * item.qty)}
                        </span>
                      </div>
                    </div>
                  ))
                )}
              </div>

              {/* Checkout footer */}
              <div style={{
                padding: '16px 18px', backgroundColor: cartCount > 0 ? 'var(--ep-soft-accent)' : 'var(--ep-canvas)',
                display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '12px',
              }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <span style={{ fontSize: '13px', fontWeight: 700, backgroundColor: 'var(--ep-primary)', color: '#fff', padding: '2px 8px', borderRadius: 'var(--ep-radius-pill)' }}>
                    {cartCount}X
                  </span>
                  <span style={{ fontSize: '14px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                    {formatPrice(cartTotal)}
                  </span>
                </div>
                <button
                  type="button"
                  onClick={() => handleCheckout(false)}
                  disabled={cartCount === 0 || submitting}
                  className="ep-btn-primary"
                  style={{ fontSize: '13px', padding: '10px 20px', display: 'inline-flex', alignItems: 'center', gap: '6px', opacity: cartCount === 0 ? 0.5 : 1 }}
                >
                  <ShoppingCart size={14} />
                  <span>{submitting ? 'Saving…' : 'View Cart / Checkout'}</span>
                </button>
              </div>
              {submitError && (
                <div style={{ padding: '10px 18px', fontSize: '12px', color: 'var(--ep-danger)', backgroundColor: '#FFF2F2' }}>
                  {submitError}
                </div>
              )}
            </div>

            {/* ===== RIGHT: TICKET TYPE LIST ===== */}
            <div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', alignItems: 'center', gap: '8px', marginBottom: '14px' }}>
                <span style={{ fontSize: '13px', color: 'var(--ep-text-secondary)' }}>Sort by:</span>
                <select
                  value={sortBy}
                  onChange={(e) => setSortBy(e.target.value)}
                  style={{ fontSize: '13px', fontWeight: 600, padding: '6px 10px', borderRadius: '8px', border: '1px solid var(--ep-border)', backgroundColor: '#fff', color: 'var(--ep-text-primary)' }}
                >
                  <option value="availability">Availability</option>
                  <option value="priceLow">Price: Low to High</option>
                  <option value="priceHigh">Price: High to Low</option>
                </select>
              </div>

              {sortedTicketTypes.length === 0 && (
                <p style={{ color: 'var(--ep-text-secondary)', fontSize: '14px' }}>
                  Ticket information is not available for this event yet.
                </p>
              )}

              <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
                {sortedTicketTypes.map((t) => {
                  const qty = quantities[t.id] || 0;
                  return (
                    <div
                      key={t.id}
                      className="ep-card"
                      style={{
                        padding: '18px 20px',
                        display: 'flex',
                        flexDirection: 'row',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        gap: '16px',
                        flexWrap: 'nowrap',
                        textAlign: 'left',
                        opacity: t.isSoldOut ? 0.55 : 1,
                      }}
                    >
                      <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: '14px', flex: 1, minWidth: 0, textAlign: 'left' }}>
                        <div style={{
                          width: '40px', height: '40px', borderRadius: '10px', backgroundColor: 'var(--ep-soft-accent)',
                          display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0,
                        }}>
                          <Ticket size={18} color="var(--ep-primary)" />
                        </div>
                        <div style={{ minWidth: 0, textAlign: 'left' }}>
                          <div style={{ fontSize: '16px', fontWeight: 700, color: 'var(--ep-text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                            {t.name}
                          </div>
                          <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                            {t.isSoldOut ? 'Sold Out' : 'Seated'}
                          </div>
                        </div>
                      </div>

                      <div style={{ textAlign: 'left', flexShrink: 0 }}>
                        <div className="ep-caption" style={{ color: 'var(--ep-text-secondary)' }}>Unit Price</div>
                        <div style={{ fontSize: '16px', fontWeight: 700, color: 'var(--ep-text-primary)', whiteSpace: 'nowrap' }}>
                          {formatPrice(t.price)}
                        </div>
                      </div>

                      {!t.isSoldOut && (
                        qty === 0 ? (
                          <button
                            type="button"
                            onClick={() => handleAdd(t.id)}
                            className="ep-btn-primary"
                            style={{ fontSize: '13px', padding: '10px 22px', flexShrink: 0 }}
                          >
                            Add Ticket
                          </button>
                        ) : (
                          <div style={{ display: 'flex', alignItems: 'center', gap: '10px', flexShrink: 0 }}>
                            <button
                              type="button"
                              onClick={() => handleStep(t.id, -1, t.availableQuantity)}
                              style={{ width: '32px', height: '32px', borderRadius: '50%', border: '1px solid var(--ep-border)', background: '#fff', cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'var(--ep-primary)' }}
                            >
                              <Minus size={14} />
                            </button>
                            <span style={{ minWidth: '28px', textAlign: 'center', fontSize: '15px', fontWeight: 700, border: '1px solid var(--ep-border)', borderRadius: '8px', padding: '4px 8px' }}>
                              {qty}
                            </span>
                            <button
                              type="button"
                              onClick={() => handleStep(t.id, 1, t.availableQuantity)}
                              style={{ width: '32px', height: '32px', borderRadius: '50%', border: '1px solid var(--ep-border)', background: '#fff', cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'var(--ep-primary)' }}
                            >
                              <Plus size={14} />
                            </button>
                          </div>
                        )
                      )}
                    </div>
                  );
                })}
              </div>
            </div>
          </div>
        )}

        {/* ===== DIFFERENT EVENT CONFLICT MODAL ===== */}
        {conflictModal && (
          <div style={{
            position: 'fixed',
            inset: 0,
            backgroundColor: 'rgba(0, 0, 0, 0.45)',
            backdropFilter: 'blur(2px)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000,
            padding: '16px',
          }}>
            <div style={{
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card)',
              maxWidth: '480px',
              width: '100%',
              padding: '24px',
              boxShadow: 'var(--ep-shadow-hover)',
            }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '14px' }}>
                <AlertCircle size={22} color="var(--ep-primary)" />
                <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                  Start a new cart?
                </h3>
              </div>
              <p style={{ fontSize: '14px', color: 'var(--ep-text-secondary)', lineHeight: 1.5, margin: '0 0 20px 0' }}>
                Your cart currently contains tickets for <strong>{conflictModal.currentEventTitle}</strong>.
                To select tickets for <strong>{event?.title}</strong>, your current cart must be cleared.
              </p>
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setConflictModal(null)}
                  className="ep-btn-secondary"
                  style={{ fontSize: '13px', padding: '9px 16px' }}
                >
                  Keep {conflictModal.currentEventTitle} Tickets
                </button>
                <button
                  type="button"
                  onClick={handleConfirmClearAndContinue}
                  className="ep-btn-primary"
                  style={{ fontSize: '13px', padding: '9px 18px' }}
                >
                  Clear Cart & Continue
                </button>
              </div>
            </div>
          </div>
        )}
      </main>
    </div>
  );
}