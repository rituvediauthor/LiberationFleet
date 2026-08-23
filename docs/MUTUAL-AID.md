# Mutual aid system

Liberation Fleet coordinates crew mutual aid through **giving seasons**, **reception order**, **cycle caps**, **survival thresholds**, **emergency sacrifices**, and **Library of Things** contribution credit. This document reflects the current behavior of `MutualAidService` and related profile/gift surfaces.

## Seasons and cycles

- A crew starts a season when enough members mark ready (first season) or when the previous season’s cycles complete (rollover).
- Each season participant gets a **primary `SeasonCycle`** for the current season, plus provisional cycles for the next and following seasons.
- **In-need members** receive incomplete primaries that must be filled to the effective member/non-member cycle cap.
- **Non-needers** still get a primary cycle on season creation (and when joining mid-season), marked **complete from the start** so season accounting and unique primary indexes stay consistent. Opting back into need can reopen an under-cap primary.
- Season end promotes the next provisional queue, converts emergency sacrifices into the next season’s percent boost, and rebuilds provisional queues.

## Reception / giving order (`ReceptionEntryType`)

Entries in the giving order are typed as:

| API value | Enum | Meaning |
|-----------|------|---------|
| `survivalThreshold` | `SurvivalThreshold` | Monthly survival aid for `NeedsSurvivalAid` when the crew allows thresholds |
| `representative` | `Representative` | Active elected Representative term (unlimited need) |
| `cycle` | `Cycle` | Remaining primary/segment cycle need |
| `catchUp` | `CatchUp` | After a completed cycle, when the monthly snapshot shows the effective cap grew |

Use `ReceptionEntryTypeExtensions.ToApiValue` / `TryParseApiValue` instead of ad-hoc strings when adding server code.

## Priority score and sacrifices

Priority score (simplified):

```
base × (peopleRepresented + disabilityLevel + 1) × (1 + PercentBonus/100)
```

- **Sacrifices this season** (`CrewMembership.EmergencySacrificesThisSeason`) increment when a member responds to emergencies.
- At season start/rollover those counts become **`User.PercentBonus`** (+10% per sacrifice) and the counter resets.
- **Sacrifices last season** on the profile is derived from `PercentBonus` (count = bonus ÷ 10). That count **explains the percent boost** shown for the current season.
- Profile shows both last-season and this-season sacrifice counts; the live priority score is shown prominently near the avatar.
- Organizer (−1) and not-in-need (−2) demotions apply only to **Library of Things** request ranking, not to profile display or giving/receiving season order.

In-season priority for LoT requests excludes active-season contributions when the requester is already in season (aligned with profile).

## Membership and capacity

- **Financial membership** uses a 3-month contribution average **including** Library of Things gifts (plus honorary/role paths).
- Averages shown as “monthly contributions (3 mo)” for capacity / in-need floor are **excluding** LoT unless a label says otherwise.
- Cycle caps may be fixed or capacity-derived; emergency splits can segment a primary and create bound segment cycles.

## Survival thresholds and catch-up

- When enabled, monthly survival thresholds are created for members with `NeedsSurvivalAid`.
- Catch-up rows appear only after the crew’s monthly catch-up snapshot, and never for non-needers.

## Library of Things gifts

- Stock use / durable handoffs create verified peer gifts on the **Library of Things** platform.
- Those gifts store `Gift.LibraryItemTitle` so **gift history** can show the good/service name (the encrypted gift log still embeds the title in its message independently).

## Dev tools (`/api/dev/mutual-aid`)

Available when the server enables mutual-aid dev tools (Development or Docker):

| Action | Behavior |
|--------|----------|
| New Month | Creates the next month’s survival thresholds (and catch-up snapshot path via production helpers) |
| New Season | Forces production rollover (`TryEndSeasonAsync` with `force: true`) |
| Complete Cycles | Completes remaining incomplete cycles at cap, then attempts rollover |
| Recalculate Caps | Recomputes season member/non-member caps after membership changes |
| Reset Season | Clears season flags, catch-up snapshot fields, membership season state, **and all season cycles/thresholds** for the crew so a fresh Mark Ready does not collide with leftover primaries |

The client **dev toolbar** is horizontally scrollable so all actions remain reachable on narrow viewports.

## Staging donations

App donations (Stripe Checkout supporting hosting/development) are separate from crew mutual aid. On **staging** hostnames the donate page shows a warning that donations will not work there; use production to give.
