# App Store & Google Play submission guide

Prerequisites: Capacitor projects exist (`liberationfleet.client/android`, and `ios` on a Mac). See [NATIVE-APPS.md](./NATIVE-APPS.md). Master checklist: [LAUNCH-CHECKLIST.md](./LAUNCH-CHECKLIST.md).

Bundle / application ID: **`com.liberationfleet.app`**

---

## 0. Before either store

1. Deploy production API on Azure with HTTPS ([AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md)).
2. Set `liberationfleet.client/src/environments/environment.native.ts`:

   ```ts
   apiBaseUrl: 'https://your-production-host'
   ```

3. Confirm CORS includes Capacitor origins + your web domain.
4. Publish Privacy Policy + Terms URLs.
5. `cd liberationfleet.client && npm run cap:sync`
6. Prepare icon (1024²), splash, and screenshots.
7. Smoke-test on a physical device: login, chat, attach image, voice join, donation checkout open.

---

## 1. Google Play (Android)

### 1.1 Console setup

1. Pay the Play Console registration fee.
2. Create app → **Liberation Fleet** → App / Game → Free (or Paid if you later sell the app itself).
3. Complete **Store listing**: short/full description, graphics, categorized as Social / Lifestyle as appropriate.
4. **Privacy policy** URL (required).
5. **App content** questionnaire: target age 18+, violence/sexual content answers consistent with AUP, data safety form (account, messages, photos — declare encryption / E2EE carefully and honestly).

### 1.2 Build a release AAB

1. Open Android Studio: `npm run cap:android`
2. Add microphone permission if not present (`RECORD_AUDIO`) for LiveKit.
3. Set versionCode / versionName in `android/app/build.gradle`.
4. Create an upload keystore (store offline + in a secrets manager; never commit).
5. **Build → Generate Signed Bundle / APK → Android App Bundle**.
6. Enroll in **Play App Signing** (Google holds the app signing key).

### 1.3 Release tracks

1. **Internal testing** → upload AAB → add testers → verify install.
2. **Closed testing** (optional) → larger group.
3. **Production** → countries, rollout %, review.

### 1.4 Play review tips

- Provide a demo account (email/password) in the review notes.
- Explain E2EE: reviewers cannot read message bodies; provide a test crew with sample plaintext-friendly flows if needed (e.g. gift logging UI).
- Donations: document that Stripe Checkout is for **voluntary platform donations**, not unlockable digital goods.
- Declare mic use for voice chat only.

---

## 2. Apple App Store (iOS) — requires macOS

### 2.1 Developer + App Store Connect

1. Enroll in Apple Developer Program (Organization recommended).
2. Certificates, Identifiers & Profiles → App ID `com.liberationfleet.app` with Push (when ready), Associated Domains (optional).
3. App Store Connect → New App → iOS → bundle ID → SKU.
4. Fill Privacy Policy URL, category, age rating (17+ if adult content features exist), App Privacy nutrition labels.

### 2.2 Xcode archive

1. On a Mac: `npx cap add ios` (once), then `npm run cap:ios`.
2. Set team, signing, display name, version/build.
3. `Info.plist` usage strings, for example:
   - `NSMicrophoneUsageDescription` — “Liberation Fleet needs the microphone for crew voice chat.”
   - Photo library / camera if you add native pickers later.
4. Product → Archive → Distribute App → App Store Connect.
5. Wait for processing → select build in the version → Submit for Review.

### 2.3 TestFlight

1. Add internal testers (App Store Connect users).
2. Optional external TestFlight (Beta App Review once).
3. Fix crash/rejection issues before production submit.

### 2.4 App Review tips

- Demo account + steps to join a crew and open chat.
- Note that message content is end-to-end encrypted.
- If using Sign in with Apple later, follow Apple’s SIWA rules.
- Donations: same Stripe voluntary-donation framing; Apple’s guidelines on tipping/donations vary — confirm with counsel (Guideline 3.1.1 / 3.2.1).

---

## 3. Web app (already on Azure)

No store listing required. Users open `https://your-domain`. Ensure:

- Custom domain + managed TLS
- Service worker / PWA optional (not required)
- Same backend as mobile

---

## 4. Common rejection causes (avoid)

| Issue | Mitigation |
|-------|------------|
| Broken login / blank WebView | Correct `apiBaseUrl` + CORS |
| Missing privacy policy | Host HTTPS policy before submit |
| Mic without purpose string | Add plist / Play declarations |
| Incomplete age rating | Align with adult-content settings |
| Placeholder `REPLACE_WITH_YOUR_API_ORIGIN` | Never ship that string |
| Crash on launch | TestFlight / internal track first |

---

## 5. Versioning convention

| Platform | Field | Example |
|----------|-------|---------|
| Angular / marketing | `package.json` version | `1.0.0` |
| Android | `versionName` / `versionCode` | `1.0.0` / `1` |
| iOS | CFBundleShortVersionString / CFBundleVersion | `1.0.0` / `1` |

Bump native build numbers on every store upload; keep marketing version aligned across web + stores when possible.
