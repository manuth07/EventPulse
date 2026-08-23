import React from 'react';

export function EventSkeleton() {
  return (
    <div className="ep-card h-100" style={{ minHeight: '340px' }}>
      <div className="ep-skeleton" style={{ height: '140px', borderRadius: '16px 16px 0 0' }} />
      <div style={{ padding: '20px', display: 'flex', flexDirection: 'column', flex: 1, gap: '12px' }}>
        <div className="ep-skeleton" style={{ height: '24px', width: '80%' }} />
        <div className="ep-skeleton" style={{ height: '16px', width: '50%' }} />
        <div className="ep-skeleton" style={{ height: '40px', width: '100%', marginTop: '8px' }} />
        <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 'auto', paddingTop: '16px' }}>
          <div className="ep-skeleton" style={{ height: '24px', width: '70px' }} />
          <div className="ep-skeleton" style={{ height: '32px', width: '100px' }} />
        </div>
      </div>
    </div>
  );
}
