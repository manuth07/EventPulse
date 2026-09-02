import React from 'react';
import { Link } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { ArrowLeft, Clock, Info } from 'lucide-react';

export function PendingEvents() {
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
      }}>
        {/* Back Link */}
        <Link
          to="/admin"
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
          <span>Back to Admin Dashboard</span>
        </Link>

        {/* Content Panel */}
        <div style={{
          backgroundColor: '#ffffff',
          borderRadius: 'var(--ep-radius-card)',
          border: '1px solid var(--ep-border)',
          padding: '32px',
          boxShadow: 'var(--ep-shadow-card)',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '8px' }}>
            <Clock size={22} color="var(--ep-primary)" />
            <h1 style={{
              fontSize: '24px',
              fontWeight: 700,
              color: 'var(--ep-text-primary)',
              margin: 0,
              letterSpacing: '-0.005em',
            }}>
              Pending Event Submissions
            </h1>
          </div>
          <p style={{
            fontSize: '14px',
            color: 'var(--ep-text-secondary)',
            margin: '0 0 28px 0',
          }}>
            Review pending event submissions from organizers before publication.
          </p>

          <div style={{
            padding: '24px',
            backgroundColor: '#FFF0E6',
            borderRadius: 'var(--ep-radius-container)',
            border: '1px solid #FFE0CC',
            display: 'flex',
            alignItems: 'flex-start',
            gap: '16px',
          }}>
            <Info size={20} color="var(--ep-primary)" style={{ marginTop: '2px', flexShrink: 0 }} />
            <div>
              <h4 style={{ fontSize: '14px', fontWeight: 600, color: 'var(--ep-text-primary)', margin: '0 0 4px 0' }}>
                Pending Review Queue Shell
              </h4>
              <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: 0, lineHeight: 1.5 }}>
                The EP-31 event-review story will implement the complete interactive review table, approval actions, and rejection feedback UI. Backend endpoints (<code style={{ backgroundColor: 'rgba(0,0,0,0.05)', padding: '2px 4px', borderRadius: '4px' }}>PUT /api/events/&#123;id&#125;/approve</code>, <code style={{ backgroundColor: 'rgba(0,0,0,0.05)', padding: '2px 4px', borderRadius: '4px' }}>PUT /api/events/&#123;id&#125;/publish</code>) are enforced by the <code style={{ backgroundColor: 'rgba(0,0,0,0.05)', padding: '2px 4px', borderRadius: '4px' }}>AdministratorOnly</code> policy.
              </p>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}
