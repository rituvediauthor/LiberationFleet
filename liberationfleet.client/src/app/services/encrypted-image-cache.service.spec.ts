import { TestBed } from '@angular/core/testing';
import { EncryptedImageCacheService } from './encrypted-image-cache.service';
import { ProposalCryptoService } from './crypto/proposal-crypto.service';
import { CryptoSessionService } from './crypto/crypto-session.service';

describe('EncryptedImageCacheService', () => {
  let service: EncryptedImageCacheService;
  let proposalCrypto: jasmine.SpyObj<ProposalCryptoService>;
  let cryptoSession: { isUnlocked: jasmine.Spy };

  beforeEach(() => {
    proposalCrypto = jasmine.createSpyObj<ProposalCryptoService>('ProposalCryptoService', [
      'decryptImageDataUrl'
    ]);
    cryptoSession = { isUnlocked: jasmine.createSpy('isUnlocked').and.returnValue(true) };

    TestBed.configureTestingModule({
      providers: [
        EncryptedImageCacheService,
        { provide: ProposalCryptoService, useValue: proposalCrypto },
        { provide: CryptoSessionService, useValue: cryptoSession }
      ]
    });

    service = TestBed.inject(EncryptedImageCacheService);
  });

  afterEach(async () => {
    service.clear();
    await (service as unknown as { persistReady: Promise<void> }).persistReady;
  });

  it('reuses an in-memory image for the same resource', async () => {
    proposalCrypto.decryptImageDataUrl.and.resolveTo(objectUrlForPng());

    const first = await service.getDataUrl({}, 'avatar-1');
    const second = await service.getDataUrl({}, 'avatar-1');

    expect(first).toBeTruthy();
    expect(second).toBe(first);
    expect(proposalCrypto.decryptImageDataUrl).toHaveBeenCalledTimes(1);
  });

  it('restores a persisted profile image after the in-memory cache is dropped', async () => {
    proposalCrypto.decryptImageDataUrl.and.resolveTo(objectUrlForPng());

    const first = await service.getDataUrl({}, 'avatar-persist');
    expect(first).toBeTruthy();
    expect(proposalCrypto.decryptImageDataUrl).toHaveBeenCalledTimes(1);

    dropMemoryCache(service);
    const restored = await service.getDataUrl({}, 'avatar-persist');

    expect(restored).toBeTruthy();
    expect(restored).not.toBe(first);
    expect(proposalCrypto.decryptImageDataUrl).toHaveBeenCalledTimes(1);
  });

  it('returns a persisted image while encryption is locked without decrypting again', async () => {
    proposalCrypto.decryptImageDataUrl.and.resolveTo(objectUrlForPng());
    await service.getDataUrl({}, 'avatar-locked');

    dropMemoryCache(service);
    cryptoSession.isUnlocked.and.returnValue(false);
    proposalCrypto.decryptImageDataUrl.calls.reset();

    const restored = await service.getDataUrl({}, 'avatar-locked');
    expect(restored).toBeTruthy();
    expect(proposalCrypto.decryptImageDataUrl).not.toHaveBeenCalled();
  });

  it('restores persisted crew and fleet profile images after a restart', async () => {
    proposalCrypto.decryptImageDataUrl.and.resolveTo(objectUrlForPng());

    await service.getDataUrl({ crewId: 7 }, 'crew-image', 'ImageAsset');
    await service.getDataUrl({ fleetId: 3 }, 'fleet-image', 'ImageAsset');
    expect(proposalCrypto.decryptImageDataUrl).toHaveBeenCalledTimes(2);

    dropMemoryCache(service);
    proposalCrypto.decryptImageDataUrl.calls.reset();

    const crew = await service.getDataUrl({ crewId: 7 }, 'crew-image', 'ImageAsset');
    const fleet = await service.getDataUrl({ fleetId: 3 }, 'fleet-image', 'ImageAsset');

    expect(crew).toBeTruthy();
    expect(fleet).toBeTruthy();
    expect(proposalCrypto.decryptImageDataUrl).not.toHaveBeenCalled();
  });

  it('clears persisted images on session cache reset', async () => {
    proposalCrypto.decryptImageDataUrl.and.resolveTo(objectUrlForPng());
    await service.getDataUrl({}, 'avatar-clear');

    service.clear();
    await (service as unknown as { persistReady: Promise<void> }).persistReady;
    proposalCrypto.decryptImageDataUrl.calls.reset();
    proposalCrypto.decryptImageDataUrl.and.resolveTo(objectUrlForPng());

    await service.getDataUrl({}, 'avatar-clear');
    expect(proposalCrypto.decryptImageDataUrl).toHaveBeenCalledTimes(1);
  });
});

function dropMemoryCache(service: EncryptedImageCacheService): void {
  (service as unknown as { cache: Map<string, unknown> }).cache.clear();
}

function objectUrlForPng(): string {
  return URL.createObjectURL(new Blob([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], { type: 'image/png' }));
}
