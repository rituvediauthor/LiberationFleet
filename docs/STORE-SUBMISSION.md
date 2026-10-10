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
   - Account deletion (Play Data safety URL): `https://liberationfleet.org/delete-account`
   - Terms: `https://liberationfleet.org/terms`
   - Community Standards: `https://liberationfleet.org/community-standards`  
   Source text: `liberationfleet.client/src/assets/privacy-policy.txt`, `account-deletion.txt`, and `terms-of-use.txt`. Deploy production so these routes resolve over HTTPS before store submit.
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

Open testing and Production both require these. Internal testing is looser; if Console blocks with “cannot be published yet,” finish this section first, then return to the release.

#### A. Dashboard checklist

1. Play Console → **Liberation Fleet** → **Dashboard** (or **Publishing overview**).
2. Open every task marked incomplete / with an error and finish it.
3. Re-check Dashboard until required items are clear before retrying Open testing / Production.

#### B. Main store listing (fixes “Add a full description”)

1. **Grow → Store presence → Main store listing** (or **Store settings → Main store listing**).
2. Fill **all** required fields for the default language (usually **English (United States)**):
   - **App name**
   - **Short description** (≤ 80 characters)
   - **Full description** (this is the “Add a full description” error — paste a real multi-paragraph description; empty or whitespace does not save)
   - **App icon** (512×512)
   - **Feature graphic** (1024×500)
   - **Phone screenshots** (at least 2)
3. Click **Save**.

Suggested full-description themes (edit to match marketing voice): mutual-aid crews/fleets, gift logging, Library of Things, E2EE chat, optional voice, voluntary Stripe donations to support the nonprofit app — not a bank or payment app for users’ money between each other.

#### C. Privacy, ads, ratings, Data safety

1. Set **Privacy policy** URL: `https://liberationfleet.org/privacy`
2. **Policy → App content** (complete every required questionnaire):
   - Target audience / age: **18+**
   - Violence / sexual content consistent with Community Standards
   - **Data safety** — see **C.1** below (match `privacy-policy.txt`)
   - **Ads**: Liberation Fleet typically **does not** show ads → declare no ads
   - **Content ratings** questionnaire → apply rating
3. Save each form until App content shows no outstanding required tasks.

#### C.1 Data safety (suggested answers)

Aligned with the published Privacy Policy. Confirm with counsel before submit; Play labels change slightly over time.

##### Overview / pledges

| Question | Suggested |
|----------|-----------|
| Does the app collect / share required user data types? | **Yes** |
| Is all user data collected encrypted in transit? | **Yes** (HTTPS) |
| Do you provide a way for users to request that their data be deleted? | **Yes** (Profile → delete account). Note: deletion anonymizes/disables; gift/donation/report rows may be retained — be ready to explain if asked |
| **Delete account URL** (required for listing) | `https://liberationfleet.org/delete-account` — must be live HTTPS before submit (source: `account-deletion.txt`) |
| Do you provide a way for users to request access to their data? | **No** (policy: no full personal-data download today) — or **Yes** only if you truly offer it |
| Independent security review (SOC2 / ISO / etc.)? | **No** unless you have one |

##### Data types to declare as **collected**

Mark each as collected for **App functionality** (and **Account management** / **Security** where asked). Not for advertising / sale.

| Play category | Declare? | Notes |
|---------------|----------|--------|
| **Personal info → Name** | Yes | Username / display name |
| **Personal info → Email address** | Yes | Account email |
| **Personal info → User IDs** | Yes | Account ids |
| **Personal info → Address** | Usually **No** | You store country / postal for discovery, not a full street address — use **Approximate location** instead |
| **Personal info → Phone number** | No | |
| **Personal info → Race and ethnicity** | Yes (optional) | Optional identity-group tags users choose |
| **Personal info → Sexual orientation** | Yes (optional) | Same (if those tags are offered) |
| **Personal info → Other info** | Yes | Aid prefs, disability level, people represented, payment **handles** (Venmo etc.), roles |
| **Financial info → User payment info** | **No** | Card data stays with **Stripe**; you never store PAN/CVC |
| **Financial info → Purchase history** | Yes | App donation amounts + Stripe session ids / yearly totals |
| **Location → Approximate** | Yes | Country / postal code for local crews & offerings |
| **Location → Precise** | **No** | No live GPS sharing with other users |
| **Photos and videos** | Yes | Avatars, chat/library media (often E2EE ciphertext on server) |
| **Audio files** | Yes | Audio notes / attachments; voice realtime is LiveKit (see sharing) |
| **Messages → Other in-app messages** | Yes | Chat, DMs, forums, proposals — mostly **E2EE** (you store ciphertext + keys wrapped for users; you cannot read bodies in normal operation). Still declare **collected** because data leaves the device |
| **App activity → App interactions** | Yes | Server/request logs needed to run the app |
| **App activity → Other actions** | Yes (optional) | Gift log / mutual-aid structural events |
| **App info and performance → Crash logs / Diagnostics** | Yes if you keep server error/diagnostic logs | No Google Analytics SDK today |
| **Device or other IDs** | Yes | Auth tokens, remembered devices, FCM/push tokens when push is enabled |
| **Contacts / Calendar / Health & fitness** | Generally **No** for Contacts/Calendar. Disability-level mutual-aid fields → prefer **Other personal info** unless Play forces Health — do not claim clinical health data |

##### Collected vs shared

For each collected type, Play asks **Is this data shared?** (“Shared” = transferred to a **third party**.)

Suggested pattern:

| Situation | Answer |
|-----------|--------|
| Shown to other crewmates / friends inside the product | That is **user-to-user** visibility; still mark **Collected**. Sharing with “other users” is not the same as a third-party SDK — follow Play’s wording on each screen (often you only mark **Shared** for external companies) |
| **Stripe** (donations) | **Purchase history** / donation metadata → **Shared** with Stripe (payment processing) |
| **LiveKit** (voice) | Voice session / media transport → **Shared** with LiveKit (or declare under Audio / App activity as processed by voice provider) |
| Hosting (Azure), email SMTP, optional report-moderation vendor | **Shared** with service providers if the form lists them / “other third parties” — purposes: App functionality, Security, Fraud prevention |
| **Sold / used for ads / advertising or marketing** | **No** for all types |
| **Ephemeral** (never on server)? | Only if truly never leaves the device — E2EE ciphertext **does** leave the device → not ephemeral |

##### Encryption / E2EE wording

- **Encrypted in transit:** Yes (TLS).
- If asked whether data is **encrypted at rest** / **encrypted so the developer cannot read it**: for **Messages** and much **Photos/Videos/Audio**, say content is **end-to-end encrypted** (ciphertext on server; operators cannot read message bodies in normal operation). **Gift amounts, who-gave-whom, account email, donation records, and report evidence** are **not** E2EE — declare those as readable server data.

##### Purposes (check what applies; avoid “Advertising” / “Sell”)

Typical checks: **App functionality**, **Account management**, **Analytics** only if you truly measure product usage (you do **not** ship GA — skip unless you mean first-party server logs for reliability), **Developer communications** (email for password reset), **Fraud prevention / Security**, **Compliance / Legal** (safety reports / NCMEC when required).

##### After the form

1. **Save** → Data safety should leave “Needs attention”.
2. Store listing privacy policy URL must match.
3. If Play later rejects for inconsistency, compare the form to `liberationfleet.client/src/assets/privacy-policy.txt` sections 2–4.

#### D. Financial features declaration

1. **Policy → App content → Financial features** (or Dashboard link “Financial features”).
2. Declare honestly. For Liberation Fleet today:
   - You are **not** a bank, wallet, brokerage, lending, crypto exchange, or money transmitter for user-to-user funds.
   - Crew **gift logging / mutual aid** is coordination/record-keeping, not moving money inside the app.
   - **Stripe Checkout** is **voluntary donations to Liberation Fleet** (platform support), not IAP unlockables and not peer payouts.
3. If the form asks whether the app provides financial features: choose the options that match **donations / fundraising** if listed; otherwise select that you **don’t** provide banking/crypto/trading features, and use any free-text/notes field to mention voluntary Stripe donations to the nonprofit.
4. Save. Re-open if Dashboard still flags it incomplete.

*(Confirm final checkbox wording with counsel if unsure — Play’s categories change.)*

#### E. Health declaration

1. **Policy → App content → Health** (or Dashboard “Health declaration”).
2. Liberation Fleet is a **mutual-aid / community coordination** app, **not** a medical device, clinical, telehealth, or fitness-tracking health app.
3. Answer that the app **does not** provide health / medical features (unless you later add something that clearly is health-related).
4. Save.

#### F. Countries / regions for Open testing (or Production)

1. Left nav: **Testing → Open testing → Countries/regions**  
   (for Production: **Release → Production → Countries/regions**).
2. Click **Add countries/regions** → select at least one (recommended first launch: **United States** only per [JURISDICTION-ASSUMPTIONS.md](./JURISDICTION-ASSUMPTIONS.md)).
3. **Save**.
4. Return to the release draft → countries error should clear.

#### G. Open testing release (after A–F)

1. **Testing → Open testing → Create new release**.
2. Upload / add from library the signed `.aab` → release name + notes → **Next**.
3. Confirm countries are set → **Save** / **Start rollout to Open testing** / **Send for review** as prompted.
4. Open testing is public-ish (anyone with the link / listing can join); it usually needs the same policy completeness as Production.

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

**Tester flow (must do in this order on the phone):**

1. On the phone, open Play Store → tap profile → confirm the **exact Google account** whose email you added as a tester (not a work profile / different account).
2. Open the **opt-in / join** link from Console (**Testers** tab → copy link). It looks like  
   `https://play.google.com/apps/internaltest/...` (Internal) or  
   `https://play.google.com/apps/testing/com.liberationfleet.app` (Closed).
3. Tap **Accept** / **Become a tester** until the page says you are a tester.
4. **Then** tap **Download it on Google Play** / **Download test app**.

If step 4 shows **Item not found**, see troubleshooting below.

##### Internal testing — “Item not found” troubleshooting

Check these in order:

1. **Release status** — Play Console → **Testing → Internal testing → Releases**. Status must be **Available to internal testers** (not Draft / still “Processing”). First-ever upload can take **a few hours** (sometimes up to ~48h) before the store listing exists.
2. **Email list saved + release assigned to that list** — **Testers** tab: email is in an active list, list is checked for this track, **Save** was clicked.
3. **Same Google account** — phone Play Store account must match the invited email. Sign out other accounts or switch profile, then reopen the join link.
4. **Join before download** — opening the public `play.google.com/store/apps/details?id=com.liberationfleet.app` link without joining first almost always shows Item not found for unpublished apps. Always use the Console join link first.
5. **Device / OS** — app requires **Android 7.0+** (`minSdk 24`). Very old devices will not see the listing.
6. **Play Store cache** — Play Store → clear cache (or restart phone) → reopen the join link → Accept → Download again.
7. **Wrong track link** — if you also set up Closed testing, Internal testers must use the **Internal** join URL (`internaltest/...`), not the Closed `apps/testing/...` URL.
8. **Device catalog → Supported empty / “isn't compatible”** — open the device under **All** and read the reason.  
   `android.software.video.encoder` required was caused by `@honem/native-video-compressor`; the app manifest overrides it to `required="false"`. After changing features, bump `versionCode`, upload a new `.aab`, and roll out again before Supported repopulates.

Optional later: **Closed testing**, then **Production** (see 1.4).

### 1.4 Release tracks

1. **Internal testing** first (1.3 D above) — verify install + login + chat + voice + donate.
2. Optional: **Closed testing** for a larger group (same upload flow as Internal; add a bigger tester list).
3. Optional: **Open testing** — needs full **1.2** checklist (description, countries, financial + health declarations). See **1.2 G**.
4. **Production** when testing looks good — click-by-click below (also needs **1.2** complete).

#### Production release (click-by-click)

Do this only after Internal (or Closed) smoke tests pass and **Step 1.2** store listing / policy items are complete. Play will block “Send for review” if required Dashboard checklist items are unfinished.

##### A. Confirm the app is ready to publish

1. Open [Play Console](https://play.google.com/console) → **Liberation Fleet**.
2. Open **Dashboard** (or **Publishing overview** — wording varies).
3. Clear every required item marked incomplete, typically:
   - Main store listing (title, short/full description, screenshots, icon, feature graphic)
   - Privacy policy URL
   - App content questionnaires (target audience **18+**, Data safety, Content ratings, Ads declaration, etc.)
   - Countries / store presence if still prompted
4. When the Dashboard shows the app can be sent for review / published, continue.

##### B. Create the production release

1. Left nav: **Release → Production**  
   (older: **Publish → Production**, or **Release → Production → Releases**).
2. Click **Create new release** (or **Edit release** if a draft already exists).
3. If Play asks about **Play App Signing** and you have not enrolled yet: accept Google managing the app signing key → continue.
4. Under **App bundles**:
   - Prefer **Add from library** if you already uploaded this `.aab` on Internal/Closed (same `versionCode`).
   - Or **Upload** a new signed `.aab` (must have a **new** `versionCode` higher than any previous upload).
5. Wait until the bundle shows as processed with no blocking errors (fix target API / minSdk issues first if red errors appear).
6. **Release name** — e.g. `1.0.0 — Initial release`.
7. **Release notes** (What’s new) — paste your initial-release notes for each language you support (at least **default / en-US**).
8. Click **Next**.

##### C. Countries / regions

1. On the countries step (or **Release → Production → Countries/regions** / **Reach and devices**):
   - Choose **Available in … countries** (or equivalent).
   - For first launch: either **all countries** or a limited set you are ready to support.
2. Confirm **18+** / content ratings already match the countries you pick.
3. Click **Next**.

##### D. Rollout percentage

1. On the rollout step:
   - **Full rollout (100%)** — everyone in selected countries gets the release after approval (fine for first launch of a new app).
   - Or **Staged rollout** (e.g. 20%) — only a fraction of users; you can raise % later under Production → Releases → **Manage rollout**.
2. New apps with no prior production users: 100% is normal; staged rollout mainly helps updates.
3. Click **Next**.

##### E. Preview and send for review

1. Review the summary (bundle `versionCode`, countries, rollout %, release notes).
2. Click **Save** (keeps a draft) or **Start rollout to Production** / **Send X changes for review** (exact button varies).
3. If prompted, confirm declarations (e.g. export compliance, ads, official name).
4. Status should move to **Pending publication** / **In review** (not live yet).
5. Watch email + Play Console **Publishing overview** until status is **Available on Google Play** (or **Published**).

##### F. After it goes live

1. Install from the public Play Store listing on a phone that is **not** only an internal tester account (or clear tester-only expectations).
2. Smoke-test: sign in → crew → chat → voice → donate Checkout.
3. For the next update: bump `versionCode` in `android/app/build.gradle`, upload a new `.aab`, create another Production release, send for review again.

**Tip:** If “Send for review” is disabled, return to **Dashboard** / **Publishing overview** and finish every required task (listing, Data safety, content rating, etc.) — that is almost always the blocker, not the AAB itself.

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
