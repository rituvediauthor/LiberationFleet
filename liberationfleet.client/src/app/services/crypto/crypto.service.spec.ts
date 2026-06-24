import { TestBed } from '@angular/core/testing';
import { CryptoService } from './crypto.service';
import { BACKUP_WRAP_RECOVERY_KEY } from './recovery-key.util';

describe('CryptoService', () => {
  let service: CryptoService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(CryptoService);
  });

  it('should wrap and unwrap a private key backup with a recovery phrase', async () => {
    const phrase = 'knock crazy economy boy claim moment sweet apple gather patch cricket fetch';
    const keyPair = await service.generateIdentityKeyPair();
    const backup = await service.wrapPrivateKeyBackup(
      keyPair.privateKey,
      phrase,
      BACKUP_WRAP_RECOVERY_KEY
    );

    const restored = await service.unwrapPrivateKeyBackup(backup, phrase);
    const restoredPublicSpki = await service.exportPublicKeyFromPrivateKey(restored);
    const originalPublicSpki = await service.exportPublicKeySpki(keyPair.publicKey);

    expect(restoredPublicSpki).toBe(originalPublicSpki);
  });
});
