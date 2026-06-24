import { BIP39_WORDLIST } from './bip39-wordlist';
import { generateRecoveryPhrase, validateRecoveryPhrase } from './recovery-key.util';

describe('recovery-key.util', () => {
  it('generateRecoveryPhrase should return 12 valid BIP39 words', () => {
    const phrase = generateRecoveryPhrase();
    const words = phrase.split(' ');

    expect(words.length).toBe(12);
    expect(new Set(words).size).toBeGreaterThan(1);
    words.forEach(word => {
      expect(BIP39_WORDLIST).toContain(word);
    });
  });

  it('generateRecoveryPhrase should complete quickly without hanging', () => {
    const started = performance.now();

    for (let i = 0; i < 100; i++) {
      generateRecoveryPhrase();
    }

    expect(performance.now() - started).toBeLessThan(1000);
  });

  it('validateRecoveryPhrase should accept generated phrases', async () => {
    const phrase = generateRecoveryPhrase();
    await expectAsync(validateRecoveryPhrase(phrase)).toBeResolvedTo(true);
  });
});
