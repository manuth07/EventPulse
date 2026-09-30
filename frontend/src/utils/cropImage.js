/**
 * High-performance HTML5 Canvas image cropping and File conversion utilities.
 */

/**
 * Creates an Image element from a source URL
 * @param {string} url - Source image URL or blob URL
 * @returns {Promise<HTMLImageElement>}
 */
export const createImage = (url) =>
  new Promise((resolve, reject) => {
    const image = new Image();
    image.addEventListener('load', () => resolve(image));
    image.addEventListener('error', (error) => reject(error));
    // Only set crossOrigin for remote http/https URLs to avoid browser security restrictions on blob: URLs
    if (url && (url.startsWith('http://') || url.startsWith('https://')) && !url.startsWith('blob:')) {
      image.setAttribute('crossOrigin', 'anonymous');
    }
    image.src = url;
  });

/**
 * Crops an image based on source coordinates and outputs a standard File object.
 * 
 * @param {string} imageSrc - Source object URL of the image.
 * @param {{ sourceX: number, sourceY: number, sourceWidth: number, sourceHeight: number, outputWidth?: number, outputHeight?: number }} cropArea
 * @param {string} [fileName='cropped-image.jpg'] - Original file name to preserve.
 * @param {string} [mimeType='image/jpeg'] - Image MIME type.
 * @returns {Promise<File>} - Resolves with the standard File object ready for FormData upload.
 */
export async function getCroppedImg(
  imageSrc,
  cropArea,
  fileName = 'cropped-image.jpg',
  mimeType = 'image/jpeg'
) {
  const image = await createImage(imageSrc);
  const canvas = document.createElement('canvas');
  const ctx = canvas.getContext('2d');

  if (!ctx) {
    throw new Error('Unable to obtain 2D canvas context for image cropping.');
  }

  const { sourceX, sourceY, sourceWidth, sourceHeight } = cropArea;

  // Clamp source bounds within natural dimensions
  const safeSourceX = Math.max(0, Math.min(image.naturalWidth - 1, sourceX));
  const safeSourceY = Math.max(0, Math.min(image.naturalHeight - 1, sourceY));
  const safeSourceWidth = Math.max(1, Math.min(image.naturalWidth - safeSourceX, sourceWidth));
  const safeSourceHeight = Math.max(1, Math.min(image.naturalHeight - safeSourceY, sourceHeight));

  // Determine export canvas resolution
  // If high-res target specified, use it; otherwise use full source resolution
  const targetWidth = cropArea.outputWidth || Math.round(safeSourceWidth);
  const targetHeight = cropArea.outputHeight || Math.round(safeSourceHeight);

  canvas.width = targetWidth;
  canvas.height = targetHeight;

  ctx.imageSmoothingEnabled = true;
  ctx.imageSmoothingQuality = 'high';

  ctx.drawImage(
    image,
    safeSourceX,
    safeSourceY,
    safeSourceWidth,
    safeSourceHeight,
    0,
    0,
    targetWidth,
    targetHeight
  );

  return new Promise((resolve, reject) => {
    // Preserve PNG and WebP if specified, otherwise default to image/jpeg for broad compatibility
    let effectiveMime = 'image/jpeg';
    if (mimeType === 'image/png') {
      effectiveMime = 'image/png';
    } else if (mimeType === 'image/webp') {
      effectiveMime = 'image/webp';
    }

    const quality = effectiveMime === 'image/png' ? undefined : 0.92;

    canvas.toBlob(
      (blob) => {
        if (!blob) {
          reject(new Error('Canvas rendering failed or produced an empty blob.'));
          return;
        }

        let extension = '.jpg';
        if (effectiveMime === 'image/png') extension = '.png';
        else if (effectiveMime === 'image/webp') extension = '.webp';

        const baseName = fileName ? fileName.replace(/\.[^/.]+$/, '') : 'cropped-image';
        const safeFileName = `${baseName || 'cropped-image'}${extension}`;

        const file = new File([blob], safeFileName, {
          type: effectiveMime,
          lastModified: Date.now(),
        });

        resolve(file);
      },
      effectiveMime,
      quality
    );
  });
}

export default getCroppedImg;
