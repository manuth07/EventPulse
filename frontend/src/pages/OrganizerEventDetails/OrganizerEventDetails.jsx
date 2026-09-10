import React, { useState, useEffect, useCallback } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { useAuth } from '../../context/AuthContext';
import { getMySubmission, resubmitEvent } from '../../services/eventService';
import { formatPrice } from '../../utils/currencyFormatter';
import { TicketTypesPanel } from '../../components/TicketTypes/TicketTypesPanel';

import {
  Calendar,
  MapPin,
  ArrowLeft,
  AlertCircle,
  Clock,
  MessageSquare,
  CheckCircle2,
  UploadCloud,
  X,
  Edit3,
  Image as ImageIcon,
} from 'lucide-react';

function formatEventDateTime(dateString) {
  if (!dateString) return 'Date TBA';
  try {
    const date = new Date(dateString);
    const datePart = date.toLocaleDateString('en-GB', {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
    });
    const timePart = date.toLocaleTimeString('en-US', {
      hour: 'numeric',
      minute: '2-digit',
      hour12: true,
    });
    return `${datePart} at ${timePart}`;
  } catch (e) {
    return dateString;
  }
}

function formatDate(dateString) {
  if (!dateString) return '';
  try {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    });
  } catch (e) {
    return dateString;
  }
}

function toLocalDatetimeInput(dateString) {
  if (!dateString) return '';
  try {
    const d = new Date(dateString);
    const pad = (n) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  } catch (e) {
    return '';
  }
}

const STATUS_CONFIG = {
  Pending: {
    label: 'PENDING REVIEW',
    badgeBg: '#FFF0E6',
    badgeColor: 'var(--ep-primary)',
    badgeBorder: '#FFE0CC',
    subtext: 'Awaiting administrator review',
  },
  Approved: {
    label: 'APPROVED',
    badgeBg: '#E8F5E9',
    badgeColor: '#2E7D32',
    badgeBorder: '#C8E6C9',
    subtext: 'This event has been approved by an administrator.',
  },
  Rejected: {
    label: 'REJECTED',
    badgeBg: '#FFEBEE',
    badgeColor: '#C62828',
    badgeBorder: '#FFCDD2',
    subtext: 'This event was rejected during review.',
  },
  Published: {
    label: 'PUBLISHED',
    badgeBg: '#E3F2FD',
    badgeColor: '#1565C0',
    badgeBorder: '#BBDEFB',
    subtext: 'This event is published and visible to the public.',
  },
};

const EVENT_CATEGORIES = [
  'Musical Concert',
  'Conference',
  'Workshop',
  'Festival',
  'Sports',
  'Theatre / Performance',
  'Other',
];

const VENUE_TYPES = ['Indoor', 'Outdoor'];

export function OrganizerEventDetails() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { accessToken } = useAuth();

  const [event, setEvent] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Edit / Resubmission state
  const [isEditing, setIsEditing] = useState(false);
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [category, setCategory] = useState(EVENT_CATEGORIES[0]);
  const [venueType, setVenueType] = useState('Indoor');
  const [venue, setVenue] = useState('');
  const [eventDate, setEventDate] = useState('');
  const [price, setPrice] = useState('0');
  const [imageFile, setImageFile] = useState(null);
  const [imagePreview, setImagePreview] = useState(null);
  const [coverFile, setCoverFile] = useState(null);
  const [coverPreview, setCoverPreview] = useState(null);

  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState(null);
  const [resubmitSuccess, setResubmitSuccess] = useState(false);

  useEffect(() => {
    return () => {
      if (imagePreview && imagePreview.startsWith('blob:')) {
        URL.revokeObjectURL(imagePreview);
      }
      if (coverPreview && coverPreview.startsWith('blob:')) {
        URL.revokeObjectURL(coverPreview);
      }
    };
  }, [imagePreview, coverPreview]);

  const loadSubmission = useCallback(async () => {
    setLoading(true);
    setError(null);

    const token = accessToken || sessionStorage.getItem('ep_access_token');
    if (!token) {
      setError('You are not authenticated. Please log in again.');
      setLoading(false);
      return;
    }

    try {
      const data = await getMySubmission(id, token);
      setEvent(data);
      setTitle(data.title || '');
      setDescription(data.description || '');
      setCategory(data.category || EVENT_CATEGORIES[0]);
      setVenueType(data.venueType || 'Indoor');
      setVenue(data.venue || '');
      setEventDate(toLocalDatetimeInput(data.eventDate));
      setPrice(String(data.price != null ? Number(data.price) : '0'));
      setImagePreview(data.imageUrl || null);
      setCoverPreview(data.coverUrl || null);
    } catch (err) {
      setError(err.message || 'Unable to load event details.');
    } finally {
      setLoading(false);
    }
  }, [id, accessToken]);

  useEffect(() => {
    loadSubmission();
  }, [loadSubmission]);

  const handleStartEdit = () => {
    if (!event || event.status !== 'Rejected') return;
    setTitle(event.title || '');
    setDescription(event.description || '');
    setCategory(event.category || EVENT_CATEGORIES[0]);
    setVenueType(event.venueType || 'Indoor');
    setVenue(event.venue || '');
    setEventDate(toLocalDatetimeInput(event.eventDate));
    setPrice(String(event.price != null ? Number(event.price) : '0'));
    setImageFile(null);
    setImagePreview(event.imageUrl || null);
    setCoverFile(null);
    setCoverPreview(event.coverUrl || null);
    setFormError(null);
    setIsEditing(true);
  };

  const handleCancelEdit = () => {
    setIsEditing(false);
    setFormError(null);
    setCategory(event?.category || EVENT_CATEGORIES[0]);
    setVenueType(event?.venueType || 'Indoor');
    setImageFile(null);
    setImagePreview(event?.imageUrl || null);
    setCoverFile(null);
    setCoverPreview(event?.coverUrl || null);
  };

  const handleImageChange = (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      setFormError('Please select a valid image file (JPEG, PNG, or WebP) for the poster.');
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      setFormError('Poster image must be less than 5 MB.');
      return;
    }

    setFormError(null);
    setImageFile(file);
    setImagePreview(URL.createObjectURL(file));
  };

  const handleCoverChange = (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      setFormError('Please select a valid image file (JPEG, PNG, or WebP) for the cover banner.');
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      setFormError('Cover image must be less than 5 MB.');
      return;
    }

    setFormError(null);
    setCoverFile(file);
    setCoverPreview(URL.createObjectURL(file));
  };

  const handleResubmit = async (e) => {
    e.preventDefault();
    setFormError(null);

    if (!title.trim()) {
      setFormError('Title is required.');
      return;
    }
    if (!description.trim()) {
      setFormError('Description is required.');
      return;
    }
    if (!category) {
      setFormError('Category is required.');
      return;
    }
    if (!venueType) {
      setFormError('Venue type is required.');
      return;
    }
    if (!venue.trim()) {
      setFormError('Venue is required.');
      return;
    }
    if (!eventDate) {
      setFormError('Event date and time is required.');
      return;
    }
    if (new Date(eventDate) <= new Date()) {
      setFormError('Event date must be in the future.');
      return;
    }

    const parsedPrice = Number(price);
    if (isNaN(parsedPrice) || parsedPrice < 0 || !Number.isInteger(parsedPrice)) {
      setFormError('Ticket price must be entered in whole LKR.');
      return;
    }

    setSubmitting(true);

    try {
      const formData = new FormData();
      formData.append('title', title.trim());
      formData.append('description', description.trim());
      formData.append('category', category);
      formData.append('venueType', venueType);
      formData.append('venue', venue.trim());
      formData.append('eventDate', new Date(eventDate).toISOString());
      formData.append('price', String(parsedPrice));

      if (imageFile) {
        formData.append('image', imageFile);
      }

      if (coverFile) {
        formData.append('coverImage', coverFile);
      }

      const token = accessToken || sessionStorage.getItem('ep_access_token');
      const updated = await resubmitEvent(id, formData, token);

      setEvent(updated);
      setIsEditing(false);
      setResubmitSuccess(true);
      setTimeout(() => {
        setResubmitSuccess(false);
      }, 5000);
    } catch (err) {
      setFormError(err.message || 'Failed to resubmit event. Please try again.');
    } finally {
      setSubmitting(false);
    }
  };

  const statusConfig = event ? (STATUS_CONFIG[event.status] || STATUS_CONFIG.Pending) : STATUS_CONFIG.Pending;

  return (
    <div style={{
      minHeight: '100vh',
      backgroundColor: 'var(--ep-canvas)',
      display: 'flex',
      flexDirection: 'column',
    }}>
      <Header />
      <main className="container" style={{
        flex: 1,
        paddingTop: '32px',
        paddingBottom: '64px',
        maxWidth: '820px',
      }}>
        {/* Back Link */}
        <Link
          to="/organizer"
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
          <ArrowLeft size={15} />
          <span>Back to Organizer Dashboard</span>
        </Link>

        {/* Loading State */}
        {loading && (
          <div style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            padding: '40px',
            display: 'flex',
            flexDirection: 'column',
            gap: '16px',
          }}>
            <div className="ep-skeleton" style={{ height: '32px', width: '60%' }} />
            <div className="ep-skeleton" style={{ height: '20px', width: '40%' }} />
            <div className="ep-skeleton" style={{ height: '240px', width: '100%', marginTop: '12px' }} />
          </div>
        )}

        {/* Error State */}
        {!loading && error && (
          <div style={{
            padding: '24px',
            backgroundColor: '#FFF5F5',
            borderRadius: 'var(--ep-radius-container, 12px)',
            border: '1px solid #FED7D7',
            display: 'flex',
            alignItems: 'flex-start',
            gap: '16px',
          }}>
            <AlertCircle size={22} color="var(--ep-danger)" style={{ marginTop: '2px', flexShrink: 0 }} />
            <div style={{ flex: 1 }}>
              <h4 style={{ fontSize: '15px', fontWeight: 600, color: 'var(--ep-text-primary)', margin: '0 0 6px 0' }}>
                Unable to load submission details
              </h4>
              <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: '0 0 16px 0', lineHeight: 1.5 }}>
                {error}
              </p>
              <button
                type="button"
                onClick={loadSubmission}
                className="ep-btn-secondary"
                style={{ fontSize: '13px', padding: '6px 16px' }}
              >
                Retry
              </button>
            </div>
          </div>
        )}

        {/* Resubmission Success Banner */}
        {!loading && resubmitSuccess && (
          <div style={{
            padding: '16px 20px',
            backgroundColor: '#F0FDF4',
            border: '1px solid #BBF7D0',
            borderRadius: 'var(--ep-radius-container, 12px)',
            marginBottom: '20px',
            display: 'flex',
            alignItems: 'center',
            gap: '12px',
          }}>
            <CheckCircle2 size={20} color="#16A34A" style={{ flexShrink: 0 }} />
            <div>
              <p style={{ fontSize: '14px', fontWeight: 600, color: '#166534', margin: 0 }}>
                Event resubmitted successfully!
              </p>
              <p style={{ fontSize: '13px', color: '#15803D', margin: 0 }}>
                Your event status has returned to <strong>Pending Review</strong> and is queued for Administrator review.
              </p>
            </div>
          </div>
        )}

        {/* Main Event Card */}
        {!loading && !error && event && (
          <div style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            overflow: 'hidden',
            boxShadow: 'var(--ep-shadow-card)',
          }}>
            {/* Header / Status Banner */}
            <div style={{
              padding: '24px 32px',
              borderBottom: '1px solid var(--ep-border)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              flexWrap: 'wrap',
              gap: '16px',
            }}>
              <div>
                <span style={{
                  backgroundColor: statusConfig.badgeBg,
                  color: statusConfig.badgeColor,
                  border: `1px solid ${statusConfig.badgeBorder}`,
                  borderRadius: 'var(--ep-radius-pill)',
                  padding: '4px 14px',
                  fontSize: '12px',
                  fontWeight: 700,
                  letterSpacing: '0.04em',
                  textTransform: 'uppercase',
                  display: 'inline-block',
                }}>
                  {statusConfig.label}
                </span>
                <p style={{
                  fontSize: '13px',
                  color: 'var(--ep-text-secondary)',
                  marginTop: '6px',
                  margin: 0,
                }}>
                  {statusConfig.subtext}
                </p>
              </div>

              {/* Action Button: Edit & Resubmit only for Rejected events */}
              {event.status === 'Rejected' && !isEditing && (
                <button
                  type="button"
                  onClick={handleStartEdit}
                  className="ep-btn-primary"
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '8px',
                    padding: '8px 18px',
                    fontSize: '13px',
                    fontWeight: 600,
                    borderRadius: 'var(--ep-radius-btn)',
                  }}
                >
                  <Edit3 size={15} />
                  <span>Edit & Resubmit</span>
                </button>
              )}
            </div>

            {/* REJECTED FEEDBACK PANEL */}
            {event.status === 'Rejected' && !isEditing && (
              <div style={{
                margin: '24px 32px 0 32px',
                padding: '20px 24px',
                backgroundColor: '#FFF5F5',
                border: '1px solid #FED7D7',
                borderRadius: 'var(--ep-radius-container, 12px)',
              }}>
                <div style={{ display: 'flex', alignItems: 'flex-start', gap: '12px' }}>
                  <MessageSquare size={20} color="#C62828" style={{ marginTop: '2px', flexShrink: 0 }} />
                  <div style={{ flex: 1 }}>
                    <h3 style={{
                      fontSize: '14px',
                      fontWeight: 700,
                      color: '#991B1B',
                      margin: '0 0 6px 0',
                      letterSpacing: '0.01em',
                      textTransform: 'uppercase',
                    }}>
                      Administrator Feedback
                    </h3>
                    <p style={{
                      fontSize: '14px',
                      color: '#4B5563',
                      lineHeight: 1.5,
                      margin: '0 0 8px 0',
                    }}>
                      {event.reviewComment || 'No feedback was provided by the reviewer.'}
                    </p>
                    {event.reviewedAt && (
                      <p style={{
                        fontSize: '12px',
                        color: '#6B7280',
                        margin: 0,
                      }}>
                        Reviewed on {formatDate(event.reviewedAt)}
                      </p>
                    )}
                  </div>
                </div>
              </div>
            )}

            {/* PREVIOUS FEEDBACK ON PENDING RESUBMITTED EVENT */}
            {event.status === 'Pending' && event.reviewComment && (
              <div style={{
                margin: '24px 32px 0 32px',
                padding: '16px 20px',
                backgroundColor: '#F8FAFC',
                border: '1px solid #E2E8F0',
                borderRadius: 'var(--ep-radius-container, 12px)',
              }}>
                <div style={{ display: 'flex', alignItems: 'flex-start', gap: '12px' }}>
                  <Clock size={18} color="#64748B" style={{ marginTop: '2px', flexShrink: 0 }} />
                  <div>
                    <h4 style={{
                      fontSize: '13px',
                      fontWeight: 600,
                      color: '#475569',
                      margin: '0 0 4px 0',
                    }}>
                      Previous Administrator Feedback
                    </h4>
                    <p style={{
                      fontSize: '13px',
                      color: '#64748B',
                      lineHeight: 1.5,
                      margin: 0,
                    }}>
                      "{event.reviewComment}"
                    </p>
                  </div>
                </div>
              </div>
            )}

            {/* EDIT & RESUBMIT FORM */}
            {isEditing ? (
              <div style={{ padding: '32px' }}>
                <h2 style={{
                  fontSize: '18px',
                  fontWeight: 700,
                  color: 'var(--ep-text-primary)',
                  margin: '0 0 4px 0',
                }}>
                  Edit & Resubmit Event
                </h2>
                <p style={{
                  fontSize: '13px',
                  color: 'var(--ep-text-secondary)',
                  margin: '0 0 24px 0',
                }}>
                  Correct the feedback items and submit for re-review. The status will return to Pending.
                </p>

                <form onSubmit={handleResubmit} style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
                  {formError && (
                    <div style={{
                      padding: '12px 16px',
                      backgroundColor: '#FFF2F2',
                      border: '1px solid var(--ep-danger)',
                      borderRadius: 'var(--ep-radius-container, 8px)',
                      fontSize: '13px',
                      color: 'var(--ep-danger)',
                    }}>
                      {formError}
                    </div>
                  )}

                  <div>
                    <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}>
                      Event Title *
                    </label>
                    <input
                      type="text"
                      required
                      value={title}
                      onChange={(e) => setTitle(e.target.value)}
                      style={{
                        width: '100%',
                        padding: '10px 14px',
                        fontSize: '14px',
                        borderRadius: 'var(--ep-radius-btn)',
                        border: '1px solid var(--ep-border)',
                        boxSizing: 'border-box',
                      }}
                    />
                  </div>

                  <div>
                    <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}>
                      Description *
                    </label>
                    <textarea
                      required
                      rows={4}
                      value={description}
                      onChange={(e) => setDescription(e.target.value)}
                      style={{
                        width: '100%',
                        padding: '10px 14px',
                        fontSize: '14px',
                        borderRadius: 'var(--ep-radius-btn)',
                        border: '1px solid var(--ep-border)',
                        boxSizing: 'border-box',
                        fontFamily: 'inherit',
                      }}
                    />
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
                    <div>
                      <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}>
                        Event Category *
                      </label>
                      <select
                        required
                        value={category}
                        onChange={(e) => setCategory(e.target.value)}
                        style={{
                          width: '100%',
                          padding: '10px 14px',
                          fontSize: '14px',
                          borderRadius: 'var(--ep-radius-btn)',
                          border: '1px solid var(--ep-border)',
                          backgroundColor: '#ffffff',
                          boxSizing: 'border-box',
                          cursor: 'pointer',
                        }}
                      >
                        {EVENT_CATEGORIES.map((cat) => (
                          <option key={cat} value={cat}>
                            {cat}
                          </option>
                        ))}
                      </select>
                    </div>

                    <div>
                      <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}>
                        Venue Type *
                      </label>
                      <select
                        required
                        value={venueType}
                        onChange={(e) => setVenueType(e.target.value)}
                        style={{
                          width: '100%',
                          padding: '10px 14px',
                          fontSize: '14px',
                          borderRadius: 'var(--ep-radius-btn)',
                          border: '1px solid var(--ep-border)',
                          backgroundColor: '#ffffff',
                          boxSizing: 'border-box',
                          cursor: 'pointer',
                        }}
                      >
                        {VENUE_TYPES.map((vt) => (
                          <option key={vt} value={vt}>
                            {vt}
                          </option>
                        ))}
                      </select>
                      <p style={{ fontSize: '11px', color: 'var(--ep-text-secondary)', margin: '4px 0 0 0' }}>
                        Select whether the event venue is primarily indoors or outdoors.
                      </p>
                    </div>
                  </div>

                  <div>
                    <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}>
                      Venue Location *
                    </label>
                    <div style={{ position: 'relative' }}>
                      <MapPin size={16} color="var(--ep-text-secondary)" style={{ position: 'absolute', left: '12px', top: '12px' }} />
                      <input
                        type="text"
                        required
                        value={venue}
                        onChange={(e) => setVenue(e.target.value)}
                        style={{
                          width: '100%',
                          padding: '10px 14px 10px 36px',
                          fontSize: '14px',
                          borderRadius: 'var(--ep-radius-btn)',
                          border: '1px solid var(--ep-border)',
                          boxSizing: 'border-box',
                        }}
                      />
                    </div>
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
                    <div>
                      <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}>
                        Event Date & Time *
                      </label>
                      <div style={{ position: 'relative' }}>
                        <Calendar size={16} color="var(--ep-text-secondary)" style={{ position: 'absolute', left: '12px', top: '12px' }} />
                        <input
                          type="datetime-local"
                          required
                          value={eventDate}
                          onChange={(e) => setEventDate(e.target.value)}
                          style={{
                            width: '100%',
                            padding: '10px 14px 10px 36px',
                            fontSize: '14px',
                            borderRadius: 'var(--ep-radius-btn)',
                            border: '1px solid var(--ep-border)',
                            boxSizing: 'border-box',
                          }}
                        />
                      </div>
                    </div>

                    <div>
                      <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}>
                        Ticket Price (LKR) *
                      </label>
                      <div style={{ position: 'relative' }}>
                        <span style={{
                          position: 'absolute',
                          left: '12px',
                          top: '11px',
                          fontSize: '12px',
                          fontWeight: 700,
                          color: 'var(--ep-text-secondary)',
                          userSelect: 'none',
                        }}>
                          LKR
                        </span>
                        <input
                          type="number"
                          min="0"
                          step="1"
                          placeholder="0"
                          required
                          value={price}
                          onChange={(e) => setPrice(e.target.value)}
                          style={{
                            width: '100%',
                            padding: '10px 14px 10px 48px',
                            fontSize: '14px',
                            borderRadius: 'var(--ep-radius-btn)',
                            border: '1px solid var(--ep-border)',
                            boxSizing: 'border-box',
                          }}
                        />
                      </div>
                    </div>
                  </div>

                  {/* Poster Section */}
                  <div>
                    <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}>
                      Event Poster
                    </label>
                    <p style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', margin: '0 0 10px 0' }}>
                      Leave unchanged to keep the existing poster, or select a new file to replace it.
                    </p>

                    {imagePreview ? (
                      <div style={{ position: 'relative', width: '100%', maxWidth: '240px' }}>
                        <img
                          src={imagePreview}
                          alt="Poster preview"
                          style={{
                            width: '100%',
                            borderRadius: 'var(--ep-radius-container, 8px)',
                            display: 'block',
                            border: '1px solid var(--ep-border)',
                            maxHeight: '280px',
                            objectFit: 'cover',
                          }}
                        />
                        <button
                          type="button"
                          onClick={() => {
                            setImageFile(null);
                            setImagePreview(null);
                          }}
                          title="Remove poster"
                          style={{
                            position: 'absolute',
                            top: '8px',
                            right: '8px',
                            background: 'rgba(255, 255, 255, 0.9)',
                            border: 'none',
                            borderRadius: '50%',
                            width: '28px',
                            height: '28px',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            cursor: 'pointer',
                            boxShadow: '0 2px 4px rgba(0,0,0,0.15)',
                          }}
                        >
                          <X size={15} color="var(--ep-text-primary)" />
                        </button>
                      </div>
                    ) : (
                      <label style={{
                        display: 'flex',
                        flexDirection: 'column',
                        alignItems: 'center',
                        justifyContent: 'center',
                        padding: '24px',
                        border: '2px dashed var(--ep-border)',
                        borderRadius: 'var(--ep-radius-container, 8px)',
                        backgroundColor: 'var(--ep-canvas)',
                        cursor: 'pointer',
                      }}>
                        <UploadCloud size={28} color="var(--ep-text-secondary)" style={{ marginBottom: '8px' }} />
                        <span style={{ fontSize: '13px', fontWeight: 500, color: 'var(--ep-text-primary)', marginBottom: '4px' }}>
                          Select replacement poster
                        </span>
                        <span style={{ fontSize: '11px', color: 'var(--ep-text-secondary)' }}>
                          JPEG, PNG or WebP • Max 5 MB
                        </span>
                        <input
                          type="file"
                          accept="image/jpeg,image/png,image/webp"
                          onChange={handleImageChange}
                          style={{ display: 'none' }}
                        />
                      </label>
                    )}
                  </div>

                  {/* Cover Banner Section */}
                  <div>
                    <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}>
                      Event Cover / Banner
                    </label>
                    <p style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', margin: '0 0 10px 0' }}>
                      Leave unchanged to keep the existing cover banner, or select a new file to replace it.
                    </p>

                    {coverPreview ? (
                      <div style={{ position: 'relative', width: '100%' }}>
                        <img
                          src={coverPreview}
                          alt="Cover banner preview"
                          style={{
                            width: '100%',
                            borderRadius: 'var(--ep-radius-container, 8px)',
                            display: 'block',
                            border: '1px solid var(--ep-border)',
                            maxHeight: '180px',
                            objectFit: 'cover',
                          }}
                        />
                        <button
                          type="button"
                          onClick={() => {
                            setCoverFile(null);
                            setCoverPreview(null);
                          }}
                          title="Remove cover banner"
                          style={{
                            position: 'absolute',
                            top: '8px',
                            right: '8px',
                            background: 'rgba(255, 255, 255, 0.9)',
                            border: 'none',
                            borderRadius: '50%',
                            width: '28px',
                            height: '28px',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            cursor: 'pointer',
                            boxShadow: '0 2px 4px rgba(0,0,0,0.15)',
                          }}
                        >
                          <X size={15} color="var(--ep-text-primary)" />
                        </button>
                      </div>
                    ) : (
                      <label style={{
                        display: 'flex',
                        flexDirection: 'column',
                        alignItems: 'center',
                        justifyContent: 'center',
                        padding: '24px',
                        border: '2px dashed var(--ep-border)',
                        borderRadius: 'var(--ep-radius-container, 8px)',
                        backgroundColor: 'var(--ep-canvas)',
                        cursor: 'pointer',
                      }}>
                        <UploadCloud size={28} color="var(--ep-text-secondary)" style={{ marginBottom: '8px' }} />
                        <span style={{ fontSize: '13px', fontWeight: 500, color: 'var(--ep-text-primary)', marginBottom: '4px' }}>
                          Select replacement cover banner
                        </span>
                        <span style={{ fontSize: '11px', color: 'var(--ep-text-secondary)' }}>
                          Wide banner ratio (~1920×720) • JPEG, PNG or WebP • Max 5 MB
                        </span>
                        <input
                          type="file"
                          accept="image/jpeg,image/png,image/webp"
                          onChange={handleCoverChange}
                          style={{ display: 'none' }}
                        />
                      </label>
                    )}
                  </div>

                  {/* Form Action Buttons */}
                  <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px', marginTop: '12px' }}>
                    <button
                      type="button"
                      disabled={submitting}
                      onClick={handleCancelEdit}
                      className="ep-btn-secondary"
                      style={{ fontSize: '13px', padding: '8px 18px' }}
                    >
                      Cancel
                    </button>
                    <button
                      type="submit"
                      disabled={submitting}
                      className="ep-btn-primary"
                      style={{
                        fontSize: '13px',
                        padding: '8px 22px',
                        borderRadius: 'var(--ep-radius-btn)',
                        cursor: submitting ? 'not-allowed' : 'pointer',
                        opacity: submitting ? 0.7 : 1,
                      }}
                    >
                      {submitting ? 'Resubmitting...' : 'Resubmit for Review'}
                    </button>
                  </div>
                </form>
              </div>
            ) : (
              /* READ-ONLY EVENT DETAILS VIEW */
              <div style={{ padding: '32px' }}>
                {/* Dedicated Event Cover Banner (if present) */}
                {event.coverUrl && (
                  <div style={{
                    width: '100%',
                    height: '180px',
                    borderRadius: 'var(--ep-radius-card)',
                    overflow: 'hidden',
                    border: '1px solid var(--ep-border)',
                    backgroundColor: 'var(--ep-canvas)',
                    marginBottom: '24px',
                  }}>
                    <img
                      src={event.coverUrl}
                      alt={`${event.title} cover banner`}
                      style={{
                        width: '100%',
                        height: '100%',
                        objectFit: 'cover',
                        display: 'block',
                      }}
                    />
                  </div>
                )}

                <div style={{
                  display: 'flex',
                  gap: '32px',
                  alignItems: 'flex-start',
                  flexWrap: 'wrap',
                }}>
                  {/* Poster on Left */}
                  <div style={{
                    width: '240px',
                    height: '300px',
                    borderRadius: 'var(--ep-radius-card)',
                    overflow: 'hidden',
                    backgroundColor: 'var(--ep-soft-accent)',
                    flexShrink: 0,
                    border: '1px solid var(--ep-border)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}>
                    {event.imageUrl ? (
                      <img
                        src={event.imageUrl}
                        alt={event.title}
                        style={{
                          width: '100%',
                          height: '100%',
                          objectFit: 'cover',
                          display: 'block',
                        }}
                      />
                    ) : (
                      <div style={{ textAlign: 'center', padding: '16px', color: 'var(--ep-primary)' }}>
                        <ImageIcon size={36} />
                        <p style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', marginTop: '8px' }}>
                          No poster uploaded
                        </p>
                      </div>
                    )}
                  </div>

                  {/* Information on Right */}
                  <div style={{ flex: 1, minWidth: '280px' }}>
                    {(event.category || event.venueType) && (
                      <div style={{ marginBottom: '10px' }}>
                        <span style={{
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '6px',
                          backgroundColor: '#FFF0E6',
                          color: '#1D1D1F',
                          border: '1px solid rgba(255, 91, 0, 0.18)',
                          borderRadius: 'var(--ep-radius-pill, 9999px)',
                          padding: '4px 12px',
                          fontSize: '12px',
                          fontWeight: 600,
                          letterSpacing: '-0.01em',
                        }}>
                          {event.venueType && event.category
                            ? `${event.venueType} • ${event.category}`
                            : (event.category || event.venueType)}
                        </span>
                      </div>
                    )}

                    <h1 style={{
                      fontSize: '24px',
                      fontWeight: 700,
                      color: 'var(--ep-text-primary)',
                      margin: '0 0 16px 0',
                      lineHeight: 1.25,
                    }}>
                      {event.title}
                    </h1>

                    <div style={{
                      display: 'flex',
                      flexDirection: 'column',
                      gap: '10px',
                      marginBottom: '20px',
                    }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '14px', color: 'var(--ep-text-secondary)' }}>
                        <MapPin size={16} color="var(--ep-text-secondary)" style={{ flexShrink: 0 }} />
                        <span>{event.venue || 'Venue TBA'}</span>
                      </div>

                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '14px', color: 'var(--ep-text-secondary)' }}>
                        <Calendar size={16} color="var(--ep-text-secondary)" style={{ flexShrink: 0 }} />
                        <span>{formatEventDateTime(event.eventDate)}</span>
                      </div>

                      <div style={{
                        fontSize: '18px',
                        fontWeight: 700,
                        color: 'var(--ep-primary)',
                        marginTop: '4px',
                      }}>
                        {formatPrice(event.price)}
                      </div>
                    </div>

                    <div style={{
                      padding: '16px',
                      backgroundColor: 'var(--ep-canvas)',
                      borderRadius: 'var(--ep-radius-container, 8px)',
                      border: '1px solid var(--ep-border)',
                      marginBottom: '20px',
                    }}>
                      <h4 style={{ fontSize: '12px', fontWeight: 700, color: 'var(--ep-text-secondary)', margin: '0 0 6px 0', textTransform: 'uppercase' }}>
                        Description
                      </h4>
                      <p style={{ fontSize: '13px', color: 'var(--ep-text-primary)', lineHeight: 1.6, margin: 0, whiteSpace: 'pre-line' }}>
                        {event.description}
                      </p>
                    </div>

                    {/* Metadata summary */}
                    <div style={{
                      fontSize: '12px',
                      color: 'var(--ep-text-secondary)',
                      display: 'flex',
                      gap: '16px',
                      flexWrap: 'wrap',
                    }}>
                      {event.createdAt && (
                        <span>Submitted on {formatDate(event.createdAt)}</span>
                      )}
                      {event.reviewedAt && (
                        <span>• Reviewed on {formatDate(event.reviewedAt)}</span>
                      )}
                    </div>

                    {/* Ticket Types — only for events eligible to sell tickets */}
                    {(event.status === 'Approved' || event.status === 'Published') && (
                      <TicketTypesPanel eventId={event.id} accessToken={accessToken} />
                    )}
                  </div>
                </div>
              </div>
            )}
          </div>
        )}
      </main>
    </div>
  );
}
