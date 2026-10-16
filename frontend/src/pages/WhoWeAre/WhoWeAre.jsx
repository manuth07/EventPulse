import React from 'react';
import { Link } from 'react-router-dom';
import { Layout } from '../../components/layout/Layout';
import { ShieldCheck, Zap, Compass, ArrowLeft, Ticket } from 'lucide-react';

export function WhoWeAre() {
  return (
    <Layout style={{ backgroundColor: 'var(--ep-canvas)' }} mainStyle={{ paddingBottom: '64px' }}>
      {/* Hero Banner */}
      <section
        style={{
          background: 'linear-gradient(135deg, #0f172a 0%, #1e293b 100%)',
          color: '#ffffff',
          padding: '64px 20px',
          textAlign: 'center',
        }}
      >
        <div style={{ maxWidth: '800px', margin: '0 auto' }}>
          <div
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '8px',
              padding: '6px 14px',
              borderRadius: '9999px',
              backgroundColor: 'rgba(255, 91, 0, 0.15)',
              border: '1px solid rgba(255, 91, 0, 0.3)',
              color: '#ff5b00',
              fontSize: '13px',
              fontWeight: 600,
              marginBottom: '20px',
            }}
          >
            <Ticket size={16} />
            <span>Sri Lanka's Premier Event Marketplace</span>
          </div>

          <h1
            style={{
              fontSize: '38px',
              fontWeight: 800,
              letterSpacing: '-0.02em',
              marginBottom: '16px',
              fontFamily: 'var(--ep-font-heading, "Plus Jakarta Sans", sans-serif)',
              lineHeight: 1.2,
            }}
          >
            Who We Are
          </h1>

          <p
            style={{
              fontSize: '17px',
              lineHeight: 1.6,
              color: '#94a3b8',
              maxWidth: '640px',
              margin: '0 auto',
            }}
          >
            Powering live experiences across Sri Lanka with a seamless, real-time platform built for concertgoers, theater lovers, conference attendees, and passionate organizers.
          </p>
        </div>
      </section>

      {/* Main Content Container */}
      <div className="container" style={{ maxWidth: '1024px', paddingLeft: '16px', paddingRight: '16px', paddingTop: '48px' }}>
        {/* Mission Statement Card */}
        <div
          style={{
            backgroundColor: '#ffffff',
            borderRadius: 'var(--ep-radius-card, 16px)',
            border: '1px solid var(--ep-border, #E5E5EA)',
            padding: '36px',
            boxShadow: 'var(--ep-shadow-card, 0 2px 8px rgba(0, 0, 0, 0.04))',
            marginBottom: '40px',
          }}
        >
          <h2
            style={{
              fontSize: '22px',
              fontWeight: 700,
              color: 'var(--ep-text-primary, #1D1D1F)',
              marginBottom: '14px',
              fontFamily: 'var(--ep-font-heading, "Plus Jakarta Sans", sans-serif)',
            }}
          >
            Our Mission
          </h2>
          <p
            style={{
              fontSize: '15px',
              lineHeight: 1.7,
              color: 'var(--ep-text-secondary, #475569)',
              margin: 0,
            }}
          >
            At EventPulse, we believe live entertainment connects communities in unforgettable ways. Our goal is to eradicate fraudulent ticketing, long queueing, and ticket loss through cutting-edge digital verification, automated QR access, and transparent pricing for every Sri Lankan event.
          </p>
        </div>

        {/* Core Platform Pillars */}
        <div style={{ marginBottom: '48px' }}>
          <h2
            style={{
              fontSize: '22px',
              fontWeight: 700,
              color: 'var(--ep-text-primary, #1D1D1F)',
              marginBottom: '24px',
              textAlign: 'center',
              fontFamily: 'var(--ep-font-heading, "Plus Jakarta Sans", sans-serif)',
            }}
          >
            Core Platform Pillars
          </h2>

          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))',
              gap: '24px',
            }}
          >
            {/* Pillar 1: Security */}
            <div
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card, 16px)',
                border: '1px solid var(--ep-border, #E5E5EA)',
                padding: '28px',
                boxShadow: 'var(--ep-shadow-card, 0 2px 8px rgba(0, 0, 0, 0.04))',
              }}
            >
              <div
                style={{
                  width: '44px',
                  height: '44px',
                  borderRadius: '12px',
                  backgroundColor: '#ECFDF5',
                  color: '#059669',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  marginBottom: '16px',
                }}
              >
                <ShieldCheck size={24} />
              </div>
              <h3 style={{ fontSize: '17px', fontWeight: 700, color: 'var(--ep-text-primary, #1D1D1F)', marginBottom: '8px' }}>
                Bank-Grade Security
              </h3>
              <p style={{ fontSize: '14px', lineHeight: 1.6, color: 'var(--ep-text-secondary, #475569)', margin: 0 }}>
                Every issued ticket is uniquely signed with cryptographic QR codes, protecting event attendees from duplicate scans and predatory scalpers.
              </p>
            </div>

            {/* Pillar 2: Speed */}
            <div
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card, 16px)',
                border: '1px solid var(--ep-border, #E5E5EA)',
                padding: '28px',
                boxShadow: 'var(--ep-shadow-card, 0 2px 8px rgba(0, 0, 0, 0.04))',
              }}
            >
              <div
                style={{
                  width: '44px',
                  height: '44px',
                  borderRadius: '12px',
                  backgroundColor: '#FFF7ED',
                  color: '#EA580C',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  marginBottom: '16px',
                }}
              >
                <Zap size={24} />
              </div>
              <h3 style={{ fontSize: '17px', fontWeight: 700, color: 'var(--ep-text-primary, #1D1D1F)', marginBottom: '8px' }}>
                Real-Time Speed
              </h3>
              <p style={{ fontSize: '14px', lineHeight: 1.6, color: 'var(--ep-text-secondary, #475569)', margin: 0 }}>
                Live inventory locking and frictionless payment checkout ensure your seats are secured in seconds, with instant SMS & email confirmation.
              </p>
            </div>

            {/* Pillar 3: Discoverability */}
            <div
              style={{
                backgroundColor: '#ffffff',
                borderRadius: 'var(--ep-radius-card, 16px)',
                border: '1px solid var(--ep-border, #E5E5EA)',
                padding: '28px',
                boxShadow: 'var(--ep-shadow-card, 0 2px 8px rgba(0, 0, 0, 0.04))',
              }}
            >
              <div
                style={{
                  width: '44px',
                  height: '44px',
                  borderRadius: '12px',
                  backgroundColor: '#EFF6FF',
                  color: '#2563EB',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  marginBottom: '16px',
                }}
              >
                <Compass size={24} />
              </div>
              <h3 style={{ fontSize: '17px', fontWeight: 700, color: 'var(--ep-text-primary, #1D1D1F)', marginBottom: '8px' }}>
                Curated Discoverability
              </h3>
              <p style={{ fontSize: '14px', lineHeight: 1.6, color: 'var(--ep-text-secondary, #475569)', margin: 0 }}>
                Browse by venue, date, and category — from high-energy arena concerts to intimate indie showcases and tech summits across the island.
              </p>
            </div>
          </div>
        </div>

        {/* Back to Events Button */}
        <div style={{ textAlign: 'center' }}>
          <Link
            to="/"
            className="ep-btn-primary"
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '8px',
              padding: '12px 28px',
              fontSize: '14px',
              fontWeight: 600,
              textDecoration: 'none',
              borderRadius: 'var(--ep-radius-btn, 12px)',
            }}
          >
            <ArrowLeft size={16} />
            <span>Back to Events</span>
          </Link>
        </div>
      </div>
    </Layout>
  );
}

export default WhoWeAre;
