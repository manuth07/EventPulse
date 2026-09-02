import React from 'react';
import { Link } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { Calendar, Plus, LayoutDashboard } from 'lucide-react';

export function OrganizerDashboard() {
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
        paddingTop: '40px',
        paddingBottom: '64px',
      }}>
        {/* Header Section */}
        <div style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          marginBottom: '32px',
          flexWrap: 'wrap',
          gap: '16px',
        }}>
          <div>
            <div style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '6px',
              backgroundColor: '#FFF0E6',
              color: 'var(--ep-primary)',
              padding: '4px 10px',
              borderRadius: 'var(--ep-radius-pill)',
              fontSize: '12px',
              fontWeight: 600,
              marginBottom: '8px',
            }}>
              <LayoutDashboard size={13} />
              <span>Organizer Workspace</span>
            </div>
            <h1 style={{
              fontSize: '28px',
              fontWeight: 700,
              color: 'var(--ep-text-primary)',
              margin: 0,
              letterSpacing: '-0.01em',
            }}>
              Organizer Dashboard
            </h1>
            <p style={{
              fontSize: '14px',
              color: 'var(--ep-text-secondary)',
              marginTop: '4px',
              margin: 0,
            }}>
              Manage your event submissions and upcoming events.
            </p>
          </div>

          <Link
            to="/events/create"
            className="ep-btn-primary"
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '8px',
              padding: '10px 20px',
              fontSize: '14px',
              fontWeight: 600,
              textDecoration: 'none',
              borderRadius: 'var(--ep-radius-btn)',
            }}
          >
            <Plus size={16} />
            <span>Create Event</span>
          </Link>
        </div>

        {/* Dashboard Shell Content */}
        <div style={{
          backgroundColor: '#ffffff',
          borderRadius: 'var(--ep-radius-card)',
          border: '1px solid var(--ep-border)',
          padding: '32px',
          boxShadow: 'var(--ep-shadow-card)',
        }}>
          <div style={{
            display: 'flex',
            alignItems: 'center',
            gap: '12px',
            marginBottom: '16px',
          }}>
            <Calendar size={20} color="var(--ep-primary)" />
            <h2 style={{
              fontSize: '18px',
              fontWeight: 600,
              color: 'var(--ep-text-primary)',
              margin: 0,
            }}>
              My Event Submissions
            </h2>
          </div>

          <p style={{
            fontSize: '14px',
            color: 'var(--ep-text-secondary)',
            marginBottom: '24px',
            lineHeight: 1.5,
          }}>
            Events submitted here undergo Administrator review before being published to public visitors.
          </p>

          <div style={{
            padding: '24px',
            backgroundColor: 'var(--ep-canvas)',
            borderRadius: 'var(--ep-radius-container)',
            border: '1px border-dashed var(--ep-border)',
            textAlign: 'center',
          }}>
            <p style={{
              fontSize: '14px',
              color: 'var(--ep-text-secondary)',
              margin: '0 0 16px 0',
            }}>
              No active submissions found in current view.
            </p>
            <Link
              to="/events/create"
              className="ep-btn-secondary"
              style={{
                fontSize: '13px',
                padding: '8px 16px',
                textDecoration: 'none',
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
              }}
            >
              <Plus size={14} />
              <span>Submit your first event</span>
            </Link>
          </div>
        </div>
      </main>
    </div>
  );
}
