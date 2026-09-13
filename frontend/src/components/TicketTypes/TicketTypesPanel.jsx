import React, { useState, useEffect, useCallback } from 'react';
import { Ticket, Plus, AlertCircle, Pencil, X } from 'lucide-react';
import { getTicketTypes, createTicketType, updateTicketType } from '../../services/ticketTypeService';
import { formatPrice } from '../../utils/currencyFormatter';

export function TicketTypesPanel({ eventId, accessToken }) {
  const [ticketTypes, setTicketTypes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const [showForm, setShowForm] = useState(false);
  const [name, setName] = useState('');
  const [price, setPrice] = useState('0');
  const [capacity, setCapacity] = useState('50');
  const [formError, setFormError] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  // Edit state — tracks which ticket type id is being edited, null when none
  const [editingId, setEditingId] = useState(null);
  const [editName, setEditName] = useState('');
  const [editPrice, setEditPrice] = useState('0');
  const [editCapacity, setEditCapacity] = useState('50');
  const [editError, setEditError] = useState(null);
  const [editSubmitting, setEditSubmitting] = useState(false);

  const getToken = () => accessToken || sessionStorage.getItem('ep_access_token');

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getTicketTypes(eventId, getToken());
      setTicketTypes(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message || 'Unable to load ticket types.');
    } finally {
      setLoading(false);
    }
  }, [eventId]);

  useEffect(() => { load(); }, [load]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setFormError(null);

    if (!name.trim()) {
      setFormError('Ticket type name is required.');
      return;
    }
    const parsedPrice = Number(price);
    if (isNaN(parsedPrice) || parsedPrice < 0 || !Number.isInteger(parsedPrice)) {
      setFormError('Price must be a whole LKR amount.');
      return;
    }
    const parsedCapacity = Number(capacity);
    if (!Number.isInteger(parsedCapacity) || parsedCapacity < 1) {
      setFormError('Capacity must be at least 1.');
      return;
    }

    setSubmitting(true);
    try {
      await createTicketType(eventId, { name: name.trim(), price: parsedPrice, capacity: parsedCapacity }, getToken());
      setName('');
      setPrice('0');
      setCapacity('50');
      setShowForm(false);
      await load();
    } catch (err) {
      setFormError(err.message || 'Failed to create ticket type.');
    } finally {
      setSubmitting(false);
    }
  };

  const handleStartEdit = (t) => {
    setEditingId(t.id);
    setEditName(t.name);
    setEditPrice(String(t.price));
    setEditCapacity(String(t.capacity));
    setEditError(null);
  };

  const handleCancelEdit = () => {
    setEditingId(null);
    setEditError(null);
  };

  const handleSaveEdit = async (ticketTypeId) => {
    setEditError(null);

    if (!editName.trim()) {
      setEditError('Ticket type name is required.');
      return;
    }
    const parsedPrice = Number(editPrice);
    if (isNaN(parsedPrice) || parsedPrice < 0 || !Number.isInteger(parsedPrice)) {
      setEditError('Price must be a whole LKR amount.');
      return;
    }
    const parsedCapacity = Number(editCapacity);
    if (!Number.isInteger(parsedCapacity) || parsedCapacity < 1) {
      setEditError('Capacity must be at least 1.');
      return;
    }

    setEditSubmitting(true);
    try {
      await updateTicketType(
        eventId,
        ticketTypeId,
        { name: editName.trim(), price: parsedPrice, capacity: parsedCapacity },
        getToken()
      );
      setEditingId(null);
      await load();
    } catch (err) {
      setEditError(err.message || 'Failed to update ticket type.');
    } finally {
      setEditSubmitting(false);
    }
  };

  return (
    <div style={{
      marginTop: '24px',
      backgroundColor: 'var(--ep-canvas)',
      borderRadius: 'var(--ep-radius-container, 12px)',
      border: '1px solid var(--ep-border)',
      padding: '20px',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '14px' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <Ticket size={18} color="var(--ep-primary)" />
          <h4 style={{ fontSize: '14px', fontWeight: 700, color: 'var(--ep-text-primary)', margin: 0 }}>
            Ticket Types
          </h4>
        </div>
        <button
          type="button"
          onClick={() => setShowForm((v) => !v)}
          className="ep-btn-secondary"
          style={{ fontSize: '12px', padding: '6px 12px', display: 'inline-flex', alignItems: 'center', gap: '6px' }}
        >
          <Plus size={13} />
          <span>{showForm ? 'Cancel' : 'Add Ticket Type'}</span>
        </button>
      </div>

      {showForm && (
        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginBottom: '18px' }}>
          {formError && (
            <div style={{ padding: '10px 12px', backgroundColor: '#FFF2F2', border: '1px solid var(--ep-danger)', borderRadius: '8px', fontSize: '12px', color: 'var(--ep-danger)' }}>
              {formError}
            </div>
          )}
          <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr 1fr', gap: '10px' }}>
            <div>
              <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: 'var(--ep-text-secondary)', marginBottom: '4px' }}>
                Ticket Name
              </label>
              <input
                type="text"
                placeholder="e.g. VIP, General Admission"
                value={name}
                onChange={(e) => setName(e.target.value)}
                className="ep-input"
                style={{ fontSize: '13px' }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: 'var(--ep-text-secondary)', marginBottom: '4px' }}>
                Price (LKR)
              </label>
              <input
                type="number"
                min="0"
                step="1"
                placeholder="0"
                value={price}
                onChange={(e) => setPrice(e.target.value)}
                className="ep-input"
                style={{ fontSize: '13px' }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: 'var(--ep-text-secondary)', marginBottom: '4px' }}>
                Capacity
              </label>
              <input
                type="number"
                min="1"
                step="1"
                placeholder="e.g. 50"
                value={capacity}
                onChange={(e) => setCapacity(e.target.value)}
                className="ep-input"
                style={{ fontSize: '13px' }}
              />
            </div>
          </div>
          <button type="submit" disabled={submitting} className="ep-btn-primary" style={{ alignSelf: 'flex-start', fontSize: '13px', padding: '8px 18px' }}>
            {submitting ? 'Saving…' : 'Save Ticket Type'}
          </button>
        </form>
      )}

      {loading && <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)' }}>Loading ticket types…</p>}

      {!loading && error && (
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', color: 'var(--ep-danger)' }}>
          <AlertCircle size={14} /><span>{error}</span>
        </div>
      )}

      {!loading && !error && ticketTypes.length === 0 && (
        <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: 0 }}>
          No ticket types configured yet.
        </p>
      )}

      {!loading && !error && ticketTypes.length > 0 && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
          {ticketTypes.map((t) => (
            <div key={t.id} style={{
              backgroundColor: '#ffffff', border: '1px solid var(--ep-border)', borderRadius: '8px', padding: '10px 14px',
            }}>
              {editingId === t.id ? (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                  {editError && (
                    <div style={{ padding: '8px 10px', backgroundColor: '#FFF2F2', border: '1px solid var(--ep-danger)', borderRadius: '6px', fontSize: '12px', color: 'var(--ep-danger)' }}>
                      {editError}
                    </div>
                  )}
                  <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr 1fr', gap: '10px' }}>
                    <div>
                      <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: 'var(--ep-text-secondary)', marginBottom: '4px' }}>
                        Ticket Name
                      </label>
                      <input
                        type="text"
                        value={editName}
                        onChange={(e) => setEditName(e.target.value)}
                        className="ep-input"
                        style={{ fontSize: '13px' }}
                      />
                    </div>
                    <div>
                      <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: 'var(--ep-text-secondary)', marginBottom: '4px' }}>
                        Price (LKR)
                      </label>
                      <input
                        type="number"
                        min="0"
                        step="1"
                        value={editPrice}
                        onChange={(e) => setEditPrice(e.target.value)}
                        className="ep-input"
                        style={{ fontSize: '13px' }}
                      />
                    </div>
                    <div>
                      <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: 'var(--ep-text-secondary)', marginBottom: '4px' }}>
                        Capacity
                      </label>
                      <input
                        type="number"
                        min="1"
                        step="1"
                        value={editCapacity}
                        onChange={(e) => setEditCapacity(e.target.value)}
                        className="ep-input"
                        style={{ fontSize: '13px' }}
                      />
                    </div>
                    {ticketTypes.find((tt) => tt.id === editingId)?.bookedQuantity > 0 && (
                    <p style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', margin: 0 }}>
                      {ticketTypes.find((tt) => tt.id === editingId).bookedQuantity} already booked — capacity cannot go below this.
                    </p>
                  )}
                  </div>
                  <div style={{ display: 'flex', gap: '8px' }}>
                    <button
                      type="button"
                      onClick={() => handleSaveEdit(t.id)}
                      disabled={editSubmitting}
                      className="ep-btn-primary"
                      style={{ fontSize: '12px', padding: '6px 14px' }}
                    >
                      {editSubmitting ? 'Saving…' : 'Save Changes'}
                    </button>
                    <button
                      type="button"
                      onClick={handleCancelEdit}
                      disabled={editSubmitting}
                      className="ep-btn-secondary"
                      style={{ fontSize: '12px', padding: '6px 14px' }}
                    >
                      Cancel
                    </button>
                  </div>
                </div>
              ) : (
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <span style={{ fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)' }}>{t.name}</span>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
                  <span style={{ fontSize: '13px', color: 'var(--ep-text-secondary)' }}>
                      {formatPrice(t.price)} • {t.availableQuantity} / {t.capacity} available
                    </span>
                    <button
                      type="button"
                      onClick={() => handleStartEdit(t)}
                      title="Edit ticket type"
                      style={{
                        background: 'none', border: 'none', cursor: 'pointer',
                        color: 'var(--ep-primary)', padding: '4px', display: 'flex', alignItems: 'center',
                      }}
                    >
                      <Pencil size={14} />
                    </button>
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}