import React from 'react';
import { Link } from 'react-router-dom';
import { Mail, MessageCircle, ShieldCheck } from 'lucide-react';


const helpfulLinks = [
  { label: 'Events', href: '/' },
  { label: 'EventPulse Deals', href: '/deals' },
  { label: 'My Account', href: '/my-bookings' },
  { label: 'Refund Policy', href: '/refund-policy' },
];

const aboutUsLinks = [
  { label: 'Who We Are', href: '/who-we-are' },
  { label: 'For Organizers', href: '/list-your-event' },
  { label: 'FAQ', href: '/faq' },
  { label: 'Contact Us', href: '/contact' },
];

export function Footer() {
  return (
    <footer
      className="bg-[#d4d4d8] border-t border-zinc-400/70 text-zinc-900"
      style={{
        backgroundColor: '#d4d4d8',
        borderTop: '1px solid rgba(161, 161, 170, 0.7)',
        color: '#18181b',
      }}
    >
      <div
        className="py-7 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto"
        style={{
          maxWidth: '80rem',
          marginLeft: 'auto',
          marginRight: 'auto',
          paddingTop: '1.75rem',
          paddingBottom: '1.75rem',
          paddingLeft: '1.5rem',
          paddingRight: '1.5rem',
        }}
      >
        {/* Compact Grid Scaffolding */}
        <div
          className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-12 gap-8 items-start ep-footer-grid"
          style={{
            alignItems: 'start',
          }}
        >
          {/* Column 1: Brand Section (5 cols on desktop) */}
          <div className="col-span-1 md:col-span-1 lg:col-span-5 ep-footer-brand text-left">
            {/* EventPulse Logo: Clean Typography without icon badge */}
            <Link
              to="/"
              className="inline-flex items-center text-xl font-bold tracking-tight text-decoration-none group"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                textDecoration: 'none',
              }}
            >
              <span
                className="text-xl font-bold tracking-tight"
                style={{
                  fontFamily: 'var(--ep-font-heading, "Plus Jakarta Sans", sans-serif)',
                  fontSize: '20px',
                  fontWeight: 700,
                  letterSpacing: '-0.02em',
                }}
              >
                <span className="text-zinc-900" style={{ color: '#18181b' }}>
                  Event
                </span>
                <span className="text-orange-600" style={{ color: '#ea580c' }}>
                  Pulse
                </span>
              </span>
            </Link>

            {/* Concise Brand Description */}
            <p
              className="text-zinc-700 text-xs mt-2.5 max-w-sm leading-relaxed font-medium"
              style={{
                color: '#3f3f46',
                fontSize: '0.75rem',
                fontWeight: 500,
                lineHeight: '1.6',
                marginTop: '0.625rem',
                marginBottom: '0',
                maxWidth: '380px',
              }}
            >
              EventPulse is Sri Lanka's premier online ticket marketplace, providing a secure and safe platform for discovering and booking live entertainment, concerts, and tech events.
            </p>

            {/* SOCIAL MEDIA BUTTONS - STRICT PIXEL SIZING */}
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginTop: '12px', marginBottom: '16px' }}>
              {/* FACEBOOK */}
              <a
                href="https://facebook.com"
                target="_blank"
                rel="noopener noreferrer"
                aria-label="Facebook"
                style={{
                  width: '32px',
                  height: '32px',
                  minWidth: '32px',
                  minHeight: '32px',
                  borderRadius: '50%',
                  backgroundColor: '#ffffff',
                  border: '1px solid #d4d4d8',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: '#3f3f46',
                  transition: 'all 0.2s ease',
                  boxShadow: '0 1px 2px rgba(0,0,0,0.05)',
                  flexShrink: 0
                }}
                className="hover:!bg-orange-500 hover:!text-white hover:!border-orange-500"
              >
                <svg style={{ width: '16px', height: '16px', minWidth: '16px', minHeight: '16px' }} viewBox="0 0 24 24" fill="currentColor">
                  <path d="M18 2h-3a5 5 0 00-5 5v3H7v4h3v8h4v-8h3l1-4h-4V7a1 1 0 011-1h3z" />
                </svg>
              </a>

              {/* INSTAGRAM */}
              <a
                href="https://instagram.com"
                target="_blank"
                rel="noopener noreferrer"
                aria-label="Instagram"
                style={{
                  width: '32px',
                  height: '32px',
                  minWidth: '32px',
                  minHeight: '32px',
                  borderRadius: '50%',
                  backgroundColor: '#ffffff',
                  border: '1px solid #d4d4d8',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: '#3f3f46',
                  transition: 'all 0.2s ease',
                  boxShadow: '0 1px 2px rgba(0,0,0,0.05)',
                  flexShrink: 0
                }}
                className="hover:!bg-orange-500 hover:!text-white hover:!border-orange-500"
              >
                <svg style={{ width: '16px', height: '16px', minWidth: '16px', minHeight: '16px' }} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="2" y="2" width="20" height="20" rx="5" ry="5"></rect>
                  <path d="M16 11.37A4 4 0 1112.63 8 4 4 0 0116 11.37z"></path>
                  <line x1="17.5" y1="6.5" x2="17.51" y2="6.5"></line>
                </svg>
              </a>

              {/* TIKTOK */}
              <a
                href="https://tiktok.com"
                target="_blank"
                rel="noopener noreferrer"
                aria-label="TikTok"
                style={{
                  width: '32px',
                  height: '32px',
                  minWidth: '32px',
                  minHeight: '32px',
                  borderRadius: '50%',
                  backgroundColor: '#ffffff',
                  border: '1px solid #d4d4d8',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: '#3f3f46',
                  transition: 'all 0.2s ease',
                  boxShadow: '0 1px 2px rgba(0,0,0,0.05)',
                  flexShrink: 0
                }}
                className="hover:!bg-orange-500 hover:!text-white hover:!border-orange-500"
              >
                <svg style={{ width: '16px', height: '16px', minWidth: '16px', minHeight: '16px' }} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M9 12a4 4 0 1 0 4 4V4a5 5 0 0 0 5 5"></path>
                </svg>
              </a>

              {/* WHATSAPP */}
              <a
                href="https://whatsapp.com"
                target="_blank"
                rel="noopener noreferrer"
                aria-label="WhatsApp"
                style={{
                  width: '32px',
                  height: '32px',
                  minWidth: '32px',
                  minHeight: '32px',
                  borderRadius: '50%',
                  backgroundColor: '#ffffff',
                  border: '1px solid #d4d4d8',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: '#3f3f46',
                  transition: 'all 0.2s ease',
                  boxShadow: '0 1px 2px rgba(0,0,0,0.05)',
                  flexShrink: 0
                }}
                className="hover:!bg-orange-500 hover:!text-white hover:!border-orange-500"
              >
                <svg style={{ width: '16px', height: '16px', minWidth: '16px', minHeight: '16px' }} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z"></path>
                </svg>
              </a>
            </div>

            {/* Payment Badges underneath socials: Visa, Mastercard, Stripe on crisp white cards */}
            <div
              className="mt-3 flex items-center gap-2"
              style={{
                marginTop: '0.75rem',
                display: 'flex',
                alignItems: 'center',
                gap: '8px',
              }}
            >
              {/* Visa Badge */}
              <div
                className="px-2.5 py-1 rounded-md bg-white border border-zinc-300 flex items-center gap-1.5 shadow-sm"
                style={{
                  padding: '4px 10px',
                  borderRadius: '6px',
                  backgroundColor: '#ffffff',
                  border: '1px solid #d4d4d8',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '5px',
                  boxShadow: '0 1px 2px 0 rgba(0, 0, 0, 0.05)',
                }}
              >
                <svg className="h-3 w-auto" viewBox="0 0 48 16" fill="none" style={{ height: '12px', width: 'auto' }}>
                  <path
                    d="M19.12 1.05L13.1 14.86H9.17L5.59 4.1C5.37 3.25 5.18 2.94 4.54 2.58C3.48 2.01 1.63 1.48 0 1.13L0.09 0.72H6.96C7.86 0.72 8.65 1.31 8.85 2.34L10.53 11.23L14.73 0.72L19.12 1.05ZM36.03 10.37C36.05 6.42 30.56 6.2 30.6 4.43C30.61 3.89 31.13 3.32 32.27 3.17C32.84 3.1 34.39 3.04 36.08 3.82L36.83 0.33C35.81 -0.04 34.49 -0.32 32.84 -0.32C28.84 -0.32 26.02 1.8 26 4.84C25.95 7.09 27.97 8.35 29.5 9.1C31.08 9.87 31.61 10.37 31.6 11.06C31.59 12.11 30.34 12.58 29.17 12.59C27.13 12.61 25.94 12.03 24.99 11.59L24.22 15.2C25.22 15.66 27.06 16.05 28.96 16.07C33.22 16.07 36.01 13.97 36.03 10.37ZM46.64 14.86H50.21L47.1 0.72H43.79C43.03 0.72 42.39 1.16 42.11 1.82L35.97 14.86H40.52L41.42 12.39H46.06L46.64 14.86ZM42.66 8.97L44.54 3.84L45.62 8.97H42.66ZM24.49 0.72L21.05 14.86H16.89L20.33 0.72H24.49Z"
                    fill="#2563EB"
                  />
                </svg>
                <span style={{ fontSize: '10px', fontWeight: 600, color: '#3f3f46' }}>Visa</span>
              </div>

              {/* Mastercard Badge */}
              <div
                className="px-2.5 py-1 rounded-md bg-white border border-zinc-300 flex items-center gap-1.5 shadow-sm"
                style={{
                  padding: '4px 10px',
                  borderRadius: '6px',
                  backgroundColor: '#ffffff',
                  border: '1px solid #d4d4d8',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '5px',
                  boxShadow: '0 1px 2px 0 rgba(0, 0, 0, 0.05)',
                }}
              >
                <svg className="h-3 w-4" viewBox="0 0 32 20" fill="none" style={{ height: '12px', width: '16px' }}>
                  <circle cx="10" cy="10" r="10" fill="#EB001B" />
                  <circle cx="22" cy="10" r="10" fill="#F79E1B" fillOpacity="0.88" />
                </svg>
                <span style={{ fontSize: '10px', fontWeight: 600, color: '#3f3f46' }}>Mastercard</span>
              </div>

              {/* Stripe Badge */}
              <div
                className="px-2.5 py-1 rounded-md bg-white border border-zinc-300 flex items-center gap-1.5 shadow-sm"
                style={{
                  padding: '4px 10px',
                  borderRadius: '6px',
                  backgroundColor: '#ffffff',
                  border: '1px solid #d4d4d8',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '5px',
                  boxShadow: '0 1px 2px 0 rgba(0, 0, 0, 0.05)',
                }}
              >
                <svg className="h-3 w-auto" viewBox="0 0 36 16" fill="none" style={{ height: '12px', width: 'auto' }}>
                  <path
                    d="M34.7 6.1C34.4 4.5 32.9 3.9 31.4 3.9C28.9 3.9 27.2 5.5 27.2 8C27.2 10.6 28.9 12.1 31.4 12.1C32.9 12.1 34.3 11.5 34.8 9.9H32.6C32.4 10.4 31.9 10.6 31.3 10.6C30.4 10.6 29.7 10 29.6 9.1H35V8.5C35 7.6 34.9 6.8 34.7 6.1ZM29.6 7.4C29.7 6.6 30.3 5.9 31.3 5.9C32 5.9 32.5 6.4 32.6 7.4H29.6ZM23.4 4.1H21V12H23.4V4.1ZM22.2 0.7C21.4 0.7 20.8 1.3 20.8 2.1C20.8 2.9 21.4 3.5 22.2 3.5C23 3.5 23.6 2.9 23.6 2.1C23.6 1.3 23 0.7 22.2 0.7ZM16.3 5.8C16.3 5.2 15.8 4.7 15.1 4.7C14.2 4.7 13.5 5.6 13.5 6.6V12H11.1V4.1H13.4V5.2C13.9 4.4 14.8 3.9 15.7 3.9C16.1 3.9 16.6 4 16.9 4.2L16.3 5.8ZM10.5 5.9H9.4V8.7C9.4 9.4 9.8 9.7 10.4 9.7C10.7 9.7 11 9.6 11.2 9.5L11.5 11C11.1 11.2 10.4 11.4 9.7 11.4C8.1 11.4 7.2 10.5 7.2 8.9V5.9H6V4.1H7.2V2.4L9.4 1.8V4.1H10.5V5.9ZM4.7 6C3.4 5.6 2.9 5.3 2.9 4.8C2.9 4.3 3.4 4 4.2 4C5.1 4 5.9 4.3 6.6 4.7L7.3 3.2C6.4 2.7 5.4 2.4 4.3 2.4C2.1 2.4 0.8 3.5 0.8 5C0.8 7.3 3.8 7.3 4.1 8C4.1 8.5 3.5 8.9 2.6 8.9C1.6 8.9 0.7 8.5 0 8L-0.8 9.6C0.1 10.2 1.3 10.6 2.6 10.6C5 10.6 6.3 9.4 6.3 7.8C6.3 5.5 3.3 5.5 4.7 6Z"
                    fill="#635BFF"
                  />
                </svg>
                <span style={{ fontSize: '10px', fontWeight: 600, color: '#3f3f46' }}>Stripe</span>
              </div>
            </div>
          </div>

          {/* Right Container: 3 Navigation & Contact Columns (7 cols on desktop) */}
          <div className="col-span-1 md:col-span-1 lg:col-span-7 grid grid-cols-1 sm:grid-cols-3 gap-6 ep-footer-nav text-left">
            {/* Column 2: Helpful Links */}
            <div className="text-left">
              <h4
                className="text-zinc-900 font-bold text-xs tracking-wider uppercase mb-3 text-left"
                style={{
                  color: '#18181b',
                  fontWeight: 700,
                  fontSize: '0.75rem',
                  letterSpacing: '0.05em',
                  textTransform: 'uppercase',
                  marginBottom: '0.75rem',
                  textAlign: 'left',
                }}
              >
                Helpful Links
              </h4>
              <ul className="space-y-1.5 text-xs" style={{ listStyle: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: '0.375rem', textAlign: 'left' }}>
                {helpfulLinks.map((link) => (
                  <li key={link.label}>
                    <Link
                      to={link.href}
                      className="text-zinc-700 hover:text-orange-600 transition-colors text-xs font-medium block text-decoration-none"
                      style={{
                        color: '#3f3f46',
                        fontSize: '0.75rem',
                        fontWeight: 500,
                        textDecoration: 'none',
                        transition: 'color 150ms ease',
                      }}
                      onMouseEnter={(e) => (e.currentTarget.style.color = '#ea580c')}
                      onMouseLeave={(e) => (e.currentTarget.style.color = '#3f3f46')}
                    >
                      {link.label}
                    </Link>
                  </li>
                ))}
              </ul>
            </div>

            {/* Column 3: About Us */}
            <div className="text-left">
              <h4
                className="text-zinc-900 font-bold text-xs tracking-wider uppercase mb-3 text-left"
                style={{
                  color: '#18181b',
                  fontWeight: 700,
                  fontSize: '0.75rem',
                  letterSpacing: '0.05em',
                  textTransform: 'uppercase',
                  marginBottom: '0.75rem',
                  textAlign: 'left',
                }}
              >
                About Us
              </h4>
              <ul className="space-y-1.5 text-xs" style={{ listStyle: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: '0.375rem', textAlign: 'left' }}>
                {aboutUsLinks.map((link) => (
                  <li key={link.label}>
                    <Link
                      to={link.href}
                      className="text-zinc-700 hover:text-orange-600 transition-colors text-xs font-medium block text-decoration-none"
                      style={{
                        color: '#3f3f46',
                        fontSize: '0.75rem',
                        fontWeight: 500,
                        textDecoration: 'none',
                        transition: 'color 150ms ease',
                      }}
                      onMouseEnter={(e) => (e.currentTarget.style.color = '#ea580c')}
                      onMouseLeave={(e) => (e.currentTarget.style.color = '#3f3f46')}
                    >
                      {link.label}
                    </Link>
                  </li>
                ))}
              </ul>
            </div>

            {/* Column 4: Contact (Strictly Left-Aligned) */}
            <div className="text-left flex flex-col items-start">
              <h4
                className="text-zinc-900 font-bold text-xs tracking-wider uppercase mb-3 text-left w-full"
                style={{
                  color: '#18181b',
                  fontWeight: 700,
                  fontSize: '0.75rem',
                  letterSpacing: '0.05em',
                  textTransform: 'uppercase',
                  marginBottom: '0.75rem',
                  textAlign: 'left',
                }}
              >
                Contact
              </h4>

              <div className="space-y-2 text-xs w-full flex flex-col items-start text-left" style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: '0.5rem', textAlign: 'left' }}>
                {/* WhatsApp (Text-only) - Flush Left */}
                <a
                  href="https://wa.me/94771234567"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="flex items-center gap-2 text-xs font-medium text-zinc-700 hover:text-orange-600 transition-colors text-decoration-none"
                  style={{
                    color: '#3f3f46',
                    fontSize: '0.75rem',
                    fontWeight: 500,
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                    textDecoration: 'none',
                    transition: 'color 150ms ease',
                  }}
                  onMouseEnter={(e) => (e.currentTarget.style.color = '#ea580c')}
                  onMouseLeave={(e) => (e.currentTarget.style.color = '#3f3f46')}
                >
                  <MessageCircle className="w-3.5 h-3.5 text-emerald-700 shrink-0" style={{ width: '14px', height: '14px', color: '#047857', flexShrink: 0 }} />
                  <span>+94 77 123 4567 (Text only)</span>
                </a>

                {/* Email - Flush Left */}
                <a
                  href="mailto:support@eventpulse.com"
                  className="flex items-center gap-2 text-xs font-medium text-zinc-700 hover:text-orange-600 transition-colors text-decoration-none"
                  style={{
                    color: '#3f3f46',
                    fontSize: '0.75rem',
                    fontWeight: 500,
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                    textDecoration: 'none',
                    transition: 'color 150ms ease',
                  }}
                  onMouseEnter={(e) => (e.currentTarget.style.color = '#ea580c')}
                  onMouseLeave={(e) => (e.currentTarget.style.color = '#3f3f46')}
                >
                  <Mail className="w-3.5 h-3.5 text-orange-600 shrink-0" style={{ width: '14px', height: '14px', color: '#ea580c', flexShrink: 0 }} />
                  <span>support@eventpulse.com</span>
                </a>

                {/* Compact Verified / Trust Seal Badge - Flush Left with White Card */}
                <div
                  className="mt-2.5 p-2.5 rounded-lg bg-white border border-zinc-300 text-zinc-800 shadow-sm flex items-center gap-2"
                  style={{
                    marginTop: '0.625rem',
                    padding: '10px',
                    borderRadius: '8px',
                    backgroundColor: '#ffffff',
                    border: '1px solid #d4d4d8',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                    width: 'fit-content',
                    boxShadow: '0 1px 2px 0 rgba(0, 0, 0, 0.05)',
                  }}
                >
                  <ShieldCheck className="w-4 h-4 text-emerald-700 shrink-0" style={{ width: '15px', height: '15px', color: '#047857', flexShrink: 0 }} />
                  <div className="text-left" style={{ textAlign: 'left' }}>
                    <div style={{ fontSize: '10px', fontWeight: 700, color: '#27272a', letterSpacing: '0.02em', lineHeight: 1.2 }}>
                      100% Verified Tickets
                    </div>
                    <div style={{ fontSize: '9px', fontWeight: 500, color: '#52525b', lineHeight: 1.2 }}>
                      Instant digital guarantee
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Sub-Footer Legal Bar & Copyright */}
        <div
          className="mt-6 pt-4 border-t border-zinc-400/60 flex flex-col sm:flex-row items-center justify-between gap-3 text-xs text-zinc-600 font-medium"
          style={{
            marginTop: '1.5rem',
            paddingTop: '1rem',
            borderTop: '1px solid rgba(161, 161, 170, 0.6)',
            display: 'flex',
            flexWrap: 'wrap',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: '0.75rem',
            fontSize: '0.75rem',
            color: '#52525b',
          }}
        >
          {/* Left Side: Legal Links */}
          <div
            className="flex items-center space-x-4"
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: '1rem',
            }}
          >
            <Link
              to="/privacy"
              className="text-zinc-600 hover:text-zinc-900 transition-colors text-xs font-medium text-decoration-none"
              style={{
                color: '#52525b',
                fontSize: '0.75rem',
                fontWeight: 500,
                textDecoration: 'none',
                transition: 'color 150ms ease',
              }}
              onMouseEnter={(e) => (e.currentTarget.style.color = '#18181b')}
              onMouseLeave={(e) => (e.currentTarget.style.color = '#52525b')}
            >
              Privacy Policy
            </Link>
            <Link
              to="/cookies"
              className="text-zinc-600 hover:text-zinc-900 transition-colors text-xs font-medium text-decoration-none"
              style={{
                color: '#52525b',
                fontSize: '0.75rem',
                fontWeight: 500,
                textDecoration: 'none',
                transition: 'color 150ms ease',
              }}
              onMouseEnter={(e) => (e.currentTarget.style.color = '#18181b')}
              onMouseLeave={(e) => (e.currentTarget.style.color = '#52525b')}
            >
              Cookie Policy
            </Link>
            <Link
              to="/terms"
              className="text-zinc-600 hover:text-zinc-900 transition-colors text-xs font-medium text-decoration-none"
              style={{
                color: '#52525b',
                fontSize: '0.75rem',
                fontWeight: 500,
                textDecoration: 'none',
                transition: 'color 150ms ease',
              }}
              onMouseEnter={(e) => (e.currentTarget.style.color = '#18181b')}
              onMouseLeave={(e) => (e.currentTarget.style.color = '#52525b')}
            >
              Terms and Conditions
            </Link>
          </div>

          {/* Right Side: Copyright */}
          <div
            className="text-zinc-600 text-xs font-medium text-center sm:text-right"
            style={{
              fontSize: '0.75rem',
              fontWeight: 500,
              color: '#52525b',
              textAlign: 'center',
            }}
          >
            Copyright 2026 © EventPulse All Rights Reserved
          </div>
        </div>
      </div>
    </footer>
  );
}

export default Footer;
