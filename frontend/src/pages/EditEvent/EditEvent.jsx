import React, { useState, useEffect, useCallback } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { useAuth } from '../../context/AuthContext';
import { getMySubmission, getEventUpdateRequest, submitEventUpdateRequest } from '../../services/eventService';
import { EVENT_CATEGORIES, VENUE_TYPES } from '../../data/eventConstants';
import {
  Calendar,
  MapPin,
  ArrowLeft,
  CheckCircle,
  UploadCloud,
  X,
  Clock,
  AlertCircle,
  Image as ImageIcon,
} from 'lucide-react';

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

export function EditEvent() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { accessToken } = useAuth();

  const [event, setEvent] = useState(null);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState(null);

  // Form states
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [category, setCategory] = useState(EVENT_CATEGORIES[0]);
  const [venueType, setVenueType] = useState('Indoor');
  const [venue, setVenue] = useState('');
  const [eventDate, setEventDate] = useState('');

  // Image replacement states
  const [imageFile, setImageFile] = useState(null);
  const [imagePreview, setImagePreview] = useState(null);
  const [currentImageUrl, setCurrentImageUrl] = useState(null);

  const [coverFile, setCoverFile] = useState(null);
  const [coverPreview, setCoverPreview] = useState(null);
  const [currentCoverUrl, setCurrentCoverUrl] = useState(null);

  // Submission & Pending states
  const [pendingUpdateRequest, setPendingUpdateRequest] = useState(null);
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState(null);
  const [success, setSuccess] = useState(false);

  // Cleanup object URLs to prevent memory leaks
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

  // Load event details & check for existing pending update request
  const loadEventData = useCallback(async () => {
    setLoading(true);
    setFetchError(null);

    const token = accessToken || sessionStorage.getItem('ep_access_token');
    if (!token) {
      setFetchError('You are not authenticated. Please log in again.');
      setLoading(false);
      return;
    }

    try {
      const [eventData, updateReqData] = await Promise.all([
        getMySubmission(id, token),
        getEventUpdateRequest(id, token).catch(() => null),
      ]);

      setEvent(eventData);
      setTitle(eventData.title || '');
      setDescription(eventData.description || '');
      setCategory(eventData.category || EVENT_CATEGORIES[0]);
      setVenueType(eventData.venueType || 'Indoor');
      setVenue(eventData.venue || '');
      setEventDate(toLocalDatetimeInput(eventData.eventDate));
      setCurrentImageUrl(eventData.imageUrl || null);
      setCurrentCoverUrl(eventData.coverUrl || null);

      if (updateReqData && updateReqData.status === 'Pending') {
        setPendingUpdateRequest(updateReqData);
      } else {
        setPendingUpdateRequest(null);
      }
    } catch (err) {
      if (err.status === 404) {
        setFetchError('Event not found or you do not have permission to edit it.');
      } else if (err.status === 403) {
        setFetchError('You do not have permission to edit this event.');
      } else {
        setFetchError(err.message || 'Unable to load event details.');
      }
    } finally {
      setLoading(false);
    }
  }, [id, accessToken]);

  useEffect(() => {
    loadEventData();
  }, [loadEventData]);

  const handleImageChange = (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      setFormError('Please select a valid image file (JPEG, PNG, or WebP) for the poster.');
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      setFormError('Event poster must be less than 5 MB.');
      return;
    }

    setFormError(null);
    setImageFile(file);
    setImagePreview(URL.createObjectURL(file));
  };

  const handleRemoveImageReplacement = () => {
    if (imagePreview && imagePreview.startsWith('blob:')) {
      URL.revokeObjectURL(imagePreview);
    }
    setImageFile(null);
    setImagePreview(null);
  };

  const handleCoverChange = (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      setFormError('Please select a valid image file (JPEG, PNG, or WebP) for the cover banner.');
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      setFormError('Event cover image must be less than 5 MB.');
      return;
    }

    setFormError(null);
    setCoverFile(file);
    setCoverPreview(URL.createObjectURL(file));
  };

  const handleRemoveCoverReplacement = () => {
    if (coverPreview && coverPreview.startsWith('blob:')) {
      URL.revokeObjectURL(coverPreview);
    }
    setCoverFile(null);
    setCoverPreview(null);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setFormError(null);

    if (pendingUpdateRequest && pendingUpdateRequest.status === 'Pending') {
      setFormError('This event already has an update request awaiting administrator review.');
      return;
    }

    if (!title.trim() || title.trim().length < 3 || title.trim().length > 200) {
      setFormError('Event title must be between 3 and 200 characters.');
      return;
    }

    if (!description.trim() || description.trim().length < 10 || description.trim().length > 2000) {
      setFormError('Description must be between 10 and 2000 characters.');
      return;
    }

    if (!venue.trim() || venue.trim().length < 3 || venue.trim().length > 200) {
      setFormError('Venue location must be between 3 and 200 characters.');
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

    if (!category) {
      setFormError('Please select a valid event category.');
      return;
    }

    if (!venueType) {
      setFormError('Please select a valid venue type.');
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

      if (imageFile) {
        formData.append('image', imageFile);
      }

      if (coverFile) {
        formData.append('coverImage', coverFile);
      }

      const token = accessToken || sessionStorage.getItem('ep_access_token');
      await submitEventUpdateRequest(id, formData, token);

      setSuccess(true);
      setTimeout(() => {
        navigate(`/organizer/events/${id}`);
      }, 2500);
    } catch (err) {
      if (err.status === 409) {
        setFormError('This event already has an update request awaiting review.');
      } else if (err.status === 403) {
        setFormError('You do not have permission to update this event.');
      } else {
        setFormError(err.message || 'Failed to submit update request. Please try again.');
      }
    } finally {
      setSubmitting(false);
    }
  };

  const isEligibleForEdit = event && (event.status === 'Approved' || event.status === 'Published');
  const hasPendingUpdate = pendingUpdateRequest && pendingUpdateRequest.status === 'Pending';

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
        maxWidth: '680px',
      }}>
        {/* Back Link */}
        <Link
          to={`/organizer/events/${id}`}
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: '6px',
            fontSize: '13px',
            fontWeight: 500,
            color: 'var(--ep-text-secondary)',
            textDecoration: 'none',
            marginBottom: '24px',
          }}
        >
          <ArrowLeft size={14} />
          <span>Back to Event Details</span>
        </Link>

        {/* Loading State */}
        {loading && (
          <div style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            padding: '32px',
            display: 'flex',
            flexDirection: 'column',
            gap: '16px',
          }}>
            <div className="ep-skeleton" style={{ height: '28px', width: '50%' }} />
            <div className="ep-skeleton" style={{ height: '18px', width: '70%' }} />
            <div className="ep-skeleton" style={{ height: '280px', width: '100%', marginTop: '12px' }} />
          </div>
        )}

        {/* Fetch Error State */}
        {!loading && fetchError && (
          <div style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            padding: '32px',
          }}>
            <div style={{
              padding: '20px',
              backgroundColor: '#FFF5F5',
              borderRadius: 'var(--ep-radius-container)',
              border: '1px solid #FED7D7',
              display: 'flex',
              alignItems: 'flex-start',
              gap: '12px',
            }}>
              <AlertCircle size={20} color="var(--ep-danger)" style={{ marginTop: '2px', flexShrink: 0 }} />
              <div>
                <h4 style={{ fontSize: '15px', fontWeight: 600, color: 'var(--ep-text-primary)', margin: '0 0 6px 0' }}>
                  Unable to edit event
                </h4>
                <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: '0 0 16px 0', lineHeight: 1.5 }}>
                  {fetchError}
                </p>
                <Link
                  to="/organizer"
                  className="ep-btn-secondary"
                  style={{ fontSize: '13px', padding: '6px 16px', textDecoration: 'none' }}
                >
                  Back to Organizer Dashboard
                </Link>
              </div>
            </div>
          </div>
        )}

        {/* Ineligible Status Warning */}
        {!loading && !fetchError && event && !isEligibleForEdit && (
          <div style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            padding: '32px',
          }}>
            <div style={{
              padding: '20px',
              backgroundColor: '#FFFBEB',
              borderRadius: 'var(--ep-radius-container)',
              border: '1px solid #FDE68A',
              display: 'flex',
              alignItems: 'flex-start',
              gap: '12px',
            }}>
              <AlertCircle size={20} color="#D97706" style={{ marginTop: '2px', flexShrink: 0 }} />
              <div>
                <h4 style={{ fontSize: '15px', fontWeight: 600, color: '#92400E', margin: '0 0 6px 0' }}>
                  Event Not Eligible for Update
                </h4>
                <p style={{ fontSize: '13px', color: '#B45309', margin: '0 0 16px 0', lineHeight: 1.5 }}>
                  Update requests can only be submitted for <strong>Approved</strong> or <strong>Published</strong> events. This event currently has status <strong>{event.status}</strong>.
                </p>
                <Link
                  to={`/organizer/events/${id}`}
                  className="ep-btn-secondary"
                  style={{ fontSize: '13px', padding: '6px 16px', textDecoration: 'none' }}
                >
                  Return to Event Details
                </Link>
              </div>
            </div>
          </div>
        )}

        {/* Form Card for Eligible Event */}
        {!loading && !fetchError && event && isEligibleForEdit && (
          <div style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            padding: '32px',
            boxShadow: 'var(--ep-shadow-card)',
          }}>
            <h1 style={{
              fontSize: '24px',
              fontWeight: 700,
              color: 'var(--ep-text-primary)',
              margin: '0 0 8px 0',
              letterSpacing: '-0.005em',
            }}>
              Edit Event
            </h1>
            <p style={{
              fontSize: '14px',
              color: 'var(--ep-text-secondary)',
              margin: '0 0 24px 0',
            }}>
              Propose updates to your approved event. Changes are submitted for Administrator review before taking effect.
            </p>

            {/* Step 4 / Business Workflow Notice Banner */}
            <div style={{
              padding: '14px 18px',
              backgroundColor: '#FFF0E6',
              border: '1px solid #FFE0CC',
              borderRadius: 'var(--ep-radius-container)',
              marginBottom: '24px',
              display: 'flex',
              alignItems: 'flex-start',
              gap: '12px',
            }}>
              <Clock size={18} color="var(--ep-primary)" style={{ marginTop: '2px', flexShrink: 0 }} />
              <div style={{ fontSize: '13px', color: '#7C2D12', lineHeight: 1.5 }}>
                <strong style={{ display: 'block', color: 'var(--ep-text-primary)', marginBottom: '2px' }}>
                  Review Workflow Notice
                </strong>
                Updates to an approved event do not take effect immediately. Submitting this form creates an <strong>Event Update Request</strong> awaiting administrator review. Your currently approved event remains live to the public.
              </div>
            </div>

            {/* Step 6: Pending Update Warning */}
            {hasPendingUpdate && (
              <div style={{
                padding: '16px 20px',
                backgroundColor: '#FFFBEB',
                border: '1px solid #FDE68A',
                borderRadius: 'var(--ep-radius-container)',
                marginBottom: '24px',
                display: 'flex',
                alignItems: 'flex-start',
                gap: '12px',
              }}>
                <AlertCircle size={20} color="#D97706" style={{ marginTop: '2px', flexShrink: 0 }} />
                <div style={{ flex: 1 }}>
                  <h4 style={{ fontSize: '14px', fontWeight: 600, color: '#92400E', margin: '0 0 4px 0' }}>
                    Update Pending Review
                  </h4>
                  <p style={{ fontSize: '13px', color: '#B45309', margin: '0 0 12px 0', lineHeight: 1.5 }}>
                    This event already has an update request awaiting administrator review. You cannot submit another update request until the current request has been processed.
                  </p>
                  <Link
                    to={`/organizer/events/${id}`}
                    className="ep-btn-secondary"
                    style={{ fontSize: '13px', padding: '6px 14px', textDecoration: 'none' }}
                  >
                    Return to Event Details
                  </Link>
                </div>
              </div>
            )}

            {/* Success State */}
            {success ? (
              <div style={{
                padding: '28px 24px',
                backgroundColor: '#F2F9F4',
                border: '1px solid #34C759',
                borderRadius: 'var(--ep-radius-container)',
                textAlign: 'center',
              }}>
                <CheckCircle size={36} color="#34C759" style={{ marginBottom: '12px' }} />
                <h3 style={{ fontSize: '18px', fontWeight: 700, color: 'var(--ep-text-primary)', margin: '0 0 8px 0' }}>
                  Changes Submitted for Review
                </h3>
                <p style={{ fontSize: '14px', color: 'var(--ep-text-secondary)', margin: '0 0 16px 0', lineHeight: 1.5 }}>
                  Your currently approved event remains live until an administrator reviews these changes.
                </p>
                <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: 0 }}>
                  Redirecting to event details...
                </p>
              </div>
            ) : (
              <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
                {formError && (
                  <div style={{
                    padding: '12px 16px',
                    backgroundColor: '#FFF2F2',
                    border: '1px solid var(--ep-danger)',
                    borderRadius: 'var(--ep-radius-container)',
                    fontSize: '13px',
                    color: 'var(--ep-danger)',
                  }}>
                    {formError}
                  </div>
                )}

                {/* Event Title */}
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}>
                    Event Title *
                  </label>
                  <input
                    type="text"
                    required
                    disabled={hasPendingUpdate}
                    value={title}
                    onChange={(e) => setTitle(e.target.value)}
                    placeholder="e.g. Summer Music Festival 2026"
                    style={{
                      width: '100%',
                      padding: '10px 14px',
                      fontSize: '14px',
                      borderRadius: 'var(--ep-radius-btn)',
                      border: '1px solid var(--ep-border)',
                      boxSizing: 'border-box',
                      backgroundColor: hasPendingUpdate ? 'var(--ep-canvas)' : '#ffffff',
                    }}
                  />
                </div>

                {/* Description */}
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}>
                    Description *
                  </label>
                  <textarea
                    required
                    rows={4}
                    disabled={hasPendingUpdate}
                    value={description}
                    onChange={(e) => setDescription(e.target.value)}
                    placeholder="Describe your event, highlights, and schedule..."
                    style={{
                      width: '100%',
                      padding: '10px 14px',
                      fontSize: '14px',
                      borderRadius: 'var(--ep-radius-btn)',
                      border: '1px solid var(--ep-border)',
                      boxSizing: 'border-box',
                      fontFamily: 'inherit',
                      backgroundColor: hasPendingUpdate ? 'var(--ep-canvas)' : '#ffffff',
                    }}
                  />
                </div>

                {/* Category & Venue Type */}
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
                  <div>
                    <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}>
                      Event Category *
                    </label>
                    <select
                      required
                      disabled={hasPendingUpdate}
                      value={category}
                      onChange={(e) => setCategory(e.target.value)}
                      style={{
                        width: '100%',
                        padding: '10px 14px',
                        fontSize: '14px',
                        borderRadius: 'var(--ep-radius-btn)',
                        border: '1px solid var(--ep-border)',
                        backgroundColor: hasPendingUpdate ? 'var(--ep-canvas)' : '#ffffff',
                        boxSizing: 'border-box',
                        cursor: hasPendingUpdate ? 'not-allowed' : 'pointer',
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
                      disabled={hasPendingUpdate}
                      value={venueType}
                      onChange={(e) => setVenueType(e.target.value)}
                      style={{
                        width: '100%',
                        padding: '10px 14px',
                        fontSize: '14px',
                        borderRadius: 'var(--ep-radius-btn)',
                        border: '1px solid var(--ep-border)',
                        backgroundColor: hasPendingUpdate ? 'var(--ep-canvas)' : '#ffffff',
                        boxSizing: 'border-box',
                        cursor: hasPendingUpdate ? 'not-allowed' : 'pointer',
                      }}
                    >
                      {VENUE_TYPES.map((vt) => (
                        <option key={vt} value={vt}>
                          {vt}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>

                {/* Venue Location */}
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}>
                    Venue Location *
                  </label>
                  <div style={{ position: 'relative' }}>
                    <MapPin size={16} color="var(--ep-text-secondary)" style={{ position: 'absolute', left: '12px', top: '12px' }} />
                    <input
                      type="text"
                      required
                      disabled={hasPendingUpdate}
                      value={venue}
                      onChange={(e) => setVenue(e.target.value)}
                      placeholder="e.g. Nelum Pokuna Theater, Colombo"
                      style={{
                        width: '100%',
                        padding: '10px 14px 10px 36px',
                        fontSize: '14px',
                        borderRadius: 'var(--ep-radius-btn)',
                        border: '1px solid var(--ep-border)',
                        boxSizing: 'border-box',
                        backgroundColor: hasPendingUpdate ? 'var(--ep-canvas)' : '#ffffff',
                      }}
                    />
                  </div>
                </div>

                {/* Event Date & Time */}
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}>
                    Event Date & Time *
                  </label>
                  <div style={{ position: 'relative' }}>
                    <Calendar size={16} color="var(--ep-text-secondary)" style={{ position: 'absolute', left: '12px', top: '12px' }} />
                    <input
                      type="datetime-local"
                      required
                      disabled={hasPendingUpdate}
                      value={eventDate}
                      onChange={(e) => setEventDate(e.target.value)}
                      style={{
                        width: '100%',
                        padding: '10px 14px 10px 36px',
                        fontSize: '14px',
                        borderRadius: 'var(--ep-radius-btn)',
                        border: '1px solid var(--ep-border)',
                        boxSizing: 'border-box',
                        backgroundColor: hasPendingUpdate ? 'var(--ep-canvas)' : '#ffffff',
                      }}
                    />
                  </div>
                </div>

                {/* Step 10: Explicit Separation Note for Ticket Types */}
                <div style={{
                  padding: '12px 16px',
                  backgroundColor: 'var(--ep-canvas)',
                  borderRadius: 'var(--ep-radius-container)',
                  border: '1px solid var(--ep-border)',
                  fontSize: '12px',
                  color: 'var(--ep-text-secondary)',
                  lineHeight: 1.5,
                }}>
                  <strong>Note on Tickets:</strong> Ticket pricing and capacities are managed separately in the <strong>Ticket Types</strong> panel on the event page and cannot be altered via event update requests.
                </div>

                {/* Event Poster (Optional Replacement) */}
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '4px' }}>
                    Event Poster (Optional Replacement)
                  </label>
                  <p style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', margin: '0 0 10px 0' }}>
                    The current approved poster remains active unless you choose to replace it with a new file.
                  </p>

                  {/* If new replacement file chosen */}
                  {imagePreview ? (
                    <div>
                      <div style={{
                        display: 'inline-block',
                        position: 'relative',
                        maxWidth: '220px',
                        border: '2px solid var(--ep-primary)',
                        borderRadius: 'var(--ep-radius-container)',
                        overflow: 'hidden',
                      }}>
                        <img
                          src={imagePreview}
                          alt="Replacement poster preview"
                          style={{ width: '100%', display: 'block' }}
                        />
                        <button
                          type="button"
                          onClick={handleRemoveImageReplacement}
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
                          }}
                          title="Revert to approved poster"
                        >
                          <X size={15} color="var(--ep-text-primary)" />
                        </button>
                      </div>
                      <p style={{ fontSize: '12px', color: 'var(--ep-primary)', fontWeight: 500, marginTop: '6px' }}>
                        Replacement poster selected (will replace approved poster upon admin approval).
                      </p>
                    </div>
                  ) : currentImageUrl ? (
                    <div>
                      <div style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '16px',
                        flexWrap: 'wrap',
                      }}>
                        <div style={{
                          width: '100px',
                          height: '125px',
                          borderRadius: 'var(--ep-radius-container)',
                          overflow: 'hidden',
                          border: '1px solid var(--ep-border)',
                          backgroundColor: 'var(--ep-canvas)',
                        }}>
                          <img
                            src={currentImageUrl}
                            alt="Current approved poster"
                            style={{ width: '100%', height: '100%', objectFit: 'cover' }}
                          />
                        </div>
                        <div>
                          <p style={{ fontSize: '12px', fontWeight: 600, color: 'var(--ep-text-primary)', margin: '0 0 4px 0' }}>
                            Currently Approved Poster
                          </p>
                          <label style={{
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: '6px',
                            padding: '6px 12px',
                            borderRadius: 'var(--ep-radius-btn)',
                            border: '1px solid var(--ep-border)',
                            backgroundColor: '#ffffff',
                            fontSize: '12px',
                            fontWeight: 500,
                            color: 'var(--ep-text-primary)',
                            cursor: hasPendingUpdate ? 'not-allowed' : 'pointer',
                          }}>
                            <UploadCloud size={14} />
                            <span>Upload Replacement Poster</span>
                            <input
                              type="file"
                              accept="image/jpeg,image/png,image/webp"
                              disabled={hasPendingUpdate}
                              onChange={handleImageChange}
                              style={{ display: 'none' }}
                            />
                          </label>
                        </div>
                      </div>
                    </div>
                  ) : (
                    <label style={{
                      display: 'flex',
                      flexDirection: 'column',
                      alignItems: 'center',
                      padding: '20px',
                      border: '2px dashed var(--ep-border)',
                      borderRadius: 'var(--ep-radius-container)',
                      backgroundColor: 'var(--ep-canvas)',
                      cursor: hasPendingUpdate ? 'not-allowed' : 'pointer',
                    }}>
                      <UploadCloud size={24} color="var(--ep-text-secondary)" style={{ marginBottom: '6px' }} />
                      <span style={{ fontSize: '13px', fontWeight: 500 }}>Upload Poster</span>
                      <span style={{ fontSize: '11px', color: 'var(--ep-text-secondary)' }}>JPEG, PNG, WebP • Max 5 MB</span>
                      <input
                        type="file"
                        accept="image/jpeg,image/png,image/webp"
                        disabled={hasPendingUpdate}
                        onChange={handleImageChange}
                        style={{ display: 'none' }}
                      />
                    </label>
                  )}
                </div>

                {/* Event Cover / Banner (Optional Replacement) */}
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '4px' }}>
                    Event Cover Banner (Optional Replacement)
                  </label>
                  <p style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', margin: '0 0 10px 0' }}>
                    The current approved banner remains active unless you choose to replace it with a new file.
                  </p>

                  {/* If new replacement file chosen */}
                  {coverPreview ? (
                    <div>
                      <div style={{
                        position: 'relative',
                        width: '100%',
                        border: '2px solid var(--ep-primary)',
                        borderRadius: 'var(--ep-radius-container)',
                        overflow: 'hidden',
                      }}>
                        <img
                          src={coverPreview}
                          alt="Replacement cover preview"
                          style={{ width: '100%', maxHeight: '160px', objectFit: 'cover', display: 'block' }}
                        />
                        <button
                          type="button"
                          onClick={handleRemoveCoverReplacement}
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
                          }}
                          title="Revert to approved banner"
                        >
                          <X size={15} color="var(--ep-text-primary)" />
                        </button>
                      </div>
                      <p style={{ fontSize: '12px', color: 'var(--ep-primary)', fontWeight: 500, marginTop: '6px' }}>
                        Replacement cover banner selected (will replace approved banner upon admin approval).
                      </p>
                    </div>
                  ) : currentCoverUrl ? (
                    <div>
                      <div style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '16px',
                        flexWrap: 'wrap',
                      }}>
                        <div style={{
                          width: '160px',
                          height: '80px',
                          borderRadius: 'var(--ep-radius-container)',
                          overflow: 'hidden',
                          border: '1px solid var(--ep-border)',
                          backgroundColor: 'var(--ep-canvas)',
                        }}>
                          <img
                            src={currentCoverUrl}
                            alt="Current approved cover banner"
                            style={{ width: '100%', height: '100%', objectFit: 'cover' }}
                          />
                        </div>
                        <div>
                          <p style={{ fontSize: '12px', fontWeight: 600, color: 'var(--ep-text-primary)', margin: '0 0 4px 0' }}>
                            Currently Approved Banner
                          </p>
                          <label style={{
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: '6px',
                            padding: '6px 12px',
                            borderRadius: 'var(--ep-radius-btn)',
                            border: '1px solid var(--ep-border)',
                            backgroundColor: '#ffffff',
                            fontSize: '12px',
                            fontWeight: 500,
                            color: 'var(--ep-text-primary)',
                            cursor: hasPendingUpdate ? 'not-allowed' : 'pointer',
                          }}>
                            <UploadCloud size={14} />
                            <span>Upload Replacement Banner</span>
                            <input
                              type="file"
                              accept="image/jpeg,image/png,image/webp"
                              disabled={hasPendingUpdate}
                              onChange={handleCoverChange}
                              style={{ display: 'none' }}
                            />
                          </label>
                        </div>
                      </div>
                    </div>
                  ) : (
                    <label style={{
                      display: 'flex',
                      flexDirection: 'column',
                      alignItems: 'center',
                      padding: '20px',
                      border: '2px dashed var(--ep-border)',
                      borderRadius: 'var(--ep-radius-container)',
                      backgroundColor: 'var(--ep-canvas)',
                      cursor: hasPendingUpdate ? 'not-allowed' : 'pointer',
                    }}>
                      <UploadCloud size={24} color="var(--ep-text-secondary)" style={{ marginBottom: '6px' }} />
                      <span style={{ fontSize: '13px', fontWeight: 500 }}>Upload Cover Banner</span>
                      <span style={{ fontSize: '11px', color: 'var(--ep-text-secondary)' }}>JPEG, PNG, WebP • Max 5 MB</span>
                      <input
                        type="file"
                        accept="image/jpeg,image/png,image/webp"
                        disabled={hasPendingUpdate}
                        onChange={handleCoverChange}
                        style={{ display: 'none' }}
                      />
                    </label>
                  )}
                </div>

                {/* Actions */}
                <div style={{ marginTop: '16px', display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                  <Link
                    to={`/organizer/events/${id}`}
                    className="ep-btn-secondary"
                    style={{ fontSize: '14px', padding: '10px 20px', textDecoration: 'none' }}
                  >
                    Cancel
                  </Link>
                  <button
                    type="submit"
                    disabled={submitting || hasPendingUpdate}
                    className="ep-btn-primary"
                    style={{
                      fontSize: '14px',
                      padding: '10px 24px',
                      borderRadius: 'var(--ep-radius-btn)',
                      cursor: (submitting || hasPendingUpdate) ? 'not-allowed' : 'pointer',
                      opacity: (submitting || hasPendingUpdate) ? 0.6 : 1,
                    }}
                  >
                    {submitting ? 'Submitting Changes...' : 'Submit Changes for Review'}
                  </button>
                </div>
              </form>
            )}
          </div>
        )}
      </main>
    </div>
  );
}
