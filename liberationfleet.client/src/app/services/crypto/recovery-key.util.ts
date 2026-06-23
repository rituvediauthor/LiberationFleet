export const BACKUP_WRAP_LEGACY_PASSWORD = 1;
export const BACKUP_WRAP_RECOVERY_KEY = 2;

const WORD_COUNT = 12;

let cachedWordlist: string[] | null = null;

export async function loadBip39Wordlist(): Promise<string[]> {
  if (cachedWordlist) {
    return cachedWordlist;
  }

  const response = await fetch('/assets/bip39-english.txt');
  if (!response.ok) {
    throw new Error('Failed to load recovery wordlist.');
  }

  const text = await response.text();
  cachedWordlist = text
    .split(/\r?\n/)
    .map(word => word.trim().toLowerCase())
    .filter(Boolean);

  if (cachedWordlist.length < 2048) {
    throw new Error('Recovery wordlist is invalid.');
  }

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

export async function generateRecoveryPhrase(): Promise<string> {
  const wordlist = await loadBip39Wordlist();
  const entropy = crypto.getRandomValues(new Uint8Array(16));
  const indices = entropyToWordIndices(entropy, WORD_COUNT);

  return indices.map(index => wordlist[index]).join(' ');
}

function entropyToWordIndices(entropy: Uint8Array, count: number): number[] {
  const indices: number[] = [];
  let bitBuffer = 0;
  let bitCount = 0;
  let byteIndex = 0;

  while (indices.length < count) {
    while (bitCount < 11 && byteIndex < entropy.length) {
      bitBuffer = (bitBuffer << 8) | entropy[byteIndex++];
      bitCount += 8;
    }

    if (bitCount >= 11) {
      bitCount -= 11;
      indices.push((bitBuffer >> bitCount) & 0x7ff);
    }
  }

  return indices;
}
