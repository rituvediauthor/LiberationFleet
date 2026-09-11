import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { CryptoSessionService } from './crypto/crypto-session.service';
import { CryptoService } from './crypto/crypto.service';
import { EncryptedLocation, ProfileLocationPayload } from '../models/profile.model';
import { normalizePostalCode, isValidPostalCode, isValidCountryCode } from '../constants/countries';

@Injectable({
  providedIn: 'root'
})
export class ProfileLocationService {
  private readonly locationSubject = new BehaviorSubject<ProfileLocationPayload | null>(null);
  readonly location$ = this.locationSubject.asObservable();

  constructor(
    private cryptoSession: CryptoSessionService,
    private cryptoService: CryptoService
  ) {}

  get current(): ProfileLocationPayload | null {
    return this.locationSubject.value;
  }

  setPlaintext(location: ProfileLocationPayload | null): void {
    if (!location?.countryCode || !location.zipCode) {
      this.locationSubject.next(null);
      return;
    }
    this.locationSubject.next({
      countryCode: location.countryCode,
      zipCode: location.zipCode
    });
  }

  clear(): void {
    this.locationSubject.next(null);
  }

  async encrypt(countryCode: string | null, zipCode: string | null): Promise<EncryptedLocation | null> {
    const country = (countryCode ?? '').trim().toUpperCase();
    const zip = normalizePostalCode(zipCode ?? '');
    if (!country && !zip) {
      return null;
    }
    if (!isValidCountryCode(country) || !zip) {
      throw new Error('Enter a valid country and postal code.');
    }

    const key = await this.cryptoSession.ensureUserContentKeyReady();
    const encrypted = await this.cryptoService.encryptJson<ProfileLocationPayload>(key, {
      countryCode: country,
      zipCode: zip
    });
    return {
      nonce: encrypted.nonce,
      ciphertext: encrypted.ciphertext,
      keyVersion: 1
    };
  }

  async decrypt(envelope: EncryptedLocation | null | undefined): Promise<ProfileLocationPayload | null> {
    const nonce = envelope?.nonce?.trim() ?? '';
    const ciphertext = envelope?.ciphertext?.trim() ?? '';
    if (!nonce || !ciphertext) {
      this.clear();
      return null;
    }

    const key = await this.cryptoSession.ensureUserContentKeyReady();
    try {
      const payload = await this.cryptoService.decryptJson<ProfileLocationPayload>(
        key,
        nonce,
        ciphertext
      );
      const country = (payload.countryCode ?? '').trim().toUpperCase();
      const zip = normalizePostalCode(payload.zipCode ?? '');
      if (!country || !zip) {
        this.clear();
        return null;
      }
      const location = { countryCode: country, zipCode: zip };
      this.setPlaintext(location);
      return location;
    } catch {
      // Corrupt ciphertext or wrong key material — treat as unset.
      this.clear();
      return null;
    }
  }
}
