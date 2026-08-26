import React from 'react';
import { AlertTriangle, RotateCcw } from 'lucide-react';

export function ErrorState({ message, onRetry }) {
  return (
    <div style={{
      textAlign: 'center',
      padding: '48px 24px',
      backgroundColor: '#ffffff',
      borderRadius: 'var(--ep-radius-card)',
      border: '1px solid var(--ep-border)',
      maxWidth: '520px',
      margin: '0 auto'
    }}>
      <div style={{
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        marginBottom: '12px'
      }}>
        <AlertTriangle size={36} color="var(--ep-danger)" />
      </div>

      <h3 className="ep-h3 mb-2" style={{ color: 'var(--ep-danger)' }}>
        We couldn't load events right now
      </h3>
      <p className="ep-body mb-4">
        {message || 'Please check your network connection or backend services and try again.'}
      </p>
      {onRetry && (
        <button type="button" className="ep-btn-primary" onClick={onRetry}>
          <RotateCcw size={15} style={{ marginRight: '6px' }} />
          Retry Loading
        </button>
      )}
    </div>
  );
}
