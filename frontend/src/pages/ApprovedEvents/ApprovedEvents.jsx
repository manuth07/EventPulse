import React, { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { useAuth } from '../../context/AuthContext';
import { getApprovedEvents, publishEvent } from '../../services/eventService';
import { formatPrice } from '../../utils/currencyFormatter';
import {
  ArrowLeft,
  CheckCircle2,
  Calendar,
  MapPin,
  RefreshCw,
  AlertCircle,
  Rocket,
} from 'lucide-react';

function formatEventDateTime(dateString) {
  if (!dateString) return 'Date TBA';
  try {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-GB', {
      day: 'numeric', month: 'short', year: 'numeric',
    }) + ' • ' + date.toLocaleTimeString('en-US', {
      hour: 'numeric', minute: '2-digit', hour12: true,
    });
  } catch (e) {
    return dateString;
  }
}

export function ApprovedEvents() {
  const { accessToken } = useAuth();
  const [events, setEvents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);
  const [feedback, setFeedback] = useState(null);
  const [publishingId, setPublishingId] = useState(null);

  const getToken = () => accessToken || sessionStorage.getItem('ep_access_token');

  const loadEvents = useCallback(async (isManualRefresh = false) => {
    isManualRefresh ? setRefreshing(true) : setLoading(true);
    setError(null);
    try {
      const data = await getApprovedEvents(getToken());
      setEvents(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message || 'Unable to load approved events.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [accessToken]);

  useEffect(() => { loadEvents(); }, [loadEvents]);

  const handlePublish = async (event) => {
    setPublishingId(event.id);
    try {
      await publishEvent(event.id, getToken());
      setFeedback({ type: 'success', message: `"${event.title}" is now live and visible to the public.` });
      setEvents((prev) => prev.filter((e) => e.id !== event.id));
    } catch (err) {
      setFeedback({ type: 'error', message: err.message || 'Failed to publish event.' });
    } finally {
      setPublishingId(null);
    }
  };

  return (
    <div style={{ minHeight: '100vh', backgroundColor: 'var(--ep-canvas)', display: 'flex', flexDirection: 'column' }}>
      <Header />
      <main className="container" style={{ flex: 1, paddingTop: '32px', paddingBottom: '64px' }}>
        <Link
          to="/admin"
          style={{ display: 'inline-flex', alignItems: 'center', gap: '6px', fontSize: '13px', fontWeight: 500, color: 'var(--ep-text-secondary)', textDecoration: 'none', marginBottom: '24px' }}
        >
          <ArrowLeft size={14} />
          <span>Back to Admin Dashboard</span>
        </Link>

        {feedback && (
          <div style={{
            marginBottom: '24px', padding: '16px 20px', borderRadius: '12px',
            backgroundColor: feedback.type === 'success' ? '#E8F5E9' : '#FFF0EF',
            border: `1px solid ${feedback.type === 'success' ? '#C8E6C9' : '#FFCDD2'}`,
          }}>
            <span style={{ fontSize: '14px', fontWeight: 600, color: feedback.type === 'success' ? '#1B5E20' : 'var(--ep-danger)' }}>
              {feedback.message}
            </span>
          </div>
        )}

        <div style={{
          backgroundColor: '#ffffff', borderRadius: 'var(--ep-radius-card)', border: '1px solid var(--ep-border)',
          padding: '32px', boxShadow: 'var(--ep-shadow-card)',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '8px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
              <CheckCircle2 size={22} color="#2E7D32" />
              <h1 style={{ fontSize: '24px', fontWeight: 700, color: 'var(--ep-text-primary)', margin: 0 }}>
                Approved Events
              </h1>
            </div>
            <button
              type="button"
              onClick={() => loadEvents(true)}
              disabled={refreshing || loading}
              style={{
                display: 'inline-flex', alignItems: 'center', gap: '6px', backgroundColor: 'transparent',
                border: '1px solid var(--ep-border)', borderRadius: 'var(--ep-radius-btn)', padding: '6px 14px',
                fontSize: '13px', fontWeight: 500, color: 'var(--ep-text-secondary)',
                cursor: refreshing || loading ? 'not-allowed' : 'pointer',
              }}
            >
              <RefreshCw size={14} style={{ animation: refreshing ? 'spin 1s linear infinite' : 'none' }} />
              <span>{refreshing ? 'Refreshing...' : 'Refresh'}</span>
            </button>
          </div>

          <p style={{ fontSize: '14px', color: 'var(--ep-text-secondary)', margin: '0 0 28px 0' }}>
            Publish approved events to make them visible to public visitors.
          </p>

          {loading && <p style={{ fontSize: '14px', color: 'var(--ep-text-secondary)' }}>Loading approved events…</p>}

          {!loading && error && (
            <div style={{ padding: '20px', backgroundColor: '#FFF5F5', borderRadius: '12px', border: '1px solid #FED7D7', display: 'flex', gap: '12px' }}>
              <AlertCircle size={20} color="var(--ep-danger)" />
              <div>
                <p style={{ fontSize: '14px', color: 'var(--ep-text-primary)', margin: '0 0 8px 0' }}>{error}</p>
                <button type="button" onClick={() => loadEvents(false)} className="ep-btn-secondary" style={{ fontSize: '13px', padding: '6px 14px' }}>
                  Retry
                </button>
              </div>
            </div>
          )}

          {!loading && !error && events.length === 0 && (
            <div style={{ padding: '48px 24px', textAlign: 'center', backgroundColor: 'var(--ep-canvas)', borderRadius: '12px', border: '1px dashed var(--ep-border)' }}>
              <CheckCircle2 size={32} color="var(--ep-primary)" style={{ margin: '0 auto 12px auto' }} />
              <h3 style={{ fontSize: '16px', fontWeight: 600, color: 'var(--ep-text-primary)', margin: '0 0 6px 0' }}>
                No approved events awaiting publication
              </h3>
              <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: 0 }}>
                Events you approve will appear here, ready to be published.
              </p>
            </div>
          )}

          {!loading && !error && events.length > 0 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              {events.map((event) => (
                <div key={event.id} style={{
                  padding: '20px', borderRadius: 'var(--ep-radius-card)', border: '1px solid var(--ep-border)',
                  backgroundColor: '#ffffff', display: 'flex', gap: '20px', alignItems: 'center', flexWrap: 'wrap',
                }}>
                  <div style={{ flex: 1, minWidth: '240px' }}>
                    <h3 style={{ fontSize: '17px', fontWeight: 700, color: 'var(--ep-text-primary)', margin: '0 0 8px 0' }}>
                      {event.title}
                    </h3>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '4px', fontSize: '13px', color: 'var(--ep-text-secondary)' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                        <MapPin size={14} /><span>{event.venue}</span>
                      </div>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                        <Calendar size={14} /><span>{formatEventDateTime(event.eventDate)}</span>
                      </div>
                    </div>
                  </div>

                  <button
                    type="button"
                    onClick={() => handlePublish(event)}
                    disabled={publishingId === event.id}
                    className="ep-btn-primary"
                    style={{ fontSize: '13px', padding: '10px 20px', display: 'inline-flex', alignItems: 'center', gap: '8px' }}
                  >
                    <Rocket size={15} />
                    <span>{publishingId === event.id ? 'Publishing…' : 'Publish'}</span>
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>
      </main>
      <style>{`@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }`}</style>
    </div>
  );
}