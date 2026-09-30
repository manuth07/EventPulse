import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { useAuth } from '../../context/AuthContext';
import { Calendar, MapPin, ArrowLeft, CheckCircle, UploadCloud, X } from 'lucide-react';
import { submitEvent, getEventCategories } from '../../services/eventService';
import { EVENT_CATEGORIES, VENUE_TYPES } from '../../data/eventConstants';
import { ImageCropperModal } from '../../components/common/ImageCropperModal';

export function CreateEvent() {
  const navigate = useNavigate();
  const { accessToken } = useAuth();

  const [categories, setCategories] = useState(EVENT_CATEGORIES);
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [category, setCategory] = useState(EVENT_CATEGORIES[0]);
  const [venueType, setVenueType] = useState('Indoor');
  const [venue, setVenue] = useState('');

  React.useEffect(() => {
    let mounted = true;
    getEventCategories()
      .then((cats) => {
        if (mounted && Array.isArray(cats) && cats.length > 0) {
          const catValues = cats.map((c) => (typeof c === 'string' ? c : c.value));
          setCategories(catValues);
        }
      })
      .catch(() => {
        // Fallback to EVENT_CATEGORIES is already in place
      });
    return () => {
      mounted = false;
    };
  }, []);
  const [eventDate, setEventDate] = useState('');
  const [price, setPrice] = useState('0');
  const [image, setImage] = useState(null);
  const [imagePreview, setImagePreview] = useState(null);
  const [coverImage, setCoverImage] = useState(null);
  const [coverImagePreview, setCoverImagePreview] = useState(null);

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);
  const [errorDetails, setErrorDetails] = useState([]);
  const [success, setSuccess] = useState(false);

  // Interactive Cropper configuration
  const [cropperConfig, setCropperConfig] = useState({
    isOpen: false,
    imageSrc: '',
    fileName: '',
    fileType: '',
    aspectRatio: 4 / 5,
    aspectTitle: 'Event Poster',
    targetWidth: 1200,
    targetHeight: 1500,
    cropType: 'poster',
  });

  // Cleanup object URLs to prevent memory leaks
  React.useEffect(() => {
    return () => {
      if (imagePreview) {
        URL.revokeObjectURL(imagePreview);
      }
      if (coverImagePreview) {
        URL.revokeObjectURL(coverImagePreview);
      }
      if (cropperConfig.imageSrc) {
        URL.revokeObjectURL(cropperConfig.imageSrc);
      }
    };
  }, [imagePreview, coverImagePreview, cropperConfig.imageSrc]);

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

    // Reset input value so re-selecting the same file fires onChange
    e.target.value = '';

    setError(null);
    const rawUrl = URL.createObjectURL(file);
    setCropperConfig({
      isOpen: true,
      imageSrc: rawUrl,
      fileName: file.name,
      fileType: file.type,
      aspectRatio: 4 / 5,
      aspectTitle: 'Event Poster (Portrait 4:5)',
      targetWidth: 1200,
      targetHeight: 1500,
      cropType: 'poster',
    });
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

    // Reset input value so re-selecting the same file fires onChange
    e.target.value = '';

    setError(null);
    const rawUrl = URL.createObjectURL(file);
    setCropperConfig({
      isOpen: true,
      imageSrc: rawUrl,
      fileName: file.name,
      fileType: file.type,
      aspectRatio: 16 / 6,
      aspectTitle: 'Event Cover Banner (16:6 / 1920x720)',
      targetWidth: 1920,
      targetHeight: 720,
      cropType: 'cover',
    });
  };

  const handleCropComplete = (croppedFile, previewUrl) => {
    if (cropperConfig.cropType === 'poster') {
      if (imagePreview) URL.revokeObjectURL(imagePreview);
      setImage(croppedFile);
      setImagePreview(previewUrl);
    } else {
      if (coverImagePreview) URL.revokeObjectURL(coverImagePreview);
      setCoverImage(croppedFile);
      setCoverImagePreview(previewUrl);
    }

    if (cropperConfig.imageSrc) {
      URL.revokeObjectURL(cropperConfig.imageSrc);
    }

    setCropperConfig((prev) => ({ ...prev, isOpen: false, imageSrc: '' }));
  };

  const handleCropCancel = () => {
    if (cropperConfig.imageSrc) {
      URL.revokeObjectURL(cropperConfig.imageSrc);
    }
    setCropperConfig((prev) => ({ ...prev, isOpen: false, imageSrc: '' }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError(null);
    setErrorDetails([]);

    // Validate string fields & lengths
    const trimmedTitle = title.trim();
    if (!trimmedTitle || trimmedTitle.length < 3) {
      setError('Event title must be at least 3 characters.');
      return;
    }
    if (trimmedTitle.length > 200) {
      setError('Event title cannot exceed 200 characters.');
      return;
    }

    const trimmedDescription = description.trim();
    if (!trimmedDescription || trimmedDescription.length < 10) {
      setError('Event description must be at least 10 characters.');
      return;
    }
    if (trimmedDescription.length > 2000) {
      setError('Event description cannot exceed 2000 characters.');
      return;
    }

    if (!category) {
      setError('Please select an event category.');
      return;
    }

    if (!venueType) {
      setError('Please select a venue type.');
      return;
    }

    const trimmedVenue = venue.trim();
    if (!trimmedVenue || trimmedVenue.length < 3) {
      setError('Venue location must be at least 3 characters.');
      return;
    }
    if (trimmedVenue.length > 200) {
      setError('Venue location cannot exceed 200 characters.');
      return;
    }

    if (!eventDate) {
      setError('Please select an event date and time.');
      return;
    }

    const eventDateTime = new Date(eventDate);
    if (isNaN(eventDateTime.getTime())) {
      setError('Please provide a valid event date and time.');
      return;
    }

    if (eventDateTime.getTime() <= Date.now()) {
      setError('Event date and time must be in the future.');
      return;
    }

    const parsedPrice = Number(price);
    if (isNaN(parsedPrice) || parsedPrice < 0 || !Number.isInteger(parsedPrice)) {
      setError('Ticket price must be entered in whole LKR (0 or greater).');
      return;
    }
    if (parsedPrice > 1000000) {
      setError('Ticket price cannot exceed 1,000,000 LKR.');
      return;
    }

    if (!image) {
      setError('Please provide an event poster image.');
      return;
    }

    if (!coverImage) {
      setError('Please provide an event cover banner image.');
      return;
    }

    setSubmitting(true);

    try {
      const formData = new FormData();
      formData.append('title', trimmedTitle);
      formData.append('description', trimmedDescription);
      formData.append('category', category);
      formData.append('venueType', venueType);
      formData.append('venue', trimmedVenue);
      formData.append('eventDate', eventDateTime.toISOString());
      formData.append('price', String(parsedPrice));

      const posterFileName = image.name || 'poster.jpg';
      formData.append('image', image, posterFileName);

      const coverFileName = coverImage.name || 'cover.jpg';
      formData.append('coverImage', coverImage, coverFileName);

      const token = accessToken || sessionStorage.getItem('ep_access_token');
      await submitEvent(formData, token);

      setSuccess(true);
      setTimeout(() => {
        navigate('/organizer');
      }, 2000);
    } catch (err) {
      console.error('Submit event error:', err);
      const responseData = err.response?.data || err.data;
      let detailedErrors = [];

      if (Array.isArray(responseData?.errors)) {
        detailedErrors = responseData.errors;
      } else if (responseData?.errors && typeof responseData.errors === 'object') {
        detailedErrors = Object.entries(responseData.errors).flatMap(([field, msgs]) =>
          Array.isArray(msgs) ? msgs.map((m) => `${field}: ${m}`) : [`${field}: ${msgs}`]
        );
      } else if (Array.isArray(err.errors) && err.errors.length > 0) {
        detailedErrors = err.errors;
      }

      setErrorDetails(detailedErrors);
      setError(
        responseData?.message ||
        responseData?.title ||
        err.message ||
        'An error occurred while submitting the event.'
      );
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
                  <div style={{ fontWeight: 600, marginBottom: errorDetails.length > 0 ? '6px' : '0' }}>
                    {error}
                  </div>
                  {errorDetails.length > 0 && (
                    <ul style={{ margin: 0, paddingLeft: '18px', display: 'flex', flexDirection: 'column', gap: '3px' }}>
                      {errorDetails.map((detail, idx) => (
                        <li key={idx} style={{ fontSize: '12px' }}>{detail}</li>
                      ))}
                    </ul>
                  )}
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
                    {categories.map((cat) => (
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
                      step="1"
                      required
                      value={price}
                      onChange={(e) => setPrice(e.target.value)}
                      placeholder="0"
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

      <ImageCropperModal
        isOpen={cropperConfig.isOpen}
        imageSrc={cropperConfig.imageSrc}
        fileName={cropperConfig.fileName}
        fileType={cropperConfig.fileType}
        aspectRatio={cropperConfig.aspectRatio}
        aspectTitle={cropperConfig.aspectTitle}
        targetWidth={cropperConfig.targetWidth}
        targetHeight={cropperConfig.targetHeight}
        onCropComplete={handleCropComplete}
        onCancel={handleCropCancel}
      />
    </div>
  );
}
