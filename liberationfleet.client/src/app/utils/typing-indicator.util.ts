/** Idle timeout matching product requirement: 2s without keystrokes ends typing. */
export const TYPING_IDLE_MS = 2000;

/** Re-broadcast while still typing so peers keep the indicator alive. */
export const TYPING_REFRESH_MS = 1500;

/** Slightly longer than idle so network delay does not flicker the label. */
export const TYPING_RECEIVER_TTL_MS = 2500;

/**
 * Local typing activity: enter typing on input, reset a 2s idle timer on each
 * character, leave typing when idle/empty/send. Hub sends are throttled.
 */
export class TypingActivityController {
  private idleTimer: ReturnType<typeof setTimeout> | null = null;
  private lastSentAt = 0;
  private active = false;

  constructor(
    private readonly send: (isTyping: boolean) => void | Promise<void>,
    private readonly idleMs = TYPING_IDLE_MS,
    private readonly refreshMs = TYPING_REFRESH_MS
  ) {}

  onInput(hasText: boolean): void {
    if (!hasText) {
      this.stop();
      return;
    }

    const now = Date.now();
    if (!this.active || now - this.lastSentAt >= this.refreshMs) {
      this.active = true;
      this.lastSentAt = now;
      void this.send(true);
    }

    this.resetIdle();
  }

  stop(): void {
    this.clearIdle();
    if (!this.active) {
      return;
    }

    this.active = false;
    this.lastSentAt = 0;
    void this.send(false);
  }

  destroy(): void {
    this.stop();
  }

  private resetIdle(): void {
    this.clearIdle();
    this.idleTimer = setTimeout(() => this.stop(), this.idleMs);
  }

  private clearIdle(): void {
    if (this.idleTimer != null) {
      clearTimeout(this.idleTimer);
      this.idleTimer = null;
    }
  }
}

export interface TypingPresenceEntry {
  key: string;
  displayName: string;
}

/**
 * Tracks remote typers and builds a display label that expires after TTL.
 */
export class TypingPresenceTracker {
  private readonly typers = new Map<string, { displayName: string; timer: ReturnType<typeof setTimeout> }>();

  constructor(
    private readonly onChange: (label: string) => void,
    private readonly ttlMs = TYPING_RECEIVER_TTL_MS
  ) {}

  setTyping(key: string, displayName: string, isTyping: boolean): void {
    const existing = this.typers.get(key);
    if (existing) {
      clearTimeout(existing.timer);
      this.typers.delete(key);
    }

    if (!isTyping || !key) {
      this.emit();
      return;
    }

    const name = displayName.trim() || 'Someone';
    const timer = setTimeout(() => {
      this.typers.delete(key);
      this.emit();
    }, this.ttlMs);

    this.typers.set(key, { displayName: name, timer });
    this.emit();
  }

  clear(): void {
    for (const entry of this.typers.values()) {
      clearTimeout(entry.timer);
    }
    this.typers.clear();
    this.emit();
  }

  destroy(): void {
    this.clear();
  }

  private emit(): void {
    const names = [...this.typers.values()].map(t => t.displayName);
    this.onChange(formatTypingIndicator(names));
  }
}

export function formatTypingIndicator(names: string[]): string {
  const unique = [...new Set(names.map(n => n.trim()).filter(Boolean))];
  if (unique.length === 0) {
    return '';
  }
  if (unique.length === 1) {
    return `${unique[0]} is typing...`;
  }
  if (unique.length === 2) {
    return `${unique[0]} and ${unique[1]} are typing...`;
  }
  return 'Several people are typing...';
}
