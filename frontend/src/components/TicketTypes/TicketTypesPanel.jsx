import React, { useState, useEffect, useCallback } from 'react';
import { Ticket, Plus, AlertCircle, Pencil, Trash2 } from 'lucide-react';
import { getTicketTypes, createTicketType, updateTicketType, deleteTicketType } from '../../services/ticketTypeService';
import { formatPrice } from '../../utils/currencyFormatter';
import { calculateRemainingQuantity, getTotalQuantity, isTicketSoldOut } from '../../utils/ticketAvailabilityHelper';

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

  // Delete state — tracks which ticket type id is being deleted, null when none
  const [deletingId, setDeletingId] = useState(null);
  const [deleteError, setDeleteError] = useState(null);

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
    setEditCapacity(String(getTotalQuantity(t)));
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

  const handleDelete = async (ticketTypeId) => {
    if (!window.confirm('Delete this ticket type? This cannot be undone.')) return;

    setDeleteError(null);
    setDeletingId(ticketTypeId);
    try {
      await deleteTicketType(eventId, ticketTypeId, getToken());
      await load();
    } catch (err) {
      setDeleteError(err.message || 'Failed to delete ticket type.');
    } finally {
      setDeletingId(null);
    }
  };

  return (
    <div
      data-testid="ticket-types-panel"
      style={{
        marginTop: '24px',
        backgroundColor: 'var(--ep-canvas)',
        borderRadius: 'var(--ep-radius-container, 12px)',
        border: '1px solid var(--ep-border)',
        padding: '20px',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '14px' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <Ticket size={18} color="var(--ep-primary)" />
          <h4 style={{ fontSize: '14px', fontWeight: 700, color: 'var(--ep-text-primary)', margin: 0 }}>
            Ticket Types
          </h4>
        </div>
        <button
          type="button"
          data-testid="toggle-add-ticket-type-btn"
          onClick={() => setShowForm((v) => !v)}
          className="ep-btn-secondary"
          style={{ fontSize: '12px', padding: '6px 12px', display: 'inline-flex', alignItems: 'center', gap: '6px' }}
        >
          <Plus size={13} />
          <span>{showForm ? 'Cancel' : 'Add Ticket Type'}</span>
        </button>
      </div>

      {showForm && (
        <form
          data-testid="create-ticket-type-form"
          onSubmit={handleSubmit}
          style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginBottom: '18px' }}
        >
          {formError && (
            <div
              data-testid="create-ticket-type-error"
              style={{
                padding: '10px 12px',
                backgroundColor: '#FFF2F2',
                border: '1px solid var(--ep-danger)',
                borderRadius: '8px',
                fontSize: '12px',
                color: 'var(--ep-danger)'
              }}
            >
              {formError}
            </div>
          )}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: '10px' }}>
            <div style={{ minWidth: '150px', gridColumn: 'span 2' }}>
              <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: 'var(--ep-text-secondary)', marginBottom: '4px' }}>
                Ticket Name <span style={{ color: 'var(--ep-danger)' }}>*</span>
              </label>
              <input
                type="text"
                data-testid="ticket-name-input"
                placeholder="e.g. VIP, General Admission"
                value={name}
                onChange={(e) => setName(e.target.value)}
                className="ep-input"
                style={{ fontSize: '13px' }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: 'var(--ep-text-secondary)', marginBottom: '4px' }}>
                Price (LKR) <span style={{ color: 'var(--ep-danger)' }}>*</span>
              </label>
              <input
                type="number"
                data-testid="ticket-price-input"
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
                Total Quantity <span style={{ color: 'var(--ep-danger)' }}>*</span>
              </label>
              <input
                type="number"
                data-testid="ticket-capacity-input"
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
          <button
            type="submit"
            data-testid="save-ticket-type-btn"
            disabled={submitting}
            className="ep-btn-primary"
            style={{ alignSelf: 'flex-start', fontSize: '13px', padding: '8px 18px' }}
          >
            {submitting ? 'Saving…' : 'Save Ticket Type'}
          </button>
        </form>
      )}

      {loading && (
        <div data-testid="ticket-types-loading" style={{ padding: '12px 0' }}>
          <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: 0 }}>
            Loading ticket types…
          </p>
        </div>
      )}

      {!loading && error && (
        <div
          data-testid="ticket-types-error"
          style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', color: 'var(--ep-danger)' }}
        >
          <AlertCircle size={14} />
          <span>{error}</span>
        </div>
      )}

      {!loading && !error && deleteError && (
        <div
          data-testid="ticket-types-delete-error"
          style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', color: 'var(--ep-danger)', marginBottom: '10px' }}
        >
          <AlertCircle size={14} />
          <span>{deleteError}</span>
        </div>
      )}

      {!loading && !error && ticketTypes.length === 0 && (
        <div data-testid="ticket-types-empty" style={{ padding: '8px 0' }}>
          <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: 0 }}>
            No ticket types configured yet.
          </p>
        </div>
      )}

      {!loading && !error && ticketTypes.length > 0 && (
        <div data-testid="ticket-types-list" style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
          {ticketTypes.map((t) => {
            const totalQty = getTotalQuantity(t);
            const remainingQty = calculateRemainingQuantity(t);
            const soldOut = isTicketSoldOut(t);
            const soldQty = t.soldQuantity ?? t.bookedQuantity ?? (totalQty - remainingQty);

            return (
              <div
                key={t.id}
                data-testid={`ticket-type-row-${t.id}`}
                style={{
                  backgroundColor: '#ffffff',
                  border: '1px solid var(--ep-border)',
                  borderRadius: '8px',
                  padding: '12px 14px',
                  boxShadow: '0 1px 3px rgba(0,0,0,0.02)',
                }}
              >
                {editingId === t.id ? (
                  <div
                    data-testid={`ticket-type-edit-form-${t.id}`}
                    style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}
                  >
                    {editError && (
                      <div
                        data-testid="ticket-type-edit-error"
                        style={{
                          padding: '8px 10px',
                          backgroundColor: '#FFF2F2',
                          border: '1px solid var(--ep-danger)',
                          borderRadius: '6px',
                          fontSize: '12px',
                          color: 'var(--ep-danger)',
                        }}
                      >
                        {editError}
                      </div>
                    )}
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: '10px' }}>
                      <div style={{ minWidth: '150px', gridColumn: 'span 2' }}>
                        <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: 'var(--ep-text-secondary)', marginBottom: '4px' }}>
                          Ticket Name
                        </label>
                        <input
                          type="text"
                          data-testid="edit-ticket-name-input"
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
                          data-testid="edit-ticket-price-input"
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
                          Total Quantity
                        </label>
                        <input
                          type="number"
                          data-testid="edit-ticket-capacity-input"
                          min="1"
                          step="1"
                          value={editCapacity}
                          onChange={(e) => setEditCapacity(e.target.value)}
                          className="ep-input"
                          style={{ fontSize: '13px' }}
                        />
                      </div>
                    </div>
                    {soldQty > 0 && (
                      <p
                        data-testid="edit-ticket-sold-warning"
                        style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', margin: 0 }}
                      >
                        {soldQty} already sold — total quantity cannot be reduced below this.
                      </p>
                    )}
                    <div style={{ display: 'flex', gap: '8px' }}>
                      <button
                        type="button"
                        data-testid="save-edit-ticket-btn"
                        onClick={() => handleSaveEdit(t.id)}
                        disabled={editSubmitting}
                        className="ep-btn-primary"
                        style={{ fontSize: '12px', padding: '6px 14px' }}
                      >
                        {editSubmitting ? 'Saving…' : 'Save Changes'}
                      </button>
                      <button
                        type="button"
                        data-testid="cancel-edit-ticket-btn"
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
                  <div
                    style={{
                      display: 'flex',
                      flexWrap: 'wrap',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      gap: '12px',
                    }}
                  >
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
                        <span
                          data-testid={`ticket-name-${t.id}`}
                          style={{ fontSize: '14px', fontWeight: 600, color: 'var(--ep-text-primary)' }}
                        >
                          {t.name}
                        </span>
                        <span style={{ fontSize: '13px', color: 'var(--ep-text-secondary)' }}>
                          • {formatPrice(t.price)}
                        </span>
                        {soldOut && (
                          <span
                            data-testid={`ticket-sold-out-badge-${t.id}`}
                            style={{
                              display: 'inline-flex',
                              alignItems: 'center',
                              fontSize: '11px',
                              fontWeight: 700,
                              color: '#C62828',
                              backgroundColor: '#FFEBEE',
                              border: '1px solid #FFCDD2',
                              borderRadius: '4px',
                              padding: '2px 8px',
                              textTransform: 'uppercase',
                              letterSpacing: '0.5px',
                            }}
                          >
                            Sold Out
                          </span>
                        )}
                      </div>
                      <div
                        data-testid={`ticket-quantities-${t.id}`}
                        style={{ display: 'flex', alignItems: 'center', gap: '14px', fontSize: '12px' }}
                      >
                        <span style={{ color: 'var(--ep-text-secondary)' }}>
                          Total: <strong data-testid={`ticket-total-${t.id}`} style={{ color: 'var(--ep-text-primary)' }}>{totalQty}</strong>
                        </span>
                        <span style={{ color: soldOut ? '#C62828' : 'var(--ep-text-secondary)' }}>
                          Remaining: <strong data-testid={`ticket-remaining-${t.id}`} style={{ color: soldOut ? '#C62828' : 'var(--ep-text-primary)' }}>{remainingQty}</strong>
                        </span>
                      </div>
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <button
                        type="button"
                        data-testid={`edit-ticket-btn-${t.id}`}
                        onClick={() => handleStartEdit(t)}
                        title="Edit ticket type"
                        style={{
                          background: 'none',
                          border: 'none',
                          cursor: 'pointer',
                          color: 'var(--ep-primary)',
                          padding: '6px',
                          display: 'flex',
                          alignItems: 'center',
                          borderRadius: '4px',
                        }}
                      >
                        <Pencil size={15} />
                      </button>
                      <button
                        type="button"
                        data-testid={`delete-ticket-btn-${t.id}`}
                        onClick={() => handleDelete(t.id)}
                        disabled={deletingId === t.id}
                        title="Delete ticket type"
                        style={{
                          background: 'none',
                          border: 'none',
                          cursor: deletingId === t.id ? 'default' : 'pointer',
                          opacity: deletingId === t.id ? 0.5 : 1,
                          color: 'var(--ep-danger)',
                          padding: '6px',
                          display: 'flex',
                          alignItems: 'center',
                          borderRadius: '4px',
                        }}
                      >
                        <Trash2 size={15} />
                      </button>
                    </div>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}