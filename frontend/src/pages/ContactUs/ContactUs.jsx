import React, { useState } from 'react';
import { Layout } from '../../components/layout/Layout';
import { Mail, MessageCircle, Clock, Send, CheckCircle2 } from 'lucide-react';

export function ContactUs() {
  const [formData, setFormData] = useState({
    name: '',
    email: '',
    subject: '',
    message: '',
  });
  const [submitted, setSubmitted] = useState(false);

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!formData.name.trim() || !formData.email.trim() || !formData.message.trim()) {
      return;
    }
    setSubmitted(true);
  };

  const handleReset = () => {
    setFormData({ name: '', email: '', subject: '', message: '' });
    setSubmitted(false);
  };

  return (
    <Layout style={{ backgroundColor: 'var(--ep-canvas)' }} mainStyle={{ paddingBottom: '64px' }}>
      {/* Header Banner */}
      <section
        style={{
          background: 'linear-gradient(135deg, #0f172a 0%, #1e293b 100%)',
          color: '#ffffff',
          padding: '56px 20px',
          textAlign: 'center',
        }}
      >
        <div style={{ maxWidth: '800px', margin: '0 auto' }}>
          <h1
            style={{
              fontSize: '36px',
              fontWeight: 800,
              letterSpacing: '-0.02em',
              marginBottom: '12px',
              fontFamily: 'var(--ep-font-heading, "Plus Jakarta Sans", sans-serif)',
              lineHeight: 1.2,
            }}
          >
            Contact Us
          </h1>
          <p
            style={{
              fontSize: '16px',
              lineHeight: 1.6,
              color: '#94a3b8',
              maxWidth: '560px',
              margin: '0 auto',
            }}
          >
            Have a question about your booking, refund, or organizing an event? We're here to help you every step of the way.
          </p>
        </div>
      </section>

      {/* Main Container */}
      <div className="container" style={{ maxWidth: '1040px', paddingLeft: '16px', paddingRight: '16px', paddingTop: '40px' }}>
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))',
            gap: '32px',
            alignItems: 'start',
          }}
        >
          {/* Left Column: Direct Support Details */}
          <div>
            <h2
              style={{
                fontSize: '20px',
                fontWeight: 700,
                color: 'var(--ep-text-primary, #1D1D1F)',
                marginBottom: '16px',
                fontFamily: 'var(--ep-font-heading, "Plus Jakarta Sans", sans-serif)',
              }}
            >
              Get in Touch Directly
            </h2>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              {/* WhatsApp Support Card */}
              <a
                href="https://wa.me/94771234567"
                target="_blank"
                rel="noopener noreferrer"
                style={{
                  backgroundColor: '#ffffff',
                  borderRadius: 'var(--ep-radius-card, 16px)',
                  border: '1px solid var(--ep-border, #E5E5EA)',
                  padding: '20px',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '16px',
                  textDecoration: 'none',
                  color: 'inherit',
                  boxShadow: 'var(--ep-shadow-card, 0 2px 8px rgba(0, 0, 0, 0.04))',
                  transition: 'transform 150ms ease, box-shadow 150ms ease',
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.transform = 'translateY(-2px)';
                  e.currentTarget.style.boxShadow = '0 6px 16px rgba(0,0,0,0.08)';
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.transform = 'translateY(0)';
                  e.currentTarget.style.boxShadow = 'var(--ep-shadow-card, 0 2px 8px rgba(0, 0, 0, 0.04))';
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
                    flexShrink: 0,
                  }}
                >
                  <MessageCircle size={22} />
                </div>
                <div>
                  <div style={{ fontSize: '13px', fontWeight: 600, color: '#059669', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                    WhatsApp Text Support
                  </div>
                  <div style={{ fontSize: '16px', fontWeight: 700, color: 'var(--ep-text-primary, #1D1D1F)' }}>
                    +94 77 123 4567
                  </div>
                  <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary, #64748b)' }}>
                    Fast text responses during service hours
                  </div>
                </div>
              </a>

              {/* Email Support Card */}
              <a
                href="mailto:support@eventpulse.com"
                style={{
                  backgroundColor: '#ffffff',
                  borderRadius: 'var(--ep-radius-card, 16px)',
                  border: '1px solid var(--ep-border, #E5E5EA)',
                  padding: '20px',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '16px',
                  textDecoration: 'none',
                  color: 'inherit',
                  boxShadow: 'var(--ep-shadow-card, 0 2px 8px rgba(0, 0, 0, 0.04))',
                  transition: 'transform 150ms ease, box-shadow 150ms ease',
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.transform = 'translateY(-2px)';
                  e.currentTarget.style.boxShadow = '0 6px 16px rgba(0,0,0,0.08)';
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.transform = 'translateY(0)';
                  e.currentTarget.style.boxShadow = 'var(--ep-shadow-card, 0 2px 8px rgba(0, 0, 0, 0.04))';
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
                    flexShrink: 0,
                  }}
                >
                  <Mail size={22} />
                </div>
                <div>
                  <div style={{ fontSize: '13px', fontWeight: 600, color: '#EA580C', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                    Email Support
                  </div>
                  <div style={{ fontSize: '16px', fontWeight: 700, color: 'var(--ep-text-primary, #1D1D1F)' }}>
                    support@eventpulse.com
                  </div>
                  <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary, #64748b)' }}>
                    Replies within 2 to 4 business hours
                  </div>
                </div>
              </a>

              {/* Service Hours Card */}
              <div
                style={{
                  backgroundColor: '#ffffff',
                  borderRadius: 'var(--ep-radius-card, 16px)',
                  border: '1px solid var(--ep-border, #E5E5EA)',
                  padding: '20px',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '16px',
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
                    flexShrink: 0,
                  }}
                >
                  <Clock size={22} />
                </div>
                <div>
                  <div style={{ fontSize: '13px', fontWeight: 600, color: '#2563EB', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                    Service Hours
                  </div>
                  <div style={{ fontSize: '15px', fontWeight: 700, color: 'var(--ep-text-primary, #1D1D1F)' }}>
                    Mon - Sat: 9:00 AM - 7:00 PM IST
                  </div>
                  <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary, #64748b)' }}>
                    Emergency ticketing assistance on event nights
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Right Column: Static Feedback/Inquiry Form */}
          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: 'var(--ep-radius-card, 16px)',
              border: '1px solid var(--ep-border, #E5E5EA)',
              padding: '32px',
              boxShadow: 'var(--ep-shadow-card, 0 2px 8px rgba(0, 0, 0, 0.04))',
            }}
          >
            <h2
              style={{
                fontSize: '20px',
                fontWeight: 700,
                color: 'var(--ep-text-primary, #1D1D1F)',
                marginBottom: '8px',
                fontFamily: 'var(--ep-font-heading, "Plus Jakarta Sans", sans-serif)',
              }}
            >
              Send an Inquiry
            </h2>
            <p style={{ fontSize: '13px', color: 'var(--ep-text-secondary, #64748b)', marginBottom: '24px' }}>
              Fill in the details below and our team will get back to you shortly.
            </p>

            {submitted ? (
              <div
                style={{
                  textAlign: 'center',
                  padding: '32px 16px',
                  backgroundColor: '#ECFDF5',
                  borderRadius: '12px',
                  border: '1px solid #A7F3D0',
                }}
              >
                <CheckCircle2 size={42} color="#059669" style={{ margin: '0 auto 12px' }} />
                <h3 style={{ fontSize: '18px', fontWeight: 700, color: '#065F46', marginBottom: '8px' }}>
                  Thank You for Reaching Out!
                </h3>
                <p style={{ fontSize: '14px', color: '#047857', maxWidth: '360px', margin: '0 auto 20px' }}>
                  Your message has been received. Our support team will respond to {formData.email || 'your email'} shortly.
                </p>
                <button
                  type="button"
                  onClick={handleReset}
                  className="ep-btn-secondary"
                  style={{
                    padding: '8px 18px',
                    fontSize: '13px',
                    fontWeight: 600,
                    borderRadius: '8px',
                    cursor: 'pointer',
                  }}
                >
                  Send Another Message
                </button>
              </div>
            ) : (
              <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                <div>
                  <label
                    htmlFor="contact-name"
                    style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary, #1D1D1F)', marginBottom: '6px' }}
                  >
                    Your Name
                  </label>
                  <input
                    id="contact-name"
                    type="text"
                    required
                    placeholder="e.g. Kasun Perera"
                    value={formData.name}
                    onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                    style={{
                      width: '100%',
                      padding: '10px 14px',
                      borderRadius: '8px',
                      border: '1px solid var(--ep-border, #E5E5EA)',
                      fontSize: '14px',
                      outline: 'none',
                    }}
                  />
                </div>

                <div>
                  <label
                    htmlFor="contact-email"
                    style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary, #1D1D1F)', marginBottom: '6px' }}
                  >
                    Email Address
                  </label>
                  <input
                    id="contact-email"
                    type="email"
                    required
                    placeholder="e.g. kasun@example.com"
                    value={formData.email}
                    onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                    style={{
                      width: '100%',
                      padding: '10px 14px',
                      borderRadius: '8px',
                      border: '1px solid var(--ep-border, #E5E5EA)',
                      fontSize: '14px',
                      outline: 'none',
                    }}
                  />
                </div>

                <div>
                  <label
                    htmlFor="contact-subject"
                    style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary, #1D1D1F)', marginBottom: '6px' }}
                  >
                    Subject
                  </label>
                  <input
                    id="contact-subject"
                    type="text"
                    placeholder="e.g. Booking inquiry, Payment verification, Event listing"
                    value={formData.subject}
                    onChange={(e) => setFormData({ ...formData, subject: e.target.value })}
                    style={{
                      width: '100%',
                      padding: '10px 14px',
                      borderRadius: '8px',
                      border: '1px solid var(--ep-border, #E5E5EA)',
                      fontSize: '14px',
                      outline: 'none',
                    }}
                  />
                </div>

                <div>
                  <label
                    htmlFor="contact-message"
                    style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--ep-text-primary, #1D1D1F)', marginBottom: '6px' }}
                  >
                    Message
                  </label>
                  <textarea
                    id="contact-message"
                    required
                    rows={4}
                    placeholder="How can we assist you?"
                    value={formData.message}
                    onChange={(e) => setFormData({ ...formData, message: e.target.value })}
                    style={{
                      width: '100%',
                      padding: '10px 14px',
                      borderRadius: '8px',
                      border: '1px solid var(--ep-border, #E5E5EA)',
                      fontSize: '14px',
                      outline: 'none',
                      resize: 'vertical',
                      fontFamily: 'inherit',
                    }}
                  />
                </div>

                <button
                  type="submit"
                  className="ep-btn-primary"
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: '8px',
                    padding: '12px 24px',
                    fontSize: '14px',
                    fontWeight: 600,
                    borderRadius: 'var(--ep-radius-btn, 12px)',
                    cursor: 'pointer',
                    marginTop: '8px',
                    border: 'none',
                    backgroundColor: 'var(--ep-primary, #FF5B00)',
                    color: '#ffffff',
                  }}
                >
                  <Send size={16} />
                  <span>Send Message</span>
                </button>
              </form>
            )}
          </div>
        </div>
      </div>
    </Layout>
  );
}

export default ContactUs;
