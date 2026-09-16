import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { useCart } from '../../context/CartContext';
import { formatPrice } from '../../utils/currencyFormatter';
import {
  ShoppingCart,
  Calendar,
  MapPin,
  Minus,
  Plus,
  Trash2,
  AlertCircle,
  ArrowRight,
  ArrowLeft,
} from 'lucide-react';

function formatDate(dateString) {
  if (!dateString) return 'Date TBA';
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

export function Cart() {
  const navigate = useNavigate();
  const { cart, loading, error, setItemQuantity, removeItem, clearCurrentCart } = useCart();

  const [actionLoading, setActionLoading] = useState(false);
  const [actionError, setActionError] = useState(null);
  const [showClearModal, setShowClearModal] = useState(false);

  const handleUpdateQuantity = async (ticketTypeId, nextQuantity) => {
    setActionError(null);
    setActionLoading(true);
    try {
      await setItemQuantity(ticketTypeId, nextQuantity);
    } catch (err) {
      setActionError(err.message || 'Failed to update ticket quantity.');
    } finally {
      setActionLoading(false);
    }
  };

  const handleRemove = async (ticketTypeId) => {
    setActionError(null);
    setActionLoading(true);
    try {
      await removeItem(ticketTypeId);
    } catch (err) {
      setActionError(err.message || 'Failed to remove ticket item.');
    } finally {
      setActionLoading(false);
    }
  };

  const handleConfirmClear = async () => {
    setShowClearModal(false);
    setActionError(null);
    setActionLoading(true);
    try {
      await clearCurrentCart();
    } catch (err) {
      setActionError(err.message || 'Failed to clear cart.');
    } finally {
      setActionLoading(false);
    }
  };

  const hasItems = cart && Array.isArray(cart.items) && cart.items.length > 0;

  return (
    <div style={{ minHeight: '100vh', backgroundColor: 'var(--ep-canvas)', display: 'flex', flexDirection: 'column' }}>
      <Header />
      <main className="container" style={{ flex: 1, padding: '32px 16px 64px', maxWidth: '720px' }}>
        
        {/* Navigation Breadcrumb */}
        {hasItems && cart.eventId && (
          <Link
            to={`/events/${cart.eventId}/tickets`}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '6px',
              fontSize: '13px',
              fontWeight: 500,
              color: 'var(--ep-text-secondary)',
              textDecoration: 'none',
              marginBottom: '20px',
            }}
          >
            <ArrowLeft size={14} />
            <span>Add more tickets</span>
          </Link>
        )}

        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '24px' }}>
          <h1 className="ep-h2" style={{ margin: 0 }}>Your Cart</h1>
          {hasItems && (
            <button
              type="button"
              onClick={() => setShowClearModal(true)}
              disabled={actionLoading}
              style={{
                background: 'none',
                border: 'none',
                fontSize: '13px',
                fontWeight: 500,
                color: 'var(--ep-text-secondary)',
                cursor: 'pointer',
                padding: '6px 8px',
                borderRadius: '6px',
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
              }}
              onMouseEnter={(e) => (e.currentTarget.style.color = 'var(--ep-danger)')}
              onMouseLeave={(e) => (e.currentTarget.style.color = 'var(--ep-text-secondary)')}
            >
              <Trash2 size={13} />
              <span>Clear Cart</span>
            </button>
          )}
        </div>

        {loading && <p style={{ color: 'var(--ep-text-secondary)' }}>Loading cart…</p>}
        {error && (
          <div style={{ padding: '16px', backgroundColor: '#FFF5F5', borderRadius: '12px', border: '1px solid #FED7D7', display: 'flex', gap: '10px', marginBottom: '20px' }}>
            <AlertCircle size={18} color="var(--ep-danger)" style={{ marginTop: '2px', flexShrink: 0 }} />
            <span style={{ fontSize: '13px', color: 'var(--ep-danger)' }}>{error}</span>
          </div>
        )}
        {actionError && (
          <div style={{ padding: '14px', backgroundColor: '#FFF2F2', borderRadius: '10px', border: '1px solid var(--ep-danger)', display: 'flex', gap: '10px', marginBottom: '20px' }}>
            <AlertCircle size={18} color="var(--ep-danger)" style={{ marginTop: '2px', flexShrink: 0 }} />
            <span style={{ fontSize: '13px', color: 'var(--ep-danger)' }}>{actionError}</span>
          </div>
        )}

        {!loading && !hasItems && (
          <div className="ep-card" style={{ padding: '48px 24px', textAlign: 'center' }}>
            <div style={{ width: '56px', height: '56px', borderRadius: '50%', backgroundColor: 'var(--ep-canvas)', display: 'flex', alignItems: 'center', justifyContent: 'center', margin: '0 auto 16px' }}>
              <ShoppingCart size={24} color="var(--ep-text-secondary)" />
            </div>
            <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '0 0 6px 0', color: 'var(--ep-text-primary)' }}>
              Your cart is empty
            </h3>
            <p style={{ fontSize: '14px', color: 'var(--ep-text-secondary)', margin: '0 0 24px 0' }}>
              Browse events and choose tickets to get started.
            </p>
            <Link
              to="/"
              className="ep-btn-primary"
              style={{ display: 'inline-flex', alignItems: 'center', gap: '8px', padding: '10px 24px', fontSize: '14px', textDecoration: 'none' }}
            >
              <span>Browse Events</span>
              <ArrowRight size={15} />
            </Link>
          </div>
        )}

        {!loading && hasItems && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
            {/* Event Summary Card */}
            <div className="ep-card" style={{ padding: '20px', display: 'flex', gap: '16px', alignItems: 'center' }}>
              {cart.eventImageUrl && (
                <div style={{ width: '64px', height: '64px', borderRadius: '12px', overflow: 'hidden', flexShrink: 0, backgroundColor: 'var(--ep-canvas)' }}>
                  <img src={cart.eventImageUrl} alt={cart.eventTitle} style={{ width: '100%', height: '100%', objectFit: 'cover' }} />
                </div>
              )}
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontSize: '11px', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.04em', color: 'var(--ep-primary)', marginBottom: '4px' }}>
                  Event Ticket Selection
                </div>
                <h3 style={{ fontSize: '17px', fontWeight: 700, color: 'var(--ep-text-primary)', margin: '0 0 6px 0', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  {cart.eventTitle || 'Selected Event'}
                </h3>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '14px', fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                  {cart.eventVenue && (
                    <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                      <MapPin size={13} />
                      <span>{cart.eventVenue}</span>
                    </span>
                  )}
                  {cart.eventDate && (
                    <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                      <Calendar size={13} />
                      <span>{formatDate(cart.eventDate)}</span>
                    </span>
                  )}
                </div>
              </div>
            </div>

            {/* Ticket Items List */}
            <div className="ep-card" style={{ padding: 0, overflow: 'hidden' }}>
              <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--ep-border)', fontSize: '13px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                Tickets in Cart
              </div>
              <div>
                {cart.items.map((item, index) => (
                  <div
                    key={item.id}
                    style={{
                      padding: '20px',
                      borderBottom: index < cart.items.length - 1 ? '1px solid var(--ep-border)' : 'none',
                      display: 'flex',
                      flexWrap: 'wrap',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      gap: '16px',
                    }}
                  >
                    {/* Item Information */}
                    <div style={{ minWidth: '180px', flex: 1 }}>
                      <div style={{ fontSize: '16px', fontWeight: 700, color: 'var(--ep-text-primary)', marginBottom: '4px' }}>
                        {item.ticketTypeName}
                      </div>
                      <div style={{ fontSize: '13px', color: 'var(--ep-text-secondary)' }}>
                        {formatPrice(item.unitPrice)} each
                      </div>
                    </div>

                    {/* Quantity Controls */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                      <button
                        type="button"
                        onClick={() => handleUpdateQuantity(item.ticketTypeId, item.quantity - 1)}
                        disabled={actionLoading}
                        aria-label="Decrease quantity"
                        style={{
                          width: '32px',
                          height: '32px',
                          borderRadius: '8px',
                          border: '1px solid var(--ep-border)',
                          backgroundColor: '#ffffff',
                          cursor: 'pointer',
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          color: 'var(--ep-text-primary)',
                        }}
                      >
                        <Minus size={14} />
                      </button>
                      <span
                        style={{
                          minWidth: '32px',
                          textAlign: 'center',
                          fontSize: '15px',
                          fontWeight: 700,
                          color: 'var(--ep-text-primary)',
                        }}
                      >
                        {item.quantity}
                      </span>
                      <button
                        type="button"
                        onClick={() => handleUpdateQuantity(item.ticketTypeId, item.quantity + 1)}
                        disabled={actionLoading}
                        aria-label="Increase quantity"
                        style={{
                          width: '32px',
                          height: '32px',
                          borderRadius: '8px',
                          border: '1px solid var(--ep-border)',
                          backgroundColor: '#ffffff',
                          cursor: 'pointer',
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          color: 'var(--ep-text-primary)',
                        }}
                      >
                        <Plus size={14} />
                      </button>
                    </div>

                    {/* Line Total & Remove Action */}
                    <div style={{ textAlign: 'right', minWidth: '120px' }}>
                      <div style={{ fontSize: '16px', fontWeight: 700, color: 'var(--ep-primary)', marginBottom: '4px' }}>
                        {formatPrice(item.lineTotal)}
                      </div>
                      <button
                        type="button"
                        onClick={() => handleRemove(item.ticketTypeId)}
                        disabled={actionLoading}
                        style={{
                          background: 'none',
                          border: 'none',
                          fontSize: '12px',
                          color: 'var(--ep-text-secondary)',
                          cursor: 'pointer',
                          padding: 0,
                          textDecoration: 'underline',
                        }}
                        onMouseEnter={(e) => (e.currentTarget.style.color = 'var(--ep-danger)')}
                        onMouseLeave={(e) => (e.currentTarget.style.color = 'var(--ep-text-secondary)')}
                      >
                        Remove
                      </button>
                    </div>
                  </div>
                ))}
              </div>

              {/* Total & Checkout Section */}
              <div
                style={{
                  padding: '24px 20px',
                  backgroundColor: 'var(--ep-canvas)',
                  borderTop: '1px solid var(--ep-border)',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: '16px',
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <div>
                    <div style={{ fontSize: '14px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                      Total ({cart.totalTicketCount} {cart.totalTicketCount === 1 ? 'ticket' : 'tickets'})
                    </div>
                    <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                      Includes all applicable taxes and fees
                    </div>
                  </div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: 'var(--ep-primary)' }}>
                    {formatPrice(cart.totalAmount)}
                  </div>
                </div>

                <button
                  type="button"
                  onClick={() => alert('Checkout session will proceed to payment gateway.')}
                  disabled={actionLoading}
                  className="ep-btn-primary"
                  style={{
                    width: '100%',
                    padding: '14px',
                    fontSize: '15px',
                    fontWeight: 700,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: '8px',
                    borderRadius: 'var(--ep-radius-btn)',
                  }}
                >
                  <span>Continue to Checkout</span>
                  <ArrowRight size={16} />
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Clear Cart Confirmation Modal */}
        {showClearModal && (
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
              maxWidth: '420px',
              width: '100%',
              padding: '24px',
              boxShadow: 'var(--ep-shadow-hover)',
            }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '12px' }}>
                <Trash2 size={20} color="var(--ep-danger)" />
                <h3 style={{ margin: 0, fontSize: '17px', fontWeight: 700, color: 'var(--ep-text-primary)' }}>
                  Clear all tickets?
                </h3>
              </div>
              <p style={{ fontSize: '14px', color: 'var(--ep-text-secondary)', lineHeight: 1.5, margin: '0 0 20px 0' }}>
                This will remove all tickets from your cart. You can always select tickets again later.
              </p>
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setShowClearModal(false)}
                  className="ep-btn-secondary"
                  style={{ fontSize: '13px', padding: '8px 16px' }}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  onClick={handleConfirmClear}
                  className="ep-btn-primary"
                  style={{
                    fontSize: '13px',
                    padding: '8px 18px',
                    backgroundColor: 'var(--ep-danger)',
                    borderColor: 'var(--ep-danger)',
                  }}
                >
                  Clear Cart
                </button>
              </div>
            </div>
          </div>
        )}
      </main>
    </div>
  );
}