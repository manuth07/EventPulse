import React from 'react';

export function Hero({ searchQuery, onSearchChange, onSearchSubmit }) {
  const handleSubmit = (e) => {
    e.preventDefault();
    if (onSearchSubmit) onSearchSubmit(searchQuery);
  };

  return (
    <section style={{
      backgroundColor: '#ffffff',
      borderBottom: '1px solid var(--ep-border)',
      paddingTop: '56px',
      paddingBottom: '56px',
      marginBottom: '32px'
    }}>
      <div className="container" style={{ maxWidth: '840px', textAlign: 'center' }}>
        <div style={{
          display: 'inline-block',
          backgroundColor: 'var(--ep-soft-accent)',
          color: 'var(--ep-primary)',
          fontSize: '13px',
          fontWeight: 600,
          padding: '6px 14px',
          borderRadius: 'var(--ep-radius-pill)',
          marginBottom: '16px'
        }}>
          Sri Lanka's Event Marketplace
        </div>

        <h1 className="ep-hero-title mb-3">
          Discover experiences worth remembering.
        </h1>
        <p className="ep-body-large mb-4" style={{ maxWidth: '600px', margin: '0 auto 28px' }}>
          Explore concerts, conferences, workshops, and live performances happening around you.
        </p>

        {/* Large Search Bar */}
        <form onSubmit={handleSubmit} style={{
          display: 'flex',
          gap: '10px',
          maxWidth: '640px',
          margin: '0 auto',
          backgroundColor: '#ffffff',
          padding: '6px',
          borderRadius: '16px',
          border: '1px solid var(--ep-border)',
          boxShadow: 'var(--ep-shadow-hover)'
        }}>
          <div style={{ flex: 1, display: 'flex', alignItems: 'center', paddingLeft: '12px' }}>
            <span style={{ marginRight: '8px', color: 'var(--ep-text-secondary)' }}>🔍</span>
            <input
              type="text"
              className="ep-input"
              style={{ border: 'none', boxShadow: 'none', paddingLeft: 0 }}
              placeholder="Search by event title, venue, or keyword..."
              value={searchQuery}
              onChange={(e) => onSearchChange(e.target.value)}
              aria-label="Search events"
            />
          </div>
          <button type="submit" className="ep-btn-primary" style={{ padding: '12px 24px', flexShrink: 0 }}>
            Search
          </button>
        </form>
      </div>
    </section>
  );
}
