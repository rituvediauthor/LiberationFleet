const DEFAULT_VIEWPORT =
  'width=device-width, initial-scale=1, maximum-scale=1, viewport-fit=cover';
const MEDIA_DETAIL_VIEWPORT =
  'width=device-width, initial-scale=1, maximum-scale=5, user-scalable=yes, viewport-fit=cover';

let mediaDetailOpenCount = 0;

function getViewportMeta(): HTMLMetaElement | null {
  return document.querySelector('meta[name="viewport"]');
}

/** Allow pinch-zoom while a media lightbox/detail view is open. */
export function enterMediaDetailZoom(): void {
  mediaDetailOpenCount += 1;
  if (mediaDetailOpenCount !== 1) {
    return;
  }
  const meta = getViewportMeta();
  if (meta) {
    meta.setAttribute('content', MEDIA_DETAIL_VIEWPORT);
  }
  document.documentElement.classList.add('lf-media-detail-open');
  document.body.style.touchAction = 'pan-x pan-y pinch-zoom';
}

export function exitMediaDetailZoom(): void {
  if (mediaDetailOpenCount === 0) {
    return;
  }
  mediaDetailOpenCount -= 1;
  if (mediaDetailOpenCount > 0) {
    return;
  }
  const meta = getViewportMeta();
  if (meta) {
    meta.setAttribute('content', DEFAULT_VIEWPORT);
  }
  document.documentElement.classList.remove('lf-media-detail-open');
  document.body.style.touchAction = '';
}
