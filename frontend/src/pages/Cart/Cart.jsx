import React, { useState, useEffect } from 'react';
import { Header } from '../../components/Header/Header';
import { useAuth } from '../../context/AuthContext';
import { getCart } from '../../services/cartService';
import { formatPrice } from '../../utils/currencyFormatter';

export function Cart() {
  const { accessToken } = useAuth();
  const [cart, setCart] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    getCart(accessToken)
      .then(setCart)
      .catch((err) => setError(err.message))
      .finally(() => setLoading(false));
  }, [accessToken]);

  return (
    <div style={{ minHeight: '100vh', backgroundColor: 'var(--ep-canvas)' }}>
      <Header />
      <main className="container" style={{ padding: '32px 16px', maxWidth: '680px' }}>
        <h1 className="ep-h2" style={{ marginBottom: '20px' }}>Your Cart</h1>

        {loading && <p style={{ color: 'var(--ep-text-secondary)' }}>Loading cart…</p>}
        {error && <p style={{ color: 'var(--ep-danger)' }}>{error}</p>}

        {!loading && !error && cart && (
          <>
            {cart.items.length === 0 ? (
              <p style={{ color: 'var(--ep-text-secondary)' }}>Your cart is empty.</p>
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                {cart.items.map((item) => (
                  <div key={item.id} style={{
                    display: 'flex', justifyContent: 'space-between', alignItems: 'center',
                    backgroundColor: '#fff', border: '1px solid var(--ep-border)', borderRadius: '12px', padding: '16px',
                  }}>
                    <div>
                      <div style={{ fontWeight: 700 }}>{item.ticketTypeName}</div>
                      <div style={{ fontSize: '13px', color: 'var(--ep-text-secondary)' }}>
                        {item.quantity} × {formatPrice(item.unitPrice)}
                      </div>
                    </div>
                    <div style={{ fontWeight: 700, color: 'var(--ep-primary)' }}>
                      {formatPrice(item.lineTotal)}
                    </div>
                  </div>
                ))}

                <div style={{
                  display: 'flex', justifyContent: 'space-between', padding: '16px',
                  borderTop: '2px solid var(--ep-border)', marginTop: '8px', fontWeight: 800, fontSize: '18px',
                }}>
                  <span>Total ({cart.totalTicketCount} tickets)</span>
                  <span style={{ color: 'var(--ep-primary)' }}>{formatPrice(cart.totalAmount)}</span>
                </div>
              </div>
            )}
          </>
        )}
      </main>
    </div>
  );
}