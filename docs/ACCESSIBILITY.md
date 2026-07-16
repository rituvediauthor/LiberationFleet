# Accessibility (WCAG 2)

Liberation Fleet targets **WCAG 2.2 Level AA** as the product goal. Shipping foundations does not equal a certified audit — re-check with axe / Lighthouse / manual keyboard + screen-reader passes before major releases.

## Foundations in the client

| Area | What we ship |
|------|----------------|
| Language | `html lang="en"` |
| Skip link | “Skip to main content” → `#main-content` |
| Landmarks | Single `<main id="main-content">` around routed UI |
| Focus | Global `:focus-visible` ring (`themes/a11y.css`); form pages no longer kill outline on `:focus` |
| Motion | `prefers-reduced-motion: reduce` short-circuits animations |
| Contrast | Darker subtle text token; stronger focus ring under `prefers-contrast: more` |
| Page titles | `LiberationFleetTitleStrategy` sets “Page · Liberation Fleet”; crew/fleet home expose an `sr-only` `<h1>` |
| Toasts | `aria-live` + `role="status"` / `role="alert"` (+ CDK `LiveAnnouncer`) |
| Dialogs | `appAccessibleDialog` — focus trap, Escape, restore focus (confirm, report, crypto, adult gate, kick, settings password, recovery, vote, sign-up legal modals, library image lightbox, voice device settings) |
| Bottom nav | `aria-current="page"` when active; decorative logos use `alt=""` |
| Menus | Kebab menus use `aria-expanded` / `aria-haspopup` / `role="menu"` / `role="menuitem"` + Escape (chat, DMs, friends, forums, proposals) |
| Forms | `aria-invalid` / `aria-describedby` / `role="alert"` via `isControlInvalidForA11y` on auth, profile, password update, create/edit/join crew & fleet, chats, proposals, forums, rules, emergency requests, donate |
| Donate | Amounts as `radiogroup` / `radio` with labels; status live region; out-of-range custom amount uses `role="alert"` |
| Settings | Content & theme preference lists use `role="radiogroup"` + `aria-labelledby` |
| Composers | Message/comment textareas expose `aria-label`; attachment pickers label remove actions |

## Attachment safety (related)

Media attachments use a **strict MIME ∩ extension allowlist** (JPEG/PNG/WebP/GIF, MP4/WebM/MOV, common audio). SVG/HTML/executables are blocked; images are rasterized to JPEG before encrypt; decrypted `data:` URLs are filtered before render. See `media-attachment-allowlist.util.ts`.


## Developer checklist for new UI

1. **Keyboard** — every interactive control reachable; visible focus ring; no `outline: none` without a stronger `:focus-visible` replacement.
2. **Name** — icon-only buttons need `aria-label`; decorative icons `aria-hidden="true"`; logos inside named buttons use `alt=""`.
3. **Forms** — `<label for>`, errors with `role="alert"` + `aria-describedby`, `aria-invalid` when shown (see `a11y-form.util.ts`).
4. **Modals** — use `role="dialog"` or `alertdialog`, `aria-modal="true"`, labelled title, and `appAccessibleDialog`.
5. **Menus** — `aria-expanded`, `aria-haspopup="menu"`, menu/menuitem roles, Escape to close.
6. **Status** — don’t rely on color alone; announce via toast live region or text.
7. **Touch / hit targets** — aim for ~44×44px on primary controls.

## Manual test pass (quick)

- Tab through crew home, donate, sign-in, a report dialog, and crypto unlock with keyboard only.
- Verify Skip link appears on first Tab and jumps into main content.
- With NVDA/VoiceOver: open a toast error; open confirm dialog; confirm focus is trapped and Escape closes where wired.
- Open a message kebab menu: Tab into items, Escape closes.
- Toggle OS “Reduce motion” and confirm toast/page animations don’t thrash.

## Out of scope / follow-ups

- Captions for future live video / voice (LiveKit) sessions
- Arrow-key navigation inside every menu (Tab + Escape is the current baseline)
- Full AA audit of every remaining historical screen
- Formal VPAT / third-party certification
