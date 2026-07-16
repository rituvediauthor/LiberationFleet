import {
  defaultAcceptAttribute,
  isSafeMediaDataUrl,
  validateAttachmentFile
} from './media-attachment-allowlist.util';

function file(name: string, type: string, size = 1024): File {
  return new File([new Uint8Array(size)], name, { type });
}

describe('media-attachment-allowlist', () => {
  it('allows jpeg/png/webp/gif images', () => {
    expect(validateAttachmentFile(file('a.jpg', 'image/jpeg')).ok).toBeTrue();
    expect(validateAttachmentFile(file('a.png', 'image/png')).ok).toBeTrue();
    expect(validateAttachmentFile(file('a.webp', 'image/webp')).ok).toBeTrue();
    expect(validateAttachmentFile(file('a.gif', 'image/gif')).ok).toBeTrue();
  });

  it('blocks svg and html even when claimed as images', () => {
    expect(validateAttachmentFile(file('x.svg', 'image/svg+xml')).ok).toBeFalse();
    expect(validateAttachmentFile(file('x.html', 'text/html')).ok).toBeFalse();
    expect(validateAttachmentFile(file('payload.exe.jpg', 'image/jpeg')).ok).toBeFalse();
  });

  it('rejects exotic image MIME types like bmp/heic', () => {
    expect(validateAttachmentFile(file('a.bmp', 'image/bmp')).ok).toBeFalse();
    expect(validateAttachmentFile(file('a.heic', 'image/heic')).ok).toBeFalse();
  });

  it('enforces allowedKinds for library-style image-only picks', () => {
    const video = validateAttachmentFile(file('clip.mp4', 'video/mp4'), ['image']);
    expect(video.ok).toBeFalse();
    const image = validateAttachmentFile(file('pic.jpg', 'image/jpeg'), ['image']);
    expect(image.ok).toBeTrue();
  });

  it('rejects oversized files', () => {
    const huge = validateAttachmentFile(file('big.jpg', 'image/jpeg', 13 * 1024 * 1024));
    expect(huge).toEqual({ ok: false, reason: 'too-large' });
  });

  it('builds a strict accept attribute without image/* wildcards', () => {
    const accept = defaultAcceptAttribute(['image']);
    expect(accept).toContain('image/jpeg');
    expect(accept).not.toContain('image/*');
    expect(accept).not.toContain('svg');
  });

  it('only treats safe raster/AV data URLs as renderable', () => {
    expect(isSafeMediaDataUrl('data:image/jpeg;base64,aaa')).toBeTrue();
    expect(isSafeMediaDataUrl('data:image/svg+xml;base64,aaa')).toBeFalse();
    expect(isSafeMediaDataUrl('data:text/html;base64,aaa')).toBeFalse();
  });
});
