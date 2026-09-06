import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { useAuth } from '../../context/AuthContext';
import { Calendar, MapPin, ArrowLeft, CheckCircle, UploadCloud, X } from 'lucide-react';
import { submitEvent } from '../../services/eventService';

export function CreateEvent() {
  const navigate = useNavigate();
  const { accessToken } = useAuth();

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [venue, setVenue] = useState('');
  const [eventDate, setEventDate] = useState('');
  const [price, setPrice] = useState('0');
  const [image, setImage] = useState(null);
  const [imagePreview, setImagePreview] = useState(null);
  const [coverImage, setCoverImage] = useState(null);
  const [coverImagePreview, setCoverImagePreview] = useState(null);

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(false);

  // Cleanup object URLs to prevent memory leaks
  React.useEffect(() => {
    return () => {
      if (imagePreview) {
        URL.revokeObjectURL(imagePreview);
      }
      if (coverImagePreview) {
        URL.revokeObjectURL(coverImagePreview);
      }
    };
  }, [imagePreview, coverImagePreview]);

  const handleImageChange = (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      setError('Please select a valid image file (JPEG, PNG, or WebP) for the poster.');
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      setError('Event poster must be less than 5 MB.');
      return;
    }

    setError(null);
    setImage(file);
    setImagePreview(URL.createObjectURL(file));
  };

  const handleCoverChange = (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      setError('Please select a valid image file (JPEG, PNG, or WebP) for the cover banner.');
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      setError('Event cover image must be less than 5 MB.');
      return;
    }

    setError(null);
    setCoverImage(file);
    setCoverImagePreview(URL.createObjectURL(file));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError(null);

    if (!image) {
      setError('Please provide an event poster.');
      return;
    }

    if (!coverImage) {
      setError('Please provide an event cover image.');
      return;
    }

    setSubmitting(true);

    try {
      const formData = new FormData();
      formData.append('title', title);
      formData.append('description', description);
      formData.append('venue', venue);
      formData.append('eventDate', new Date(eventDate).toISOString());
      formData.append('price', parseFloat(price) || 0);
      formData.append('image', image);
      formData.append('coverImage', coverImage);

      const token = accessToken || sessionStorage.getItem('ep_access_token');
      await submitEvent(formData, token);

      setSuccess(true);
      setTimeout(() => {
        navigate('/organizer');
      }, 2000);
    } catch (err) {
      setError(err.message || 'An error occurred while submitting the event.');
    } finally {
      setSubmitting(false);
    }
  };

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
          to="/organizer"
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
          <span>Back to Organizer Dashboard</span>
        </Link>

        {/* Title Card */}
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
            Submit New Event
          </h1>
          <p style={{
            fontSize: '14px',
            color: 'var(--ep-text-secondary)',
            margin: '0 0 28px 0',
          }}>
            Provide event details below for Administrator review.
          </p>

          {success ? (
            <div style={{
              padding: '24px',
              backgroundColor: '#F2F9F4',
              border: '1px solid #34C759',
              borderRadius: 'var(--ep-radius-container)',
              textAlign: 'center',
            }}>
              <CheckCircle size={32} color="#34C759" style={{ marginBottom: '8px' }} />
              <h3 style={{ fontSize: '16px', fontWeight: 600, color: 'var(--ep-text-primary)', margin: '0 0 4px 0' }}>
                Event Submitted Successfully!
              </h3>
              <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: 0 }}>
                Redirecting to Organizer Dashboard...
              </p>
            </div>
          ) : (
            <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
              {error && (
                <div style={{
                  padding: '12px 16px',
                  backgroundColor: '#FFF2F2',
                  border: '1px solid var(--ep-danger)',
                  borderRadius: 'var(--ep-radius-container)',
                  fontSize: '13px',
                  color: 'var(--ep-danger)',
                }}>
                  {error}
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
                  placeholder="e.g. Summer Music Festival 2026"
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
                  placeholder="Describe your event, highlights, and schedule..."
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
                    placeholder="e.g. Nelum Pokuna Theater, Colombo"
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
                      step="0.01"
                      required
                      value={price}
                      onChange={(e) => setPrice(e.target.value)}
                      placeholder="0.00"
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

              <div>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}>
                  Event Poster *
                </label>
                {imagePreview ? (
                  <div style={{ position: 'relative', width: '100%', maxWidth: '280px' }}>
                    <img
                      src={imagePreview}
                      alt="Event poster preview"
                      style={{ width: '100%', borderRadius: 'var(--ep-radius-container)', display: 'block', border: '1px solid var(--ep-border)' }}
                    />
                    <button
                      type="button"
                      onClick={() => {
                        setImage(null);
                        setImagePreview(null);
                        URL.revokeObjectURL(imagePreview);
                      }}
                      style={{
                        position: 'absolute',
                        top: '8px',
                        right: '8px',
                        background: 'rgba(255, 255, 255, 0.9)',
                        border: 'none',
                        borderRadius: '50%',
                        width: '32px',
                        height: '32px',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        cursor: 'pointer',
                        boxShadow: '0 2px 4px rgba(0,0,0,0.1)',
                      }}
                    >
                      <X size={16} color="var(--ep-text-primary)" />
                    </button>
                  </div>
                ) : (
                  <label style={{
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    justifyContent: 'center',
                    padding: '28px',
                    border: '2px dashed var(--ep-border)',
                    borderRadius: 'var(--ep-radius-container)',
                    backgroundColor: 'var(--ep-canvas)',
                    cursor: 'pointer',
                    transition: 'border-color 0.2s',
                  }}>
                    <UploadCloud size={30} color="var(--ep-text-secondary)" style={{ marginBottom: '10px' }} />
                    <span style={{ fontSize: '14px', fontWeight: 500, color: 'var(--ep-text-primary)', marginBottom: '4px' }}>
                      Click to upload poster
                    </span>
                    <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', textAlign: 'center' }}>
                      Portrait ratio (~4:5) • JPEG, PNG or WebP • Max 5 MB
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

              <div>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary)', marginBottom: '6px' }}>
                  Event Cover / Banner *
                </label>
                {coverImagePreview ? (
                  <div style={{ position: 'relative', width: '100%' }}>
                    <img
                      src={coverImagePreview}
                      alt="Event cover preview"
                      style={{
                        width: '100%',
                        maxHeight: '220px',
                        objectFit: 'cover',
                        borderRadius: 'var(--ep-radius-container)',
                        display: 'block',
                        border: '1px solid var(--ep-border)',
                      }}
                    />
                    <button
                      type="button"
                      onClick={() => {
                        setCoverImage(null);
                        setCoverImagePreview(null);
                        URL.revokeObjectURL(coverImagePreview);
                      }}
                      style={{
                        position: 'absolute',
                        top: '8px',
                        right: '8px',
                        background: 'rgba(255, 255, 255, 0.9)',
                        border: 'none',
                        borderRadius: '50%',
                        width: '32px',
                        height: '32px',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        cursor: 'pointer',
                        boxShadow: '0 2px 4px rgba(0,0,0,0.1)',
                      }}
                    >
                      <X size={16} color="var(--ep-text-primary)" />
                    </button>
                  </div>
                ) : (
                  <label style={{
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    justifyContent: 'center',
                    padding: '28px',
                    border: '2px dashed var(--ep-border)',
                    borderRadius: 'var(--ep-radius-container)',
                    backgroundColor: 'var(--ep-canvas)',
                    cursor: 'pointer',
                    transition: 'border-color 0.2s',
                  }}>
                    <UploadCloud size={30} color="var(--ep-text-secondary)" style={{ marginBottom: '10px' }} />
                    <span style={{ fontSize: '14px', fontWeight: 500, color: 'var(--ep-text-primary)', marginBottom: '4px' }}>
                      Click to upload wide cover banner
                    </span>
                    <span style={{ fontSize: '12px', color: 'var(--ep-text-secondary)', textAlign: 'center' }}>
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

              <div style={{ marginTop: '12px', display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                <Link
                  to="/organizer"
                  className="ep-btn-secondary"
                  style={{ fontSize: '14px', padding: '10px 20px', textDecoration: 'none' }}
                >
                  Cancel
                </Link>
                <button
                  type="submit"
                  disabled={submitting}
                  className="ep-btn-primary"
                  style={{
                    fontSize: '14px',
                    padding: '10px 24px',
                    borderRadius: 'var(--ep-radius-btn)',
                    cursor: submitting ? 'not-allowed' : 'pointer',
                    opacity: submitting ? 0.7 : 1,
                  }}
                >
                  {submitting ? 'Submitting...' : 'Submit Event'}
                </button>
              </div>
            </form>
          )}
        </div>
      </main>
    </div>
  );
}
