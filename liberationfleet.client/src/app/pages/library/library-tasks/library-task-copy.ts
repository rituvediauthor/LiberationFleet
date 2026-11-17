/** Shared quest-value wording so "$X" is not mistaken for a cash payout. */

export function formatQuestValueAmount(value: number): string {
  return Number.isInteger(value) ? value.toString() : value.toFixed(2).replace(/\.?0+$/, '');
}

export function questValueCardNote(value: number): string {
  return `Completion counts as a $${formatQuestValueAmount(value)} gift.`;
}

export function questValueExplanation(value: number): string {
  return (
    `Completion counts as a $${formatQuestValueAmount(value)} contribution from the quest doer, ` +
    `but does not reduce the quest giver's amount needed in survival thresholds and reception cycles.`
  );
}
