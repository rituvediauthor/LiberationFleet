import { BIP39_WORDLIST } from './bip39-wordlist';

export const BACKUP_WRAP_LEGACY_PASSWORD = 1;
export const BACKUP_WRAP_RECOVERY_KEY = 2;

const WORD_COUNT = 12;

let cachedWordlist: string[] | null = null;

export async function loadBip39Wordlist(): Promise<string[]> {
  if (cachedWordlist) {
    return cachedWordlist;
  }

  cachedWordlist = [...BIP39_WORDLIST];
  return cachedWordlist;
}

export function normalizeRecoveryPhrase(phrase: string): string {
  return phrase
    .trim()
    .toLowerCase()
    .replace(/[^a-z\s]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

export function recoveryPhraseToSecret(phrase: string): string {
  return normalizeRecoveryPhrase(phrase);
}

export async function validateRecoveryPhrase(phrase: string): Promise<boolean> {
  const normalized = normalizeRecoveryPhrase(phrase);
  const words = normalized.split(' ').filter(Boolean);
  if (words.length !== WORD_COUNT) {
    return false;
  }

  const wordlist = await loadBip39Wordlist();
  const wordSet = new Set(wordlist);
  return words.every(word => wordSet.has(word));
}

export function generateRecoveryPhrase(): string {
  const indices = randomWordIndices(WORD_COUNT);
  return indices.map(index => BIP39_WORDLIST[index]).join(' ');
}

function randomWordIndices(count: number): number[] {
  const indices: number[] = [];
  let bitBuffer = 0;
  let bitCount = 0;

  while (indices.length < count) {
    while (bitCount < 11) {
      const byte = crypto.getRandomValues(new Uint8Array(1))[0];
      bitBuffer = (bitBuffer << 8) | byte;
      bitCount += 8;
    }

    bitCount -= 11;
    indices.push((bitBuffer >> bitCount) & 0x7ff);
  }

  return indices;
}
