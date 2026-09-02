import React from 'react';
import { Link } from 'react-router-dom';
import { Header } from '../../components/Header/Header';
import { ShieldCheck, FileCheck, Users } from 'lucide-react';

export function AdminDashboard() {
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
              backgroundColor: 'var(--ep-canvas)',
              color: 'var(--ep-text-primary)',
              padding: '4px 10px',
              borderRadius: 'var(--ep-radius-pill)',
              fontSize: '12px',
              fontWeight: 600,
              border: '1px solid var(--ep-border)',
              marginBottom: '8px',
            }}>
              <ShieldCheck size={13} color="var(--ep-primary)" />
              <span>Platform Administration</span>
            </div>
            <h1 style={{
              fontSize: '28px',
              fontWeight: 700,
              color: 'var(--ep-text-primary)',
              margin: 0,
              letterSpacing: '-0.01em',
            }}>
              Admin Dashboard
            </h1>
            <p style={{
              fontSize: '14px',
              color: 'var(--ep-text-secondary)',
              marginTop: '4px',
              margin: 0,
            }}>
              Review EventPulse platform activity and pending event submissions.
            </p>
          </div>

          <Link
            to="/admin/events/pending"
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
            <FileCheck size={16} />
            <span>Pending Events</span>
          </Link>
        </div>

        {/* Overview Grid */}
        <div style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))',
          gap: '20px',
          marginBottom: '32px',
        }}>
          <div style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            padding: '24px',
            boxShadow: 'var(--ep-shadow-card)',
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '12px' }}>
              <FileCheck size={20} color="var(--ep-primary)" />
              <h3 style={{ fontSize: '15px', fontWeight: 600, color: 'var(--ep-text-primary)', margin: 0 }}>
                Event Approvals
              </h3>
            </div>
            <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: '0 0 16px 0', lineHeight: 1.5 }}>
              Review Organizer event submissions and publish approved events to the platform.
            </p>
            <Link
              to="/admin/events/pending"
              className="ep-btn-secondary"
              style={{ fontSize: '13px', padding: '6px 14px', textDecoration: 'none', display: 'inline-block' }}
            >
              Review Submissions →
            </Link>
          </div>

          <div style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px solid var(--ep-border)',
            padding: '24px',
            boxShadow: 'var(--ep-shadow-card)',
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '12px' }}>
              <Users size={20} color="var(--ep-text-secondary)" />
              <h3 style={{ fontSize: '15px', fontWeight: 600, color: 'var(--ep-text-primary)', margin: 0 }}>
                Platform Security & Role Governance
              </h3>
            </div>
            <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', margin: 0, lineHeight: 1.5 }}>
              Role-based authorization active across Identity Service and Event Service backend policies.
            </p>
          </div>
        </div>
      </main>
    </div>
  );
}
