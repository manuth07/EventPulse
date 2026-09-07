import React from 'react';

export function EventSkeleton() {
  return (
    <div
      className="ep-card"
      style={{
        maxWidth: '400px',
        width: '100%',
        margin: '0 auto',
        backgroundColor: '#FFFFFF',
        borderRadius: '18px',
        overflow: 'hidden',
        border: '1px solid #E5E7EB',
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
      }}
    >
      <div className="ep-skeleton" style={{ height: '290px', width: '100%', flexShrink: 0 }} />
      <div style={{ padding: '20px 22px 22px 22px', display: 'flex', flexDirection: 'column', flex: 1, gap: '14px' }}>
        <div className="ep-skeleton" style={{ height: '22px', width: '85%' }} />
        <div className="ep-skeleton" style={{ height: '16px', width: '45%' }} />
        <div style={{ display: 'flex', gap: '8px', margin: '6px 0 10px 0' }}>
          <div className="ep-skeleton" style={{ height: '26px', width: '130px', borderRadius: '9999px' }} />
          <div className="ep-skeleton" style={{ height: '26px', width: '26px', borderRadius: '50%' }} />
        </div>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 'auto' }}>
          <div className="ep-skeleton" style={{ height: '32px', width: '90px' }} />
          <div className="ep-skeleton" style={{ height: '32px', width: '110px' }} />
        </div>
      </div>
    </div>
  );
}
