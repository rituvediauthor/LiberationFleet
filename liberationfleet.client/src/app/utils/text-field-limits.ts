/**
 * Canonical plaintext length limits for user-facing inputs.
 * Keep in sync with LiberationFleet.Server.Application.Common.TextFieldLimits.
 */
export const TextFieldLimits = {
  /** Crew / fleet display names */
  orgName: 100,
  /** Chat room display names */
  chatRoomName: 120,
  /** Chat room purpose / short blurbs */
  chatRoomPurpose: 2000,
  /** Titles: proposals, forums, rules, library offerings */
  title: 200,
  /** Long-form bodies: proposals, forum posts, crew rules, goods/services descriptions */
  longBody: 10000,
  /** Fleet rule descriptions (matches DB column) */
  fleetRuleDescription: 4000,
  /** Chat, DMs, library request messages, comments, replies */
  message: 2000,
  /** Emergency request purpose, kick/report notes-adjacent short prose */
  shortPurpose: 2000,
  /** Content report / kick reason notes */
  note: 1000,
  /** Payment platform handle / username */
  paymentHandle: 128,
  /** Custom payment platform name (matches DB) */
  paymentPlatformName: 64,
  /** Library unit label / placeholder display name (short) */
  shortLabel: 64,
  /** Non-crewmate placeholder display name */
  placeholderDisplayName: 256
} as const;

export type TextFieldLimitKey = keyof typeof TextFieldLimits;
