import React from 'react';
import { Search } from 'lucide-react';
import heroBackground from '../../assets/images/background.webp';

export function Hero({ searchQuery, onSearchChange, onSearchSubmit }) {
  const handleSubmit = (e) => {
    e.preventDefault();
    if (onSearchSubmit) onSearchSubmit(searchQuery);
  };

  return (
    <section style={{
      position: 'relative',
      backgroundImage: `url(${heroBackground})`,
      backgroundSize: 'cover',
      backgroundPosition: 'center center',
      backgroundRepeat: 'no-repeat',
      borderBottom: '1px solid var(--ep-border)',
      paddingTop: '48px',
      paddingBottom: '56px',
      marginBottom: '32px',
      overflow: 'hidden',
    }}>
      <div className="container" style={{ maxWidth: '840px', textAlign: 'center', position: 'relative', zIndex: 1 }}>
        {/* Compact Pill Search Bar at the Top */}
        <form onSubmit={handleSubmit} style={{
          display: 'flex',
          alignItems: 'center',
          maxWidth: '560px',
          width: '100%',
          minHeight: '48px',
          maxHeight: '52px',
          margin: '0 auto 28px',
          backgroundColor: '#ffffff',
          padding: '4px 6px 4px 16px',
          borderRadius: '9999px',
          border: '1px solid rgba(255, 255, 255, 0.25)',
          boxShadow: '0 4px 20px rgba(0, 0, 0, 0.20)',
        }}>
          <div style={{ flex: 1, display: 'flex', alignItems: 'center' }}>
            <Search size={18} color="var(--ep-text-secondary, #6b7280)" style={{ marginRight: '10px', flexShrink: 0 }} />
            <input
              type="text"
              className="ep-input"
              style={{
                border: 'none',
                boxShadow: 'none',
                outline: 'none',
                padding: '8px 0',
                backgroundColor: 'transparent',
                color: '#111827',
                fontSize: '14px',
              }}
              placeholder="Search by event title, venue, or keyword..."
              value={searchQuery}
              onChange={(e) => onSearchChange(e.target.value)}
              aria-label="Search events"
            />
          </div>
          <button
            type="submit"
            className="ep-btn-primary"
            style={{
              borderRadius: '9999px',
              padding: '8px 20px',
              fontSize: '14px',
              fontWeight: 600,
              flexShrink: 0,
              backgroundColor: 'var(--ep-primary, #FF5B00)',
              border: 'none',
              cursor: 'pointer',
            }}
          >
            Search
          </button>
        </form>

        {/* Badge */}
        <div style={{
          display: 'inline-block',
          backgroundColor: 'rgba(0, 0, 0, 0.45)',
          backdropFilter: 'blur(4px)',
          WebkitBackdropFilter: 'blur(4px)',
          color: 'var(--ep-primary, #FF5B00)',
          border: '1px solid rgba(255, 255, 255, 0.2)',
          fontSize: '13px',
          fontWeight: 600,
          padding: '6px 16px',
          borderRadius: '9999px',
          marginBottom: '20px',
          letterSpacing: '0.01em',
        }}>
          Sri Lanka's Event Marketplace
        </div>

        {/* Heading */}
        <h1 className="ep-hero-title mb-3" style={{
          color: '#FFFFFF',
          textShadow: '0 2px 10px rgba(0, 0, 0, 0.65)',
        }}>
          Discover <span style={{ color: 'var(--ep-primary, #FF5B00)' }}>experiences</span> worth remembering.
        </h1>

        {/* Subtitle */}
        <p className="ep-body-large" style={{
          maxWidth: '600px',
          margin: '0 auto',
          color: 'rgba(255, 255, 255, 0.92)',
          textShadow: '0 1px 6px rgba(0, 0, 0, 0.6)',
          lineHeight: 1.5,
        }}>
          Explore concerts, conferences, workshops, and live performances happening around you.
        </p>
      </div>
    </section>
  );
}
