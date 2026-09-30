import React, { useState, useRef, useEffect, useCallback } from 'react';
import {
  X,
  Check,
  ZoomIn,
  ZoomOut,
  RotateCcw,
  Crop as CropIcon,
  Loader2,
  Move,
} from 'lucide-react';
import { getCroppedImg } from '../../utils/cropImage';

/**
 * Interactive Image Cropper Modal for Event Posters (4:5) and Cover Banners (16:6 / 1920:720).
 * Supports drag-to-pan, slider & mouse-wheel zooming, rule-of-thirds grid, and canvas File export.
 *
 * @param {object} props
 * @param {boolean} props.isOpen - Whether the modal is visible.
 * @param {string} props.imageSrc - Raw image URL / object URL to be cropped.
 * @param {string} [props.fileName='image.jpg'] - Original file name to retain on exported File.
 * @param {string} [props.fileType='image/jpeg'] - Original file MIME type.
 * @param {number} [props.aspectRatio=4/5] - Target aspect ratio (width / height).
 * @param {string} [props.aspectTitle='Image'] - Display label (e.g. "Event Poster (4:5)" or "Cover Banner (16:6)").
 * @param {number} [props.targetWidth] - Optional target width for exported canvas (e.g. 1920 for banners).
 * @param {number} [props.targetHeight] - Optional target height for exported canvas (e.g. 720 for banners).
 * @param {(croppedFile: File, previewUrl: string) => void} props.onCropComplete - Callback with standard File and preview URL.
 * @param {() => void} props.onCancel - Callback when user cancels.
 */
export function ImageCropperModal({
  isOpen,
  imageSrc,
  fileName = 'image.jpg',
  fileType = 'image/jpeg',
  aspectRatio = 4 / 5,
  aspectTitle = 'Crop Image',
  targetWidth,
  targetHeight,
  onCropComplete,
  onCancel,
}) {
  const [zoom, setZoom] = useState(1);
  const [pan, setPan] = useState({ x: 0, y: 0 });
  const [isDragging, setIsDragging] = useState(false);
  const [isProcessing, setIsProcessing] = useState(false);
  const [imageMeta, setImageMeta] = useState({ naturalWidth: 0, naturalHeight: 0, loaded: false });

  const containerRef = useRef(null);
  const dragStartRef = useRef({ mouseX: 0, mouseY: 0, panX: 0, panY: 0 });

  // Viewport & crop-box size configuration
  const VIEWPORT_MAX_WIDTH = 640;
  const VIEWPORT_MAX_HEIGHT = 420;

  // Calculate crop box dimensions locked to aspect ratio
  const cropBox = (() => {
    let width = VIEWPORT_MAX_WIDTH;
    let height = width / aspectRatio;

    if (height > VIEWPORT_MAX_HEIGHT) {
      height = VIEWPORT_MAX_HEIGHT;
      width = height * aspectRatio;
    }

    return {
      width: Math.round(width),
      height: Math.round(height),
    };
  })();

  // Load natural image dimensions when imageSrc changes
  useEffect(() => {
    if (!imageSrc || !isOpen) {
      setImageMeta({ naturalWidth: 0, naturalHeight: 0, loaded: false });
      setZoom(1);
      setPan({ x: 0, y: 0 });
      return;
    }

    const img = new Image();
    img.onload = () => {
      setImageMeta({
        naturalWidth: img.naturalWidth,
        naturalHeight: img.naturalHeight,
        loaded: true,
      });
      setZoom(1);
      setPan({ x: 0, y: 0 });
    };
    img.src = imageSrc;
  }, [imageSrc, isOpen]);

  // Compute maximum allowable pan offset based on current zoom
  const getPanBounds = useCallback(
    (currentZoom) => {
      if (!imageMeta.loaded) return { maxPanX: 0, maxPanY: 0, scale: 1 };

      const baseScale = Math.max(
        cropBox.width / imageMeta.naturalWidth,
        cropBox.height / imageMeta.naturalHeight
      );

      const effectiveScale = baseScale * currentZoom;
      const renderedWidth = imageMeta.naturalWidth * effectiveScale;
      const renderedHeight = imageMeta.naturalHeight * effectiveScale;

      const maxPanX = Math.max(0, (renderedWidth - cropBox.width) / 2);
      const maxPanY = Math.max(0, (renderedHeight - cropBox.height) / 2);

      return { maxPanX, maxPanY, scale: effectiveScale };
    },
    [cropBox.width, cropBox.height, imageMeta]
  );

  // Clamp pan when zoom updates
  const handleZoomChange = (newZoom) => {
    const clampedZoom = Math.min(3, Math.max(1, newZoom));
    setZoom(clampedZoom);

    const { maxPanX, maxPanY } = getPanBounds(clampedZoom);
    setPan((prev) => ({
      x: Math.max(-maxPanX, Math.min(maxPanX, prev.x)),
      y: Math.max(-maxPanY, Math.min(maxPanY, prev.y)),
    }));
  };

  // Mouse / Touch handlers for panning
  const handlePointerDown = (clientX, clientY) => {
    setIsDragging(true);
    dragStartRef.current = {
      mouseX: clientX,
      mouseY: clientY,
      panX: pan.x,
      panY: pan.y,
    };
  };

  const handlePointerMove = (clientX, clientY) => {
    if (!isDragging) return;

    const dx = clientX - dragStartRef.current.mouseX;
    const dy = clientY - dragStartRef.current.mouseY;

    const { maxPanX, maxPanY } = getPanBounds(zoom);

    setPan({
      x: Math.max(-maxPanX, Math.min(maxPanX, dragStartRef.current.panX + dx)),
      y: Math.max(-maxPanY, Math.min(maxPanY, dragStartRef.current.panY + dy)),
    });
  };

  const handlePointerUp = () => {
    setIsDragging(false);
  };

  // Mouse wheel zoom
  const handleWheel = (e) => {
    e.preventDefault();
    const zoomStep = 0.1;
    const delta = e.deltaY < 0 ? zoomStep : -zoomStep;
    handleZoomChange(zoom + delta);
  };

  // Reset to default crop state
  const handleReset = () => {
    setZoom(1);
    setPan({ x: 0, y: 0 });
  };

  // Execute canvas crop and emit File
  const handleApplyCrop = async () => {
    if (!imageMeta.loaded || isProcessing) return;

    try {
      setIsProcessing(true);

      const { scale } = getPanBounds(zoom);

      // Map crop box center and dimensions back into original image pixels
      const originalCenterX = imageMeta.naturalWidth / 2 - pan.x / scale;
      const originalCenterY = imageMeta.naturalHeight / 2 - pan.y / scale;

      const sourceWidth = cropBox.width / scale;
      const sourceHeight = cropBox.height / scale;

      const sourceX = originalCenterX - sourceWidth / 2;
      const sourceY = originalCenterY - sourceHeight / 2;

      const cropArea = {
        sourceX,
        sourceY,
        sourceWidth,
        sourceHeight,
        outputWidth: targetWidth,
        outputHeight: targetHeight,
      };

      const croppedFile = await getCroppedImg(imageSrc, cropArea, fileName, fileType);
      const previewUrl = URL.createObjectURL(croppedFile);

      onCropComplete(croppedFile, previewUrl);
    } catch (err) {
      console.error('Image cropping error:', err);
    } finally {
      setIsProcessing(false);
    }
  };

  if (!isOpen) return null;

  const { scale } = getPanBounds(zoom);
  const renderedWidth = imageMeta.naturalWidth * scale;
  const renderedHeight = imageMeta.naturalHeight * scale;

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        backgroundColor: 'rgba(15, 23, 42, 0.78)',
        backdropFilter: 'blur(6px)',
        WebkitBackdropFilter: 'blur(6px)',
        zIndex: 9999,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '16px',
        animation: 'epFadeIn 0.2s ease-out',
      }}
      onClick={(e) => {
        if (e.target === e.currentTarget && !isProcessing) onCancel();
      }}
    >
      <div
        style={{
          backgroundColor: '#ffffff',
          borderRadius: '16px',
          boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.25)',
          maxWidth: '720px',
          width: '100%',
          overflow: 'hidden',
          display: 'flex',
          flexDirection: 'column',
          border: '1px solid var(--ep-border, #E2E8F0)',
        }}
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div
          style={{
            padding: '18px 24px',
            borderBottom: '1px solid var(--ep-border, #E2E8F0)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            backgroundColor: '#ffffff',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <div
              style={{
                width: '36px',
                height: '36px',
                borderRadius: '8px',
                backgroundColor: 'rgba(255, 91, 0, 0.1)',
                color: 'var(--ep-primary, #FF5B00)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              <CropIcon size={20} />
            </div>
            <div>
              <h2
                style={{
                  margin: 0,
                  fontSize: '17px',
                  fontWeight: 700,
                  color: 'var(--ep-text-primary, #0F172A)',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '8px',
                }}
              >
                <span>{aspectTitle}</span>
                <span
                  style={{
                    fontSize: '11px',
                    fontWeight: 700,
                    textTransform: 'uppercase',
                    backgroundColor: 'rgba(255, 91, 0, 0.08)',
                    color: 'var(--ep-primary, #FF5B00)',
                    padding: '2px 8px',
                    borderRadius: '12px',
                    letterSpacing: '0.04em',
                  }}
                >
                  {aspectRatio === 4 / 5
                    ? '4:5 Portrait'
                    : Math.abs(aspectRatio - 16 / 6) < 0.1
                    ? '16:6 Wide Banner'
                    : `${aspectRatio.toFixed(2)}:1`}
                </span>
              </h2>
              <p
                style={{
                  margin: '2px 0 0 0',
                  fontSize: '12px',
                  color: 'var(--ep-text-secondary, #64748B)',
                }}
              >
                Drag to position and use the zoom slider to adjust the composition.
              </p>
            </div>
          </div>

          <button
            type="button"
            onClick={onCancel}
            disabled={isProcessing}
            style={{
              background: 'none',
              border: 'none',
              cursor: isProcessing ? 'not-allowed' : 'pointer',
              color: '#94A3B8',
              padding: '6px',
              borderRadius: '8px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              transition: 'all 0.15s ease',
            }}
            onMouseOver={(e) => {
              e.currentTarget.style.color = '#0F172A';
              e.currentTarget.style.backgroundColor = '#F1F5F9';
            }}
            onMouseOut={(e) => {
              e.currentTarget.style.color = '#94A3B8';
              e.currentTarget.style.backgroundColor = 'transparent';
            }}
          >
            <X size={20} />
          </button>
        </div>

        {/* Viewport Workspace */}
        <div
          ref={containerRef}
          style={{
            position: 'relative',
            backgroundColor: '#090D16',
            height: '440px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            overflow: 'hidden',
            userSelect: 'none',
            cursor: isDragging ? 'grabbing' : 'grab',
          }}
          onMouseDown={(e) => handlePointerDown(e.clientX, e.clientY)}
          onMouseMove={(e) => handlePointerMove(e.clientX, e.clientY)}
          onMouseUp={handlePointerUp}
          onMouseLeave={handlePointerUp}
          onTouchStart={(e) => {
            if (e.touches[0]) handlePointerDown(e.touches[0].clientX, e.touches[0].clientY);
          }}
          onTouchMove={(e) => {
            if (e.touches[0]) handlePointerMove(e.touches[0].clientX, e.touches[0].clientY);
          }}
          onTouchEnd={handlePointerUp}
          onWheel={handleWheel}
        >
          {/* Subtle instructions watermark on hover */}
          <div
            style={{
              position: 'absolute',
              top: '12px',
              left: '12px',
              zIndex: 30,
              display: 'inline-flex',
              alignItems: 'center',
              gap: '6px',
              padding: '4px 10px',
              backgroundColor: 'rgba(0, 0, 0, 0.55)',
              borderRadius: '6px',
              color: '#CBD5E1',
              fontSize: '11px',
              fontWeight: 500,
              pointerEvents: 'none',
            }}
          >
            <Move size={12} />
            <span>Drag image to pan</span>
          </div>

          {/* Active Image Layer */}
          {imageMeta.loaded && (
            <div
              style={{
                position: 'absolute',
                width: `${renderedWidth}px`,
                height: `${renderedHeight}px`,
                transform: `translate(${pan.x}px, ${pan.y}px)`,
                pointerEvents: 'none',
                transition: isDragging ? 'none' : 'transform 0.05s ease-out',
              }}
            >
              <img
                src={imageSrc}
                alt="Source preview for crop"
                style={{
                  width: '100%',
                  height: '100%',
                  objectFit: 'fill',
                  display: 'block',
                }}
                draggable={false}
              />
            </div>
          )}

          {/* Shaded Mask: Dims areas outside the crop box */}
          <div
            style={{
              position: 'absolute',
              inset: 0,
              pointerEvents: 'none',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              zIndex: 10,
            }}
          >
            {/* The Crop Aperture Box */}
            <div
              style={{
                width: `${cropBox.width}px`,
                height: `${cropBox.height}px`,
                boxShadow: '0 0 0 9999px rgba(10, 15, 29, 0.72)',
                border: '2px solid var(--ep-primary, #FF5B00)',
                borderRadius: '4px',
                position: 'relative',
                overflow: 'hidden',
                boxSizing: 'border-box',
              }}
            >
              {/* Rule of Thirds Grid Lines */}
              <div
                style={{
                  position: 'absolute',
                  inset: 0,
                  display: 'grid',
                  gridTemplateColumns: 'repeat(3, 1fr)',
                  gridTemplateRows: 'repeat(3, 1fr)',
                  pointerEvents: 'none',
                }}
              >
                <div style={{ borderRight: '1px solid rgba(255, 255, 255, 0.28)', borderBottom: '1px solid rgba(255, 255, 255, 0.28)' }} />
                <div style={{ borderRight: '1px solid rgba(255, 255, 255, 0.28)', borderBottom: '1px solid rgba(255, 255, 255, 0.28)' }} />
                <div style={{ borderBottom: '1px solid rgba(255, 255, 255, 0.28)' }} />

                <div style={{ borderRight: '1px solid rgba(255, 255, 255, 0.28)', borderBottom: '1px solid rgba(255, 255, 255, 0.28)' }} />
                <div style={{ borderRight: '1px solid rgba(255, 255, 255, 0.28)', borderBottom: '1px solid rgba(255, 255, 255, 0.28)' }} />
                <div style={{ borderBottom: '1px solid rgba(255, 255, 255, 0.28)' }} />

                <div style={{ borderRight: '1px solid rgba(255, 255, 255, 0.28)' }} />
                <div style={{ borderRight: '1px solid rgba(255, 255, 255, 0.28)' }} />
                <div />
              </div>

              {/* Crop Corner Accents */}
              <div style={{ position: 'absolute', top: 0, left: 0, width: 14, height: 14, borderTop: '3px solid #ffffff', borderLeft: '3px solid #ffffff' }} />
              <div style={{ position: 'absolute', top: 0, right: 0, width: 14, height: 14, borderTop: '3px solid #ffffff', borderRight: '3px solid #ffffff' }} />
              <div style={{ position: 'absolute', bottom: 0, left: 0, width: 14, height: 14, borderBottom: '3px solid #ffffff', borderLeft: '3px solid #ffffff' }} />
              <div style={{ position: 'absolute', bottom: 0, right: 0, width: 14, height: 14, borderBottom: '3px solid #ffffff', borderRight: '3px solid #ffffff' }} />
            </div>
          </div>
        </div>

        {/* Bottom Controls Bar */}
        <div
          style={{
            padding: '16px 24px',
            backgroundColor: '#ffffff',
            borderTop: '1px solid var(--ep-border, #E2E8F0)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: '16px',
            flexWrap: 'wrap',
          }}
        >
          {/* Zoom Slider and Reset */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flex: 1, minWidth: '240px' }}>
            <button
              type="button"
              onClick={() => handleZoomChange(zoom - 0.2)}
              disabled={zoom <= 1}
              title="Zoom out"
              style={{
                background: 'none',
                border: '1px solid #E2E8F0',
                borderRadius: '6px',
                padding: '6px',
                color: zoom <= 1 ? '#CBD5E1' : '#475569',
                cursor: zoom <= 1 ? 'not-allowed' : 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              <ZoomOut size={16} />
            </button>

            <div style={{ position: 'relative', flex: 1, display: 'flex', alignItems: 'center' }}>
              <input
                type="range"
                min="1"
                max="3"
                step="0.02"
                value={zoom}
                onChange={(e) => handleZoomChange(parseFloat(e.target.value))}
                style={{
                  width: '100%',
                  accentColor: 'var(--ep-primary, #FF5B00)',
                  cursor: 'pointer',
                }}
              />
            </div>

            <button
              type="button"
              onClick={() => handleZoomChange(zoom + 0.2)}
              disabled={zoom >= 3}
              title="Zoom in"
              style={{
                background: 'none',
                border: '1px solid #E2E8F0',
                borderRadius: '6px',
                padding: '6px',
                color: zoom >= 3 ? '#CBD5E1' : '#475569',
                cursor: zoom >= 3 ? 'not-allowed' : 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              <ZoomIn size={16} />
            </button>

            <span
              style={{
                fontSize: '12px',
                fontWeight: 600,
                color: '#64748B',
                minWidth: '42px',
                fontVariantNumeric: 'tabular-nums',
              }}
            >
              {Math.round(zoom * 100)}%
            </span>

            <button
              type="button"
              onClick={handleReset}
              title="Reset position and zoom"
              style={{
                background: 'none',
                border: '1px solid #E2E8F0',
                borderRadius: '6px',
                padding: '6px 10px',
                fontSize: '12px',
                fontWeight: 600,
                color: '#64748B',
                cursor: 'pointer',
                display: 'inline-flex',
                alignItems: 'center',
                gap: '4px',
              }}
            >
              <RotateCcw size={13} />
              <span>Reset</span>
            </button>
          </div>

          {/* Action Buttons */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <button
              type="button"
              onClick={onCancel}
              disabled={isProcessing}
              style={{
                padding: '9px 18px',
                fontSize: '13px',
                fontWeight: 600,
                borderRadius: '8px',
                border: '1px solid #CBD5E1',
                backgroundColor: '#ffffff',
                color: '#334155',
                cursor: isProcessing ? 'not-allowed' : 'pointer',
                transition: 'all 0.15s ease',
              }}
            >
              Cancel
            </button>

            <button
              type="button"
              onClick={handleApplyCrop}
              disabled={isProcessing}
              style={{
                padding: '9px 20px',
                fontSize: '13px',
                fontWeight: 600,
                borderRadius: '8px',
                border: 'none',
                backgroundColor: 'var(--ep-primary, #FF5B00)',
                color: '#ffffff',
                cursor: isProcessing ? 'not-allowed' : 'pointer',
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                boxShadow: '0 2px 4px rgba(255, 91, 0, 0.25)',
                transition: 'all 0.15s ease',
              }}
            >
              {isProcessing ? (
                <>
                  <Loader2 size={15} className="ep-spin" />
                  <span>Applying Crop…</span>
                </>
              ) : (
                <>
                  <Check size={16} />
                  <span>Apply Crop</span>
                </>
              )}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

export default ImageCropperModal;
