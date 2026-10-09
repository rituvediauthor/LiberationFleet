# App Store & Google Play submission — step-by-step

Prerequisites: Capacitor projects exist and a physical-device smoke test passed. See [NATIVE-APPS.md](./NATIVE-APPS.md).  
Master order of operations: [LAUNCH-CHECKLIST.md](./LAUNCH-CHECKLIST.md).  
Backend: [AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md).

Bundle / application ID: **`com.liberationfleet.app`**

---

## Step 0 — Before either store

1. Deploy **production** API on Azure with HTTPS ([AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md)).
2. Publish **Privacy Policy** and **Terms** on public HTTPS URLs. The SPA serves them at:
   - Privacy: `https://liberationfleet.org/privacy` (staging: your staging host + `/privacy`)
   - Terms: `https://liberationfleet.org/terms`
   - Community Standards: `https://liberationfleet.org/community-standards`  
   Source text: `liberationfleet.client/src/assets/privacy-policy.txt` and `terms-of-use.txt`. Deploy production so these routes resolve over HTTPS before store submit.
3. Edit `liberationfleet.client/src/environments/environment.native.ts`:
   ```ts
   apiBaseUrl: 'https://your-production-host'
   ```
4. Confirm CORS includes Capacitor origins + your web domain.
5. From `liberationfleet.client`:
   ```bash
   npm run build:native
   npm run cap:sync
   ```
6. Prepare icon (1024²), splash, and screenshots.
7. Smoke-test on a **physical device**: login, chat, attach image, voice join, open donation Checkout.
8. Create a **demo account** (email/password) and a small test crew for reviewers.

---

## Step 1 — Google Play (Android)

### 1.1 Register and create the app

1. Open [Google Play Console](https://play.google.com/console).
2. Pay the one-time registration fee if you have not already.
3. **Create app** → name **Liberation Fleet** → App → Free (unless you sell the APK itself).
4. Accept declarations as prompted.

### 1.2 Store listing & policy

1. **Grow → Store presence → Main store listing** (wording varies by Console version):
   - Short description, full description
   - App icon, feature graphic, phone screenshots
2. Set **Privacy policy** URL (required).
3. **Policy → App content** (complete all required questionnaires):
   - Target audience / age: **18+** aligned with product
   - Violence / sexual content answers consistent with Community Standards
   - **Data safety**: account data, messages, photos/media; describe encryption / E2EE **honestly**
4. Complete **Ads**, **Content ratings**, etc. as required before production.

### 1.3 Signing and AAB (click-by-click)

#### A. Prepare the Android project

1. In a terminal:
   ```bash
   cd liberationfleet.client
   npm run build:native
   npm run cap:sync
   npm run cap:android
   ```
   That opens **Android Studio** on the `android` folder.
2. Wait for Gradle sync to finish (bottom status bar / “Syncing…”).
3. Confirm `RECORD_AUDIO` (and other needed permissions) in  
   `android/app/src/main/AndroidManifest.xml` (needed for LiveKit voice).
4. Open `android/app/build.gradle` → under `defaultConfig`, set:
   - `versionName` — marketing version, e.g. `"1.0.0"`
   - `versionCode` — integer that **must increase on every Play upload**, e.g. `1`, then `2`, …
5. Save the file. Sync if Android Studio prompts.

#### B. Create an upload keystore + signed `.aab` (first time)

Do this once; keep the keystore forever. Losing it makes updates painful.

1. In Android Studio menu: **Build → Generate Signed App Bundle or APK…**  
   (older menus: **Build → Generate Signed Bundle / APK…**)
2. Choose **Android App Bundle** → **Next**.
3. Under **Key store path**, click **Create new…**
4. In **New Key Store**:
   - **Key store path** → **…** → pick a folder **outside** the repo (e.g. `Documents/LiberationFleet-keys/`) → filename like `liberationfleet-upload.jks` → **OK**
   - **Password** / **Confirm** — strong password; save in a password manager
   - **Alias** — e.g. `upload`
   - **Key password** / **Confirm** — can match store password; save it too
   - **Validity (years)** — `25` or more
   - **Certificate** — fill at least First and Last Name (org/city optional but fine to complete)
   - **OK**
5. Back on the signing screen:
   - **Key store path**, **Key store password**, **Key alias**, **Key password** should be filled
   - Check **Remember passwords** only on a machine you trust
   - **Next**
6. Select build variant **release** → **Create** (or **Finish**).
7. When the build finishes, click the notification **locate** / open the folder.  
   You want the file ending in **`.aab`** (often  
   `android/app/release/app-release.aab`).  
   That is what you upload to Play Console — **not** an `.apk`.

**Never commit** the `.jks` / `.keystore`, passwords, or `key.properties` to git. Back up the keystore file + passwords offline (encrypted drive / password manager attachment).

#### C. Later uploads (keystore already exists)

1. **Build → Generate Signed App Bundle or APK…** → **Android App Bundle** → **Next**
2. **Choose existing…** → select your `.jks` → enter passwords → alias → **Next**
3. **release** → **Create**
4. Bump `versionCode` in `build.gradle` before each new Play upload.

#### D. Play App Signing (first upload to Play Console)

Google keeps the **app signing key**; you only keep the **upload key** (the keystore above).

1. [Play Console](https://play.google.com/console) → your app **Liberation Fleet**.
2. Go to a release track (recommended first):  
   **Testing → Internal testing → Create new release**  
   (left nav labels vary slightly by Console version).
3. If prompted to enroll in **Play App Signing** / **Google Play App Signing**:
   - Accept the default (**Let Google manage and protect your app signing key**)
   - Continue / Save — you do **not** need to upload a separate “app signing” key for a new app
4. Under **App bundles**, click **Upload** → select your `app-release.aab` → wait for processing.
5. Add a short **Release name** / notes → **Next** → **Save** / **Start rollout to Internal testing**.
6. Add tester emails (or a Google Group) on the Internal testing testers tab → copy the opt-in link → install on a device and smoke-test.

Optional later: **Closed testing**, then **Production** (countries, rollout %, send for review).

### 1.4 Release tracks (summary)

1. **Internal testing** first (above) — verify install + login + chat + voice + donate.
2. Optional: **Closed testing** for a larger group.
3. **Production** → countries, rollout %, send for review.

### 1.5 Review notes (Play)

In the release “notes for reviewers” (or App content → instructions):

- Demo email / password  
- Steps: sign in → open crew → chat → (optional) voice  
- E2EE: reviewers cannot read message ciphertext; point them to gift log / settings UI if they need visible flows  
- Donations: Stripe Checkout is **voluntary platform donations**, not IAP unlockables  
- Mic: voice chat only  

---

## Step 2 — Apple App Store (iOS, macOS required)

### 2.1 Apple Developer & App ID

1. Enroll at [developer.apple.com](https://developer.apple.com) (**Organization** recommended) — annual fee.
2. **Certificates, Identifiers & Profiles → Identifiers → App IDs** → register `com.liberationfleet.app`.
3. Enable capabilities you need now (Push later when implemented).
4. Create distribution certificate + provisioning profile (Xcode “Automatically manage signing” is fine for most solo/small teams).

### 2.2 App Store Connect record

1. [App Store Connect](https://appstoreconnect.apple.com) → **My Apps → +** → iOS → select bundle ID → SKU → name **Liberation Fleet**.
2. Set **Privacy Policy** URL, category, subtitle, description.
3. **Age rating** / content questionnaire (17+ if adult-content features exist).
4. **App Privacy** nutrition labels (account, messages, photos — be accurate about E2EE and what the server stores).

### 2.3 Xcode archive

1. On a Mac:
   ```bash
   cd liberationfleet.client
   npm run build:native
   npm run cap:sync
   npm run cap:ios
   ```
2. Select Team, unique display name, Version / Build (bump Build every upload).
3. Confirm `Info.plist` usage strings, e.g.:
   - `NSMicrophoneUsageDescription` — “Liberation Fleet needs the microphone for crew voice chat.”
4. **Product → Archive** → **Distribute App → App Store Connect** → Upload.
5. Wait for processing in App Store Connect → select the build on your version → fill screenshots → **Submit for Review**.

### 2.4 TestFlight first

1. App Store Connect → **TestFlight** → add internal testers (App Store Connect users).
2. Install via TestFlight; re-run smoke tests.
3. Optional external TestFlight (triggers Beta App Review once).
4. Fix crashes before production submit.

### 2.5 App Review notes (Apple)

- Demo account + steps to join a crew and open chat  
- Note that message content is end-to-end encrypted  
- Donations: voluntary Stripe Checkout framing; confirm Guideline 3.1.1 / 3.2.1 with counsel  
- If you add Sign in with Apple later, follow SIWA rules  

---

## Step 3 — Web (no store)

Users open `https://your-domain`. Ensure:

- Custom domain + managed TLS ([AZURE-GO-LIVE Step 10](./AZURE-GO-LIVE.md#step-10--custom-domain--tls))
- Same backend as mobile

---

## Step 4 — Common rejection causes

| Issue | Mitigation |
|-------|------------|
| Broken login / blank WebView | Correct `apiBaseUrl` + CORS + `cap:sync` |
| Missing privacy policy | Host HTTPS policy before submit |
| Mic without purpose string | Add plist / Play declarations |
| Incomplete age rating | Align with adult-content settings |
| Placeholder API URL | Never ship `REPLACE_WITH_YOUR_API_ORIGIN` |
| Crash on launch | TestFlight / Play internal track first |

---

## Step 5 — Versioning convention

| Platform | Field | Example |
|----------|-------|---------|
| Angular / marketing | `package.json` version | `1.0.0` |
| Android | `versionName` / `versionCode` | `1.0.0` / `1` |
| iOS | CFBundleShortVersionString / CFBundleVersion | `1.0.0` / `1` |

Bump native **build numbers** on every store upload; keep marketing version aligned across web + stores when possible.
