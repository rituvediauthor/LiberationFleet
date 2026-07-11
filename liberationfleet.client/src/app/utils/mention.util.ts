export interface MentionCandidate {
  userId: number;
  username: string;
}

export interface MentionSearchResponse {
  success: boolean;
  message: string;
  items: MentionCandidate[];
}

export type MentionTextMode = 'composer' | 'display';

export interface MentionTextSegment {
  type: 'text' | 'mention';
  value: string;
}

const MENTION_USERNAME_PATTERN = /[A-Za-z0-9_]/;
const MENTION_TOKEN_REGEX = /@([A-Za-z0-9_]+)/g;

export function findActiveMentionQuery(text: string, cursorIndex: number): string | null {
  const before = text.slice(0, cursorIndex);
  const match = before.match(/@([A-Za-z0-9_]*)$/);
  return match ? match[1] : null;
}

export function insertMention(
  text: string,
  cursorIndex: number,
  username: string
): { text: string; cursorIndex: number } {
  const before = text.slice(0, cursorIndex);
  const after = text.slice(cursorIndex);
  const match = before.match(/@([A-Za-z0-9_]*)$/);
  if (!match) {
    return { text, cursorIndex };
  }

  const start = before.length - match[0].length;
  const newText = `${text.slice(0, start)}@${username} ${after}`;
  return { text: newText, cursorIndex: start + username.length + 2 };
}

export function collectMentionedUserIds(text: string, usernameToId: Map<string, number>): number[] {
  const ids = new Set<number>();
  let match: RegExpExecArray | null;
  const regex = new RegExp(MENTION_TOKEN_REGEX.source, 'g');
  while ((match = regex.exec(text)) !== null) {
    const userId = usernameToId.get(match[1].toLowerCase());
    if (userId) {
      ids.add(userId);
    }
  }
  return [...ids];
}

export function isMentionUsernameChar(char: string): boolean {
  return char.length === 1 && MENTION_USERNAME_PATTERN.test(char);
}

export function parseMentionSegments(
  text: string,
  mode: MentionTextMode,
  knownUsernames?: ReadonlySet<string> | null
): MentionTextSegment[] {
  if (!text) {
    return [];
  }

  const segments: MentionTextSegment[] = [];
  let lastIndex = 0;
  let match: RegExpExecArray | null;
  const regex = new RegExp(MENTION_TOKEN_REGEX.source, 'g');

  while ((match = regex.exec(text)) !== null) {
    const username = match[1];
    const isKnown = !knownUsernames || knownUsernames.has(username.toLowerCase());
    if (!isKnown) {
      continue;
    }

    if (match.index > lastIndex) {
      segments.push({ type: 'text', value: text.slice(lastIndex, match.index) });
    }

    segments.push({
      type: 'mention',
      value: mode === 'composer' ? `@${username}` : username
    });
    lastIndex = match.index + match[0].length;
  }

  if (lastIndex < text.length) {
    segments.push({ type: 'text', value: text.slice(lastIndex) });
  }

  return segments;
}

export function buildMentionBackdropHtml(text: string, knownUsernames: ReadonlySet<string>): string {
  if (!text) {
    return '';
  }

  const segments = parseMentionSegments(text, 'composer', knownUsernames);
  if (segments.length === 0) {
    return escapeHtml(text).replace(/\n/g, '<br>');
  }

  const html = segments
    .map(segment => {
      if (segment.type === 'mention') {
        return `<strong class="mention-highlight">${escapeHtml(segment.value)}</strong>`;
      }
      return escapeHtml(segment.value).replace(/\n/g, '<br>');
    })
    .join('');

  return text.endsWith('\n') ? `${html}<br>` : html;
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}
