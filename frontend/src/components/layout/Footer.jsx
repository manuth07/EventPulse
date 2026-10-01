import React from 'react';
import { Link } from 'react-router-dom';
import { Ticket, Activity, Mail, MessageCircle, Award } from 'lucide-react';

const socialLinks = [
  {
    name: 'Facebook',
    href: 'https://facebook.com',
    icon: (
      <svg className="w-3.5 h-3.5 fill-current" viewBox="0 0 24 24" aria-hidden="true">
        <path d="M24 12.073c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.99 4.388 10.954 10.125 11.854v-8.385H7.078v-3.47h3.047V9.43c0-3.007 1.792-4.669 4.533-4.669 1.312 0 2.686.235 2.686.235v2.953H15.83c-1.491 0-1.956.925-1.956 1.874v2.25h3.328l-.532 3.47h-2.796v8.385C19.612 23.027 24 18.062 24 12.073z" />
      </svg>
    ),
  },
  {
    name: 'Instagram',
    href: 'https://instagram.com',
    icon: (
      <svg className="w-3.5 h-3.5 fill-current" viewBox="0 0 24 24" aria-hidden="true">
        <path d="M12 2.163c3.204 0 3.584.012 4.85.07 3.252.148 4.771 1.691 4.919 4.919.058 1.265.069 1.645.069 4.849 0 3.205-.012 3.584-.069 4.849-.149 3.225-1.664 4.771-4.919 4.919-1.266.058-1.644.07-4.85.07-3.204 0-3.584-.012-4.849-.07-3.26-.149-4.771-1.699-4.919-4.92-.058-1.265-.07-1.644-.07-4.849 0-3.204.013-3.583.07-4.849.149-3.227 1.664-4.771 4.919-4.919 1.266-.057 1.645-.069 4.849-.069zm0-2.163c-3.259 0-3.667.014-4.947.072-4.358.2-6.78 2.618-6.98 6.98-.059 1.281-.073 1.689-.073 4.948 0 3.259.014 3.668.072 4.948.2 4.358 2.618 6.78 6.98 6.98 1.281.058 1.689.072 4.948.072 3.259 0 3.668-.014 4.948-.072 4.354-.2 6.782-2.618 6.979-6.98.059-1.28.073-1.689.073-4.948 0-3.259-.014-3.667-.072-4.947-.196-4.354-2.617-6.78-6.979-6.98-1.281-.059-1.69-.073-4.949-.073zm0 5.838c-3.403 0-6.162 2.759-6.162 6.162s2.759 6.163 6.162 6.163 6.162-2.759 6.162-6.163c0-3.403-2.759-6.162-6.162-6.162zm0 10.162c-2.209 0-4-1.79-4-4 0-2.209 1.791-4 4-4s4 1.791 4 4c0 2.21-1.791 4-4 4zm6.406-11.845c-.796 0-1.441.645-1.441 1.44s.645 1.44 1.441 1.44c.795 0 1.439-.645 1.439-1.44s-.644-1.44-1.439-1.44z" />
      </svg>
    ),
  },
  {
    name: 'X (Twitter)',
    href: 'https://x.com',
    icon: (
      <svg className="w-3 h-3 fill-current" viewBox="0 0 24 24" aria-hidden="true">
        <path d="M18.244 2.25h3.308l-7.227 8.26 8.502 11.24H16.17l-5.214-6.817L4.99 21.75H1.68l7.73-8.835L1.254 2.25H8.08l4.713 6.231zm-1.161 17.52h1.833L7.084 4.126H5.117z" />
      </svg>
    ),
  },
  {
    name: 'LinkedIn',
    href: 'https://linkedin.com',
    icon: (
      <svg className="w-3.5 h-3.5 fill-current" viewBox="0 0 24 24" aria-hidden="true">
        <path d="M19 0h-14c-2.761 0-5 2.239-5 5v14c0 2.761 2.239 5 5 5h14c2.762 0 5-2.239 5-5v-14c0-2.761-2.238-5-5-5zm-11 19h-3v-11h3v11zm-1.5-12.268c-.966 0-1.75-.79-1.75-1.764s.784-1.764 1.75-1.764 1.75.79 1.75 1.764-.783 1.764-1.75 1.764zm13.5 12.268h-3v-5.604c0-3.368-4-3.113-4 0v5.604h-3v-11h3v1.765c1.396-2.586 7-2.777 7 2.476v6.759z" />
      </svg>
    ),
  },
  {
    name: 'TikTok',
    href: 'https://tiktok.com',
    icon: (
      <svg className="w-3.5 h-3.5 fill-current" viewBox="0 0 24 24" aria-hidden="true">
        <path d="M12.525.02c1.31-.02 2.61-.01 3.91-.02.08 1.53.63 3.09 1.75 4.17 1.12 1.11 2.7 1.62 4.24 1.79v4.03c-1.44-.05-2.89-.35-4.2-.97-.57-.26-1.1-.59-1.62-.97-.01 2.92.01 5.84-.02 8.75-.08 1.4-.54 2.79-1.35 3.94-1.31 1.92-3.58 3.17-5.91 3.21-1.43.08-2.86-.31-4.08-1.03-2.02-1.19-3.44-3.37-3.65-5.71-.02-.5-.03-1-.01-1.49.18-1.9 1.12-3.72 2.58-4.96 1.66-1.44 3.98-2.13 6.15-1.72.02 1.48-.04 2.96-.04 4.44-.99-.32-2.15-.23-3.02.37-.63.41-1.11 1.04-1.36 1.75-.21.51-.24 1.07-.14 1.61.24 1.64 1.82 3.02 3.5 2.87 1.12-.01 2.19-.66 2.77-1.61.19-.33.4-.67.41-1.06.1-1.79.06-3.57.07-5.36.01-4.03-.01-8.05.02-12.07z" />
      </svg>
    ),
  },
  {
    name: 'YouTube',
    href: 'https://youtube.com',
    icon: (
      <svg className="w-3.5 h-3.5 fill-current" viewBox="0 0 24 24" aria-hidden="true">
        <path d="M23.498 6.186a3.016 3.016 0 0 0-2.122-2.136C19.505 3.545 12 3.545 12 3.545s-7.505 0-9.377.505A3.017 3.017 0 0 0 .502 6.186C0 8.07 0 12 0 12s0 3.93.502 5.814a3.016 3.016 0 0 0 2.122 2.136c1.871.505 9.376.505 9.376.505s7.505 0 9.377-.505a3.015 3.015 0 0 0 2.122-2.136C24 15.93 24 12 24 12s0-3.93-.502-5.814zM9.545 15.568V8.432L15.818 12l-6.273 3.568z" />
      </svg>
    ),
  },
  {
    name: 'WhatsApp',
    href: 'https://whatsapp.com',
    icon: (
      <svg className="w-3.5 h-3.5 fill-current" viewBox="0 0 24 24" aria-hidden="true">
        <path d="M.057 24l1.687-6.163c-1.041-1.804-1.588-3.849-1.587-5.946.003-6.556 5.338-11.891 11.893-11.891 3.181.001 6.167 1.24 8.413 3.488 2.245 2.248 3.481 5.236 3.48 8.414-.003 6.557-5.338 11.892-11.893 11.892-1.99-.001-3.951-.5-5.688-1.448l-6.305 1.654zm6.597-3.807c1.676.995 3.276 1.591 5.392 1.592 5.448 0 9.886-4.434 9.889-9.885.002-5.462-4.415-9.89-9.881-9.892-5.452 0-9.887 4.434-9.889 9.884-.001 2.225.651 3.891 1.746 5.634l-.999 3.648 3.742-.981zm11.387-5.464c-.074-.124-.272-.198-.57-.347-.297-.149-1.758-.868-2.031-.967-.272-.099-.47-.149-.669.149-.198.297-.768.967-.941 1.165-.173.198-.347.223-.644.074-.297-.149-1.255-.462-2.39-1.475-.883-.788-1.48-1.761-1.653-2.059-.173-.297-.018-.458.13-.606.134-.133.297-.347.446-.521.151-.172.2-.296.3-.495.099-.198.05-.372-.025-.521-.075-.148-.669-1.611-.916-2.206-.242-.579-.487-.501-.669-.51l-.57-.01c-.198 0-.52.074-.792.372s-1.04 1.016-1.04 2.479 1.065 2.876 1.213 3.074c.149.198 2.095 3.2 5.076 4.487.709.306 1.263.489 1.694.626.712.226 1.36.194 1.872.118.571-.085 1.758-.719 2.006-1.413.248-.695.248-1.29.173-1.414z" />
      </svg>
    ),
  },
];

const helpfulLinks = [
  { label: 'Events', href: '/' },
  { label: 'EventPulse Deals', href: '/deals' },
  { label: 'My Account', href: '/my-bookings' },
  { label: 'Refund Policy', href: '/refund-policy' },
];

const aboutUsLinks = [
  { label: 'Who We Are', href: '/about' },
  { label: 'For Organizers', href: '/list-your-event' },
  { label: 'Careers', href: '/careers' },
  { label: 'FAQ', href: '/faq' },
  { label: 'Contact Us', href: '/contact' },
];

export function Footer() {
  return (
    <footer
      className="bg-[#0a1128] border-t border-slate-800 text-slate-300"
      style={{
        backgroundColor: '#0a1128',
        borderTop: '1px solid #1e293b',
        color: '#cbd5e1',
      }}
    >
      <div
        className="py-8 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto"
        style={{
          maxWidth: '80rem',
          marginLeft: 'auto',
          marginRight: 'auto',
          paddingTop: '2rem',
          paddingBottom: '2rem',
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
          <div className="col-span-1 md:col-span-1 lg:col-span-5 ep-footer-brand">
            {/* EventPulse Logo */}
            <Link
              to="/"
              className="inline-flex items-center gap-2.5 text-2xl font-bold tracking-tight text-decoration-none group"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '10px',
                textDecoration: 'none',
              }}
            >
              <div
                className="w-9 h-9 rounded-xl bg-gradient-to-tr from-orange-600 to-orange-500 flex items-center justify-center shadow-md shadow-orange-500/20 text-white"
                style={{
                  width: '36px',
                  height: '36px',
                  borderRadius: '10px',
                  background: 'linear-gradient(135deg, #ea580c 0%, #f97316 100%)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: '#ffffff',
                  boxShadow: '0 4px 12px rgba(249, 115, 22, 0.25)',
                }}
              >
                <div style={{ position: 'relative', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                  <Ticket className="w-4 h-4 -rotate-12" style={{ width: '18px', height: '18px', transform: 'rotate(-12deg)' }} />
                  <Activity
                    className="w-2.5 h-2.5 absolute text-white"
                    style={{
                      width: '9px',
                      height: '9px',
                      position: 'absolute',
                      top: '4px',
                      right: '-3px',
                      color: '#ffffff',
                    }}
                  />
                </div>
              </div>

              <span
                className="text-2xl font-extrabold tracking-tight"
                style={{
                  fontFamily: 'var(--ep-font-heading, "Plus Jakarta Sans", sans-serif)',
                  fontSize: '22px',
                  fontWeight: 800,
                  letterSpacing: '-0.02em',
                }}
              >
                <span className="text-white" style={{ color: '#ffffff' }}>
                  Event
                </span>
                <span className="text-orange-500" style={{ color: '#f97316' }}>
                  Pulse
                </span>
              </span>
            </Link>

            {/* Concise Brand Description (max 3 lines) */}
            <p
              className="text-sm text-slate-400 mt-3 leading-relaxed"
              style={{
                color: '#94a3b8',
                fontSize: '0.875rem',
                lineHeight: '1.6',
                marginTop: '0.75rem',
                marginBottom: '0',
                maxWidth: '440px',
              }}
            >
              EventPulse is Sri Lanka's premier online ticket marketplace, providing a secure and safe platform for discovering and booking live entertainment, concerts, and tech events.
            </p>

            {/* Social Icons row (7 icons) */}
            <div
              className="mt-4 flex flex-wrap gap-2.5 items-center"
              style={{
                marginTop: '1rem',
                display: 'flex',
                flexWrap: 'wrap',
                gap: '10px',
                alignItems: 'center',
              }}
            >
              {socialLinks.map((item) => (
                <a
                  key={item.name}
                  href={item.href}
                  target="_blank"
                  rel="noopener noreferrer"
                  aria-label={item.name}
                  className="w-8 h-8 rounded-full bg-slate-800/80 hover:bg-orange-500 text-slate-300 hover:text-white transition flex items-center justify-center text-xs"
                  style={{
                    width: '32px',
                    height: '32px',
                    borderRadius: '9999px',
                    backgroundColor: 'rgba(30, 41, 59, 0.8)',
                    color: '#cbd5e1',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    textDecoration: 'none',
                    transition: 'all 150ms ease-in-out',
                    border: '1px solid rgba(51, 65, 85, 0.4)',
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.backgroundColor = '#f97316';
                    e.currentTarget.style.color = '#ffffff';
                    e.currentTarget.style.borderColor = '#f97316';
                    e.currentTarget.style.transform = 'translateY(-2px)';
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.backgroundColor = 'rgba(30, 41, 59, 0.8)';
                    e.currentTarget.style.color = '#cbd5e1';
                    e.currentTarget.style.borderColor = 'rgba(51, 65, 85, 0.4)';
                    e.currentTarget.style.transform = 'translateY(0)';
                  }}
                >
                  {item.icon}
                </a>
              ))}
            </div>

            {/* Payment Badges underneath socials */}
            <div
              className="mt-4 flex flex-wrap items-center gap-2"
              style={{
                marginTop: '1rem',
                display: 'flex',
                flexWrap: 'wrap',
                alignItems: 'center',
                gap: '8px',
              }}
            >
              {/* Visa Badge */}
              <div
                className="px-2.5 py-1 rounded bg-slate-900/90 border border-slate-800 flex items-center gap-1.5 shadow-sm"
                style={{
                  padding: '4px 10px',
                  borderRadius: '4px',
                  backgroundColor: 'rgba(15, 23, 42, 0.9)',
                  border: '1px solid #1e293b',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '6px',
                }}
              >
                <svg className="h-3.5 w-auto" viewBox="0 0 48 16" fill="none" style={{ height: '14px', width: 'auto' }}>
                  <path
                    d="M19.12 1.05L13.1 14.86H9.17L5.59 4.1C5.37 3.25 5.18 2.94 4.54 2.58C3.48 2.01 1.63 1.48 0 1.13L0.09 0.72H6.96C7.86 0.72 8.65 1.31 8.85 2.34L10.53 11.23L14.73 0.72L19.12 1.05ZM36.03 10.37C36.05 6.42 30.56 6.2 30.6 4.43C30.61 3.89 31.13 3.32 32.27 3.17C32.84 3.1 34.39 3.04 36.08 3.82L36.83 0.33C35.81 -0.04 34.49 -0.32 32.84 -0.32C28.84 -0.32 26.02 1.8 26 4.84C25.95 7.09 27.97 8.35 29.5 9.1C31.08 9.87 31.61 10.37 31.6 11.06C31.59 12.11 30.34 12.58 29.17 12.59C27.13 12.61 25.94 12.03 24.99 11.59L24.22 15.2C25.22 15.66 27.06 16.05 28.96 16.07C33.22 16.07 36.01 13.97 36.03 10.37ZM46.64 14.86H50.21L47.1 0.72H43.79C43.03 0.72 42.39 1.16 42.11 1.82L35.97 14.86H40.52L41.42 12.39H46.06L46.64 14.86ZM42.66 8.97L44.54 3.84L45.62 8.97H42.66ZM24.49 0.72L21.05 14.86H16.89L20.33 0.72H24.49Z"
                    fill="#2563EB"
                  />
                </svg>
                <span style={{ fontSize: '11px', fontWeight: 600, color: '#cbd5e1' }}>Visa</span>
              </div>

              {/* Mastercard Badge */}
              <div
                className="px-2.5 py-1 rounded bg-slate-900/90 border border-slate-800 flex items-center gap-1.5 shadow-sm"
                style={{
                  padding: '4px 10px',
                  borderRadius: '4px',
                  backgroundColor: 'rgba(15, 23, 42, 0.9)',
                  border: '1px solid #1e293b',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '6px',
                }}
              >
                <svg className="h-3.5 w-5" viewBox="0 0 32 20" fill="none" style={{ height: '14px', width: '20px' }}>
                  <circle cx="10" cy="10" r="10" fill="#EB001B" />
                  <circle cx="22" cy="10" r="10" fill="#F79E1B" fillOpacity="0.88" />
                </svg>
                <span style={{ fontSize: '11px', fontWeight: 600, color: '#cbd5e1' }}>Mastercard</span>
              </div>

              {/* Stripe Badge */}
              <div
                className="px-2.5 py-1 rounded bg-slate-900/90 border border-slate-800 flex items-center gap-1.5 shadow-sm"
                style={{
                  padding: '4px 10px',
                  borderRadius: '4px',
                  backgroundColor: 'rgba(15, 23, 42, 0.9)',
                  border: '1px solid #1e293b',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '6px',
                }}
              >
                <svg className="h-3.5 w-auto" viewBox="0 0 36 16" fill="none" style={{ height: '14px', width: 'auto' }}>
                  <path
                    d="M34.7 6.1C34.4 4.5 32.9 3.9 31.4 3.9C28.9 3.9 27.2 5.5 27.2 8C27.2 10.6 28.9 12.1 31.4 12.1C32.9 12.1 34.3 11.5 34.8 9.9H32.6C32.4 10.4 31.9 10.6 31.3 10.6C30.4 10.6 29.7 10 29.6 9.1H35V8.5C35 7.6 34.9 6.8 34.7 6.1ZM29.6 7.4C29.7 6.6 30.3 5.9 31.3 5.9C32 5.9 32.5 6.4 32.6 7.4H29.6ZM23.4 4.1H21V12H23.4V4.1ZM22.2 0.7C21.4 0.7 20.8 1.3 20.8 2.1C20.8 2.9 21.4 3.5 22.2 3.5C23 3.5 23.6 2.9 23.6 2.1C23.6 1.3 23 0.7 22.2 0.7ZM16.3 5.8C16.3 5.2 15.8 4.7 15.1 4.7C14.2 4.7 13.5 5.6 13.5 6.6V12H11.1V4.1H13.4V5.2C13.9 4.4 14.8 3.9 15.7 3.9C16.1 3.9 16.6 4 16.9 4.2L16.3 5.8ZM10.5 5.9H9.4V8.7C9.4 9.4 9.8 9.7 10.4 9.7C10.7 9.7 11 9.6 11.2 9.5L11.5 11C11.1 11.2 10.4 11.4 9.7 11.4C8.1 11.4 7.2 10.5 7.2 8.9V5.9H6V4.1H7.2V2.4L9.4 1.8V4.1H10.5V5.9ZM4.7 6C3.4 5.6 2.9 5.3 2.9 4.8C2.9 4.3 3.4 4 4.2 4C5.1 4 5.9 4.3 6.6 4.7L7.3 3.2C6.4 2.7 5.4 2.4 4.3 2.4C2.1 2.4 0.8 3.5 0.8 5C0.8 7.3 3.8 7.3 4.1 8C4.1 8.5 3.5 8.9 2.6 8.9C1.6 8.9 0.7 8.5 0 8L-0.8 9.6C0.1 10.2 1.3 10.6 2.6 10.6C5 10.6 6.3 9.4 6.3 7.8C6.3 5.5 3.3 5.5 4.7 6Z"
                    fill="#635BFF"
                  />
                </svg>
                <span style={{ fontSize: '11px', fontWeight: 600, color: '#cbd5e1' }}>Stripe</span>
              </div>

              {/* Koko Badge */}
              <div
                className="px-2.5 py-1 rounded bg-slate-900/90 border border-slate-800 flex items-center gap-1 shadow-sm"
                style={{
                  padding: '4px 10px',
                  borderRadius: '4px',
                  backgroundColor: 'rgba(15, 23, 42, 0.9)',
                  border: '1px solid #1e293b',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '4px',
                }}
              >
                <span style={{ fontSize: '11px', fontWeight: 800, color: '#38bdf8', letterSpacing: '-0.03em' }}>koko</span>
                <span style={{ fontSize: '9px', fontWeight: 700, backgroundColor: '#0284c7', color: '#ffffff', padding: '1px 3px', borderRadius: '2px' }}>pay</span>
              </div>
            </div>
          </div>

          {/* Right Container: 3 Navigation & Contact Columns (7 cols on desktop) */}
          <div className="col-span-1 md:col-span-1 lg:col-span-7 grid grid-cols-1 sm:grid-cols-3 gap-6 ep-footer-nav">
            {/* Column 2: Helpful Links */}
            <div>
              <h4
                className="text-white font-semibold text-sm mb-3.5"
                style={{
                  color: '#ffffff',
                  fontWeight: 600,
                  fontSize: '0.875rem',
                  marginBottom: '0.875rem',
                }}
              >
                Helpful Links
              </h4>
              <ul className="space-y-2 text-sm text-slate-400" style={{ listStyle: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                {helpfulLinks.map((link) => (
                  <li key={link.label}>
                    <Link
                      to={link.href}
                      className="hover:text-orange-400 transition-colors block text-decoration-none"
                      style={{
                        color: '#94a3b8',
                        textDecoration: 'none',
                        transition: 'color 150ms ease',
                      }}
                      onMouseEnter={(e) => (e.currentTarget.style.color = '#fb923c')}
                      onMouseLeave={(e) => (e.currentTarget.style.color = '#94a3b8')}
                    >
                      {link.label}
                    </Link>
                  </li>
                ))}
              </ul>
            </div>

            {/* Column 3: About Us */}
            <div>
              <h4
                className="text-white font-semibold text-sm mb-3.5"
                style={{
                  color: '#ffffff',
                  fontWeight: 600,
                  fontSize: '0.875rem',
                  marginBottom: '0.875rem',
                }}
              >
                About Us
              </h4>
              <ul className="space-y-2 text-sm text-slate-400" style={{ listStyle: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                {aboutUsLinks.map((link) => (
                  <li key={link.label}>
                    <Link
                      to={link.href}
                      className="hover:text-orange-400 transition-colors block text-decoration-none"
                      style={{
                        color: '#94a3b8',
                        textDecoration: 'none',
                        transition: 'color 150ms ease',
                      }}
                      onMouseEnter={(e) => (e.currentTarget.style.color = '#fb923c')}
                      onMouseLeave={(e) => (e.currentTarget.style.color = '#94a3b8')}
                    >
                      {link.label}
                    </Link>
                  </li>
                ))}
              </ul>
            </div>

            {/* Column 4: Contact */}
            <div>
              <h4
                className="text-white font-semibold text-sm mb-3.5"
                style={{
                  color: '#ffffff',
                  fontWeight: 600,
                  fontSize: '0.875rem',
                  marginBottom: '0.875rem',
                }}
              >
                Contact
              </h4>

              <div className="space-y-2.5" style={{ display: 'flex', flexDirection: 'column', gap: '0.625rem' }}>
                {/* WhatsApp (Text-only) */}
                <a
                  href="https://wa.me/94771234567"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="flex items-center gap-2 text-xs text-slate-300 hover:text-orange-400 transition-colors text-decoration-none"
                  style={{
                    color: '#cbd5e1',
                    fontSize: '0.75rem',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                    textDecoration: 'none',
                    transition: 'color 150ms ease',
                  }}
                  onMouseEnter={(e) => (e.currentTarget.style.color = '#fb923c')}
                  onMouseLeave={(e) => (e.currentTarget.style.color = '#cbd5e1')}
                >
                  <MessageCircle className="w-3.5 h-3.5 text-emerald-400 shrink-0" style={{ width: '14px', height: '14px', color: '#34d399', flexShrink: 0 }} />
                  <span>WhatsApp: +94 77 123 4567 (Text only)</span>
                </a>

                {/* Email */}
                <a
                  href="mailto:support@eventpulse.com"
                  className="flex items-center gap-2 text-xs text-slate-300 hover:text-orange-400 transition-colors text-decoration-none"
                  style={{
                    color: '#cbd5e1',
                    fontSize: '0.75rem',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                    textDecoration: 'none',
                    transition: 'color 150ms ease',
                  }}
                  onMouseEnter={(e) => (e.currentTarget.style.color = '#fb923c')}
                  onMouseLeave={(e) => (e.currentTarget.style.color = '#cbd5e1')}
                >
                  <Mail className="w-3.5 h-3.5 text-orange-400 shrink-0" style={{ width: '14px', height: '14px', color: '#fb923c', flexShrink: 0 }} />
                  <span>support@eventpulse.com</span>
                </a>

                {/* Trust / Gold Verified Badge */}
                <div
                  className="mt-3.5 p-2.5 rounded-lg bg-gradient-to-br from-amber-500/10 via-slate-900 to-amber-950/20 border border-amber-500/30 flex items-center gap-2.5 shadow-sm"
                  style={{
                    marginTop: '0.75rem',
                    padding: '8px 10px',
                    borderRadius: '8px',
                    background: 'linear-gradient(135deg, rgba(245, 158, 11, 0.12) 0%, rgba(15, 23, 42, 0.9) 100%)',
                    border: '1px solid rgba(245, 158, 11, 0.35)',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                  }}
                >
                  <div
                    style={{
                      width: '28px',
                      height: '28px',
                      borderRadius: '9999px',
                      background: 'linear-gradient(135deg, #f59e0b 0%, #d97706 100%)',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      color: '#ffffff',
                      flexShrink: 0,
                      boxShadow: '0 2px 6px rgba(245, 158, 11, 0.3)',
                    }}
                  >
                    <Award className="w-4 h-4 text-white" style={{ width: '16px', height: '16px' }} />
                  </div>
                  <div>
                    <div style={{ fontSize: '11px', fontWeight: 700, color: '#fcd34d', letterSpacing: '0.02em', lineHeight: 1.2 }}>
                      VERIFIED TICKETS
                    </div>
                    <div style={{ fontSize: '10px', color: '#94a3b8', lineHeight: 1.2 }}>
                      100% Guaranteed Entry
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Secondary Slim Legal Bottom Bar */}
        <div
          className="mt-8 pt-6 border-t border-slate-800/80 flex flex-col sm:flex-row items-center justify-between gap-3 text-xs text-slate-500"
          style={{
            marginTop: '2rem',
            paddingTop: '1.5rem',
            borderTop: '1px solid rgba(30, 41, 59, 0.8)',
            display: 'flex',
            flexWrap: 'wrap',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: '0.75rem',
            fontSize: '0.75rem',
            color: '#64748b',
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
              className="hover:text-slate-300 transition-colors text-decoration-none"
              style={{
                color: '#64748b',
                textDecoration: 'none',
                transition: 'color 150ms ease',
              }}
              onMouseEnter={(e) => (e.currentTarget.style.color = '#cbd5e1')}
              onMouseLeave={(e) => (e.currentTarget.style.color = '#64748b')}
            >
              Privacy Policy
            </Link>
            <Link
              to="/cookies"
              className="hover:text-slate-300 transition-colors text-decoration-none"
              style={{
                color: '#64748b',
                textDecoration: 'none',
                transition: 'color 150ms ease',
              }}
              onMouseEnter={(e) => (e.currentTarget.style.color = '#cbd5e1')}
              onMouseLeave={(e) => (e.currentTarget.style.color = '#64748b')}
            >
              Cookie Policy
            </Link>
            <Link
              to="/terms"
              className="hover:text-slate-300 transition-colors text-decoration-none"
              style={{
                color: '#64748b',
                textDecoration: 'none',
                transition: 'color 150ms ease',
              }}
              onMouseEnter={(e) => (e.currentTarget.style.color = '#cbd5e1')}
              onMouseLeave={(e) => (e.currentTarget.style.color = '#64748b')}
            >
              Terms and Conditions
            </Link>
          </div>

          {/* Right Side: Copyright */}
          <div
            className="text-center sm:text-right"
            style={{
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
