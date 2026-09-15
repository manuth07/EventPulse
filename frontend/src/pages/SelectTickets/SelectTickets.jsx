import React, { useState, useEffect, useCallback } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { useAuth } from '../../context/AuthContext';
import { fetchEventById } from '../../services/eventService';
import { getPublicTicketTypes } from '../../services/ticketTypeService';
import { addToCart } from '../../services/cartService';
import { formatPrice } from '../../utils/currencyFormatter';
import { ArrowLeft, Ticket, AlertCircle, ShoppingCart } from 'lucide-react';

function formatDate(dateString) {
  if (!dateString) return 'Date TBA';
  try {
    const d = new Date(dateString);
    const datePart = d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short' });
    const timePart = d.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit', hour12: true });
    return `${datePart} • ${timePart}`;
  } catch (e) {
    return dateString;
  }
}

export function SelectTickets() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { isAuthenticated, accessToken } = useAuth();

  const [event, setEvent] = useState(null);
  const [ticketTypes, setTicketTypes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const [quantities, setQuantities] = useState({});
  const [addingId, setAddingId] = useState(null);
  const [addError, setAddError] = useState(null);

  const getToken = () => accessToken || sessionStorage.getItem('ep_access_token');

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

  useEffect(() => { load(); }, [load]);

  const handleQuantityChange = (ticketTypeId, delta, max) => {
    setQuantities((prev) => {
      const current = prev[ticketTypeId] || 1;
      const next = Math.max(1, Math.min(current + delta, max));
      return { ...prev, [ticketTypeId]: next };
    });
  };

  const handleAddToCart = async (t) => {
    if (!isAuthenticated) {
      navigate('/login', { state: { returnTo: `/events/${id}/tickets` } });
      return;
    }
    setAddError(null);
    setAddingId(t.id);
    try {
      const qty = quantities[t.id] || 1;
      await addToCart(id, t.id, qty, getToken());
      navigate('/cart');
    } catch (err) {
      setAddError(err.message || 'Failed to add to cart.');
    } finally {
      setAddingId(null);
    }
  };

  return (
    <div style={{ minHeight: '100vh', backgroundColor: 'var(--ep-canvas)', display: 'flex', flexDirection: 'column' }}>
      <Header />
      <main className="container" style={{ flex: 1, paddingTop: '24px', paddingBottom: '64px' }}>
        <Link
          to={`/events/${id}`}
          style={{ display: 'inline-flex', alignItems: 'center', gap: '6px', fontSize: '13px', fontWeight: 500, color: 'var(--ep-text-secondary)', textDecoration: 'none', marginBottom: '20px' }}
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
          <div className="row g-4">
            {/* Left: Event summary card */}
            <div className="col-12 col-lg-4">
              <div className="ep-card" style={{ padding: '20px', position: 'sticky', top: '96px' }}>
                {(event.imageUrl || event.coverUrl) && (
                  <div style={{ width: '100%', height: '120px', borderRadius: '12px', overflow: 'hidden', marginBottom: '14px', backgroundColor: 'var(--ep-canvas)' }}>
                    <img
                      src={event.imageUrl || event.coverUrl}
                      alt={event.title}
                      style={{ width: '100%', height: '100%', objectFit: 'cover' }}
                    />
                  </div>
                )}
                <h2 style={{ fontSize: '17px', fontWeight: 700, color: 'var(--ep-text-primary)', margin: '0 0 6px 0' }}>
                  {event.title}
                </h2>
                <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: '0 0 4px 0' }}>
                  {event.venue}
                </p>
                <p style={{ fontSize: '13px', fontWeight: 600, color: 'var(--ep-primary)', margin: 0 }}>
                  {formatDate(event.eventDate)}
                </p>
              </div>
            </div>

            {/* Right: Ticket type list */}
            <div className="col-12 col-lg-8">
              {addError && (
                <div style={{ marginBottom: '16px', padding: '12px 16px', backgroundColor: '#FFF2F2', border: '1px solid var(--ep-danger)', borderRadius: '10px', fontSize: '13px', color: 'var(--ep-danger)', display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <AlertCircle size={15} /><span>{addError}</span>
                </div>
              )}

              {ticketTypes.length === 0 && (
                <p style={{ color: 'var(--ep-text-secondary)', fontSize: '14px' }}>
                  Ticket information is not available for this event yet.
                </p>
              )}

              <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
                {ticketTypes.map((t) => (
                  <div
                    key={t.id}
                    className="ep-card"
                    style={{
                      padding: '20px',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      gap: '16px',
                      flexWrap: 'wrap',
                      opacity: t.isSoldOut ? 0.6 : 1,
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
                      <div style={{
                        width: '44px', height: '44px', borderRadius: '10px', backgroundColor: 'var(--ep-soft-accent)',
                        display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0,
                      }}>
                        <Ticket size={20} color="var(--ep-primary)" />
                      </div>
                      <div>
                        <div style={{ fontSize: '16px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                          {t.name}
                        </div>
                        <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                          {t.isSoldOut ? 'Sold Out' : `${t.availableQuantity} left`}
                        </div>
                      </div>
                    </div>

                    <div style={{ textAlign: 'right' }}>
                      <div className="ep-caption" style={{ color: 'var(--ep-text-secondary)' }}>Unit Price</div>
                      <div style={{ fontSize: '16px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                        {formatPrice(t.price)}
                      </div>
                    </div>

                    {!t.isSoldOut && (
                      <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                        <div style={{ display: 'flex', alignItems: 'center', border: '1px solid var(--ep-border)', borderRadius: '8px' }}>
                          <button
                            type="button"
                            onClick={() => handleQuantityChange(t.id, -1, t.availableQuantity)}
                            style={{ width: '30px', height: '30px', background: 'none', border: 'none', cursor: 'pointer', fontSize: '16px', color: 'var(--ep-text-primary)' }}
                          >−</button>
                          <span style={{ width: '28px', textAlign: 'center', fontSize: '14px', fontWeight: 600 }}>
                            {quantities[t.id] || 1}
                          </span>
                          <button
                            type="button"
                            onClick={() => handleQuantityChange(t.id, 1, t.availableQuantity)}
                            style={{ width: '30px', height: '30px', background: 'none', border: 'none', cursor: 'pointer', fontSize: '16px', color: 'var(--ep-text-primary)' }}
                          >+</button>
                        </div>
                        <button
                          type="button"
                          onClick={() => handleAddToCart(t)}
                          disabled={addingId === t.id}
                          className="ep-btn-primary"
                          style={{ fontSize: '13px', padding: '8px 18px', display: 'inline-flex', alignItems: 'center', gap: '6px' }}
                        >
                          <ShoppingCart size={14} />
                          <span>{addingId === t.id ? 'Adding…' : 'Add Ticket'}</span>
                        </button>
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </div>
          </div>
        )}
      </main>
    </div>
  );
}