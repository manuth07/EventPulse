import React, { useState, useEffect } from 'react';
import { Ticket, AlertCircle } from 'lucide-react';
import { getPublicTicketTypes } from '../../services/ticketTypeService';
import { formatPrice } from '../../utils/currencyFormatter';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { addToCart } from '../../services/cartService';

export function PublicTicketList({ eventId }) {
  const [ticketTypes, setTicketTypes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const navigate = useNavigate();
  const { isAuthenticated, accessToken } = useAuth();
  const [quantities, setQuantities] = useState({});
  const [addingId, setAddingId] = useState(null);
  const [addError, setAddError] = useState(null);
  const [addSuccessId, setAddSuccessId] = useState(null);

  const handleQuantityChange = (ticketTypeId, value, max) => {
    const num = Math.max(1, Math.min(Number(value) || 1, max));
    setQuantities((prev) => ({ ...prev, [ticketTypeId]: num }));
  };

  const handleAddToCart = async (t) => {
    if (!isAuthenticated) {
      navigate('/login', { state: { returnTo: window.location.pathname } });
      return;
    }
    setAddError(null);
    setAddingId(t.id);
    try {
      const qty = quantities[t.id] || 1;
      await addToCart(eventId, t.id, qty, accessToken);
      setAddSuccessId(t.id);
      setTimeout(() => setAddSuccessId(null), 2000);
    } catch (err) {
      setAddError(err.message || 'Failed to add to cart.');
    } finally {
      setAddingId(null);
    }
  };

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
      {addError && (
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', color: 'var(--ep-danger)', padding: '8px 12px', backgroundColor: '#FFF2F2', border: '1px solid var(--ep-danger)', borderRadius: '8px' }}>
          <AlertCircle size={14} /><span>{addError}</span>
        </div>
      )}
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
            {!t.isSoldOut && (
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginTop: '8px', justifyContent: 'flex-end' }}>
                <input
                  type="number"
                  min="1"
                  max={t.availableQuantity}
                  value={quantities[t.id] || 1}
                  onChange={(e) => handleQuantityChange(t.id, e.target.value, t.availableQuantity)}
                  style={{ width: '56px', padding: '6px 8px', fontSize: '13px', borderRadius: '8px', border: '1px solid var(--ep-border)' }}
                />
                <button
                  type="button"
                  onClick={() => handleAddToCart(t)}
                  disabled={addingId === t.id}
                  className="ep-btn-secondary"
                  style={{ fontSize: '12px', padding: '6px 12px' }}
                >
                  {addingId === t.id ? 'Adding…' : addSuccessId === t.id ? 'Added ✓' : 'Add to Cart'}
                </button>
              </div>
            )}
          </div>
        </div>
      ))}
    </div>
  );
}