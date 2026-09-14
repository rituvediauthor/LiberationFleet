/** Brief window after tapping a mention suggestion so composers don't collapse on blur. */
let mentionSelectionUntil = 0;

export function beginMentionSelection(holdMs = 750): void {
  mentionSelectionUntil = Date.now() + holdMs;
}

export function isMentionSelectionPending(): boolean {
  return Date.now() < mentionSelectionUntil;
}
