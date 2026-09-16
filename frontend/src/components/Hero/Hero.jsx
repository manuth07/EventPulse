import React, { useState, useEffect, useRef, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search, ArrowRight, Loader2, Ticket } from 'lucide-react';
import heroBackground from '../../assets/images/hero/hero-background.webp';
import { useDebounce } from '../../hooks/useDebounce';
import { fetchEventSuggestions } from '../../services/eventService';

function formatSuggestionDate(dateString) {
  if (!dateString) return '';
  try {
    const d = new Date(dateString);
    return d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short' });
  } catch (e) {
    return '';
  }
}

function SuggestionThumbnail({ src, alt }) {
  const [hasError, setHasError] = useState(false);

  useEffect(() => {
    setHasError(false);
  }, [src]);

  if (!src || hasError) {
    return (
      <div
        style={{
          width: '52px',
          height: '52px',
          minWidth: '52px',
          borderRadius: '10px',
          backgroundColor: '#F3F4F6',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          flexShrink: 0,
          color: '#9CA3AF',
        }}
        aria-hidden="true"
      >
        <Ticket size={22} strokeWidth={1.75} />
      </div>
    );
  }

  return (
    <div
      style={{
        width: '52px',
        height: '52px',
        minWidth: '52px',
        borderRadius: '10px',
        overflow: 'hidden',
        backgroundColor: '#F3F4F6',
        flexShrink: 0,
      }}
    >
      <img
        src={src}
        alt={alt || ''}
        loading="lazy"
        onError={() => setHasError(true)}
        style={{
          width: '100%',
          height: '100%',
          objectFit: 'cover',
          display: 'block',
        }}
      />
    </div>
  );
}

export function Hero({ searchQuery, onSearchChange, onSearchSubmit }) {
  const navigate = useNavigate();

  const [isOpen, setIsOpen] = useState(false);
  const [suggestions, setSuggestions] = useState([]);
  const [loading, setLoading] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);

  const containerRef = useRef(null);
  const inputRef = useRef(null);
  const abortControllerRef = useRef(null);

  const debouncedQuery = useDebounce(searchQuery, 300);

  // Fetch suggestions when debounced query changes
  useEffect(() => {
    const trimmed = (debouncedQuery || '').trim();

    // Minimum query length: 2 characters
    if (trimmed.length < 2) {
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
      setSuggestions([]);
      setLoading(false);
      setIsOpen(false);
      setActiveIndex(-1);
      return;
    }

    // Abort previous in-flight suggestion request
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }
    const controller = new AbortController();
    abortControllerRef.current = controller;

    setLoading(true);

    fetchEventSuggestions(trimmed, { signal: controller.signal })
      .then((data) => {
        setSuggestions(Array.isArray(data) ? data : []);
        setIsOpen(true);
        setActiveIndex(-1);
      })
      .catch((err) => {
        if (err.name !== 'AbortError') {
          setSuggestions([]);
        }
      })
      .finally(() => {
        setLoading(false);
      });

    return () => {
      controller.abort();
    };
  }, [debouncedQuery]);

  // Click outside to close suggestion dropdown
  useEffect(() => {
    function handleClickOutside(event) {
      if (containerRef.current && !containerRef.current.contains(event.target)) {
        setIsOpen(false);
        setActiveIndex(-1);
      }
    }

    document.addEventListener('mousedown', handleClickOutside);
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, []);

  const handleSelectSuggestion = useCallback((suggestion) => {
    setIsOpen(false);
    setActiveIndex(-1);
    if (suggestion && suggestion.id) {
      navigate(`/events/${suggestion.id}`);
    }
  }, [navigate]);

  const handleExecuteFullSearch = useCallback((term) => {
    setIsOpen(false);
    setActiveIndex(-1);
    if (onSearchSubmit) {
      onSearchSubmit(term || searchQuery);
    }
  }, [onSearchSubmit, searchQuery]);

  const handleSubmit = (e) => {
    e.preventDefault();
    if (activeIndex >= 0 && activeIndex < suggestions.length) {
      handleSelectSuggestion(suggestions[activeIndex]);
    } else {
      handleExecuteFullSearch(searchQuery);
    }
  };

  const handleKeyDown = (e) => {
    if (!isOpen && (e.key === 'ArrowDown' || e.key === 'ArrowUp')) {
      if ((searchQuery || '').trim().length >= 2) {
        setIsOpen(true);
      }
      return;
    }

    if (e.key === 'ArrowDown') {
      e.preventDefault();
      const nextIndex = activeIndex < suggestions.length - 1 ? activeIndex + 1 : 0;
      setActiveIndex(nextIndex);
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      const prevIndex = activeIndex > 0 ? activeIndex - 1 : suggestions.length - 1;
      setActiveIndex(prevIndex);
    } else if (e.key === 'Escape') {
      e.preventDefault();
      setIsOpen(false);
      setActiveIndex(-1);
    } else if (e.key === 'Enter') {
      if (activeIndex >= 0 && activeIndex < suggestions.length) {
        e.preventDefault();
        handleSelectSuggestion(suggestions[activeIndex]);
      }
    }
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
      overflow: 'visible',
    }}>
      {/* Semi-transparent dark overlay to improve text contrast while keeping stage lighting vibrant */}
      <div
        style={{
          position: 'absolute',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: 'linear-gradient(180deg, rgba(0, 0, 0, 0.44) 0%, rgba(0, 0, 0, 0.52) 100%)',
          pointerEvents: 'none',
          zIndex: 1,
        }}
        aria-hidden="true"
      />

      <div className="container" style={{ maxWidth: '840px', textAlign: 'center', position: 'relative', zIndex: 10 }}>
        {/* Search Control with Autocomplete Dropdown */}
        <div ref={containerRef} style={{ position: 'relative', maxWidth: '560px', width: '100%', margin: '0 auto 28px' }}>
          <form onSubmit={handleSubmit} style={{
            display: 'flex',
            alignItems: 'center',
            width: '100%',
            minHeight: '48px',
            maxHeight: '52px',
            backgroundColor: '#ffffff',
            padding: '4px 6px 4px 16px',
            borderRadius: '9999px',
            border: '1px solid rgba(255, 255, 255, 0.25)',
            boxShadow: '0 4px 20px rgba(0, 0, 0, 0.20)',
            position: 'relative',
            zIndex: 2,
          }}>
            <div style={{ flex: 1, display: 'flex', alignItems: 'center' }}>
              <Search size={18} color="var(--ep-text-secondary, #6b7280)" style={{ marginRight: '10px', flexShrink: 0 }} />
              <input
                ref={inputRef}
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
                placeholder="Search events, venues, or categories..."
                value={searchQuery}
                onChange={(e) => onSearchChange(e.target.value)}
                onFocus={() => {
                  if ((searchQuery || '').trim().length >= 2) {
                    setIsOpen(true);
                  }
                }}
                onKeyDown={handleKeyDown}
                role="combobox"
                aria-expanded={isOpen}
                aria-haspopup="listbox"
                aria-autocomplete="list"
                aria-label="Search events, venues, or categories"
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

          {/* Autocomplete Suggestion Panel */}
          {isOpen && (searchQuery || '').trim().length >= 2 && (
            <div
              role="listbox"
              aria-label="Search suggestions"
              style={{
                position: 'absolute',
                top: 'calc(100% + 8px)',
                left: 0,
                right: 0,
                backgroundColor: '#ffffff',
                borderRadius: '16px',
                border: '1px solid var(--ep-border, #E5E7EB)',
                boxShadow: '0 12px 32px rgba(0, 0, 0, 0.18)',
                overflow: 'hidden',
                zIndex: 50,
                textAlign: 'left',
              }}
            >
              {loading && suggestions.length === 0 ? (
                <div style={{
                  padding: '16px',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: '8px',
                  color: 'var(--ep-text-secondary, #6b7280)',
                  fontSize: '13px',
                }}>
                  <Loader2 size={16} className="ep-spin" />
                  <span>Searching events...</span>
                </div>
              ) : suggestions.length === 0 ? (
                <div style={{
                  padding: '16px 20px',
                  color: 'var(--ep-text-secondary, #6b7280)',
                  fontSize: '13px',
                  textAlign: 'center',
                }}>
                  No matching events
                </div>
              ) : (
                <div>
                  {suggestions.map((item, idx) => {
                    const isSelected = activeIndex === idx;
                    const dateFormatted = formatSuggestionDate(item.eventDate);
                    const metaParts = [item.category, item.venue].filter(Boolean).join(' · ');

                    return (
                      <div
                        key={item.id}
                        role="option"
                        aria-selected={isSelected}
                        onMouseDown={(e) => {
                          e.preventDefault();
                          handleSelectSuggestion(item);
                        }}
                        onMouseEnter={() => setActiveIndex(idx)}
                        style={{
                          padding: '10px 16px',
                          cursor: 'pointer',
                          backgroundColor: isSelected ? '#F3F4F6' : '#ffffff',
                          borderBottom: idx < suggestions.length - 1 ? '1px solid #F3F4F6' : 'none',
                          transition: 'background-color 0.12s ease',
                          display: 'flex',
                          alignItems: 'center',
                          gap: '12px',
                        }}
                      >
                        {/* Event thumbnail */}
                        <SuggestionThumbnail src={item.imageUrl} alt={item.title} />

                        {/* Main information */}
                        <div style={{ minWidth: 0, flex: 1, textAlign: 'left' }}>
                          <div style={{
                            fontSize: '14px',
                            fontWeight: 600,
                            color: isSelected ? 'var(--ep-primary, #FF5B00)' : 'var(--ep-text-primary, #111827)',
                            whiteSpace: 'nowrap',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                            lineHeight: '1.25',
                          }}>
                            {item.title}
                          </div>
                          {metaParts && (
                            <div style={{
                              fontSize: '12px',
                              color: 'var(--ep-text-secondary, #6b7280)',
                              marginTop: '3px',
                              whiteSpace: 'nowrap',
                              overflow: 'hidden',
                              textOverflow: 'ellipsis',
                              lineHeight: '1.25',
                            }}>
                              {metaParts}
                            </div>
                          )}
                        </div>

                        {/* Date aligned toward the right */}
                        {dateFormatted && (
                          <div style={{
                            fontSize: '12px',
                            fontWeight: 600,
                            color: 'var(--ep-text-secondary, #6b7280)',
                            flexShrink: 0,
                            textAlign: 'right',
                            marginLeft: 'auto',
                            paddingLeft: '8px',
                            whiteSpace: 'nowrap',
                          }}>
                            {dateFormatted}
                          </div>
                        )}
                      </div>
                    );
                  })}

                  {/* See all results for query link */}
                  <div
                    onMouseDown={(e) => {
                      e.preventDefault();
                      handleExecuteFullSearch(searchQuery);
                    }}
                    style={{
                      padding: '11px 18px',
                      borderTop: '1px solid var(--ep-border, #E5E7EB)',
                      fontSize: '13px',
                      fontWeight: 600,
                      color: 'var(--ep-primary, #FF5B00)',
                      backgroundColor: '#FAFAFA',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                    }}
                  >
                    <span>See all results for &ldquo;{searchQuery.trim()}&rdquo;</span>
                    <ArrowRight size={14} />
                  </div>
                </div>
              )}
            </div>
          )}
        </div>

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
