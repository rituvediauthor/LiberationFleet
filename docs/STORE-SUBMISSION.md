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
   - Community Standards (also Play **Child safety → Safety standards URL**): `https://liberationfleet.org/community-standards`
   - Child safety contact (Play Child Safety Standards form): `rituvedi@liberationfleetco.org` (developer-account email, or another inbox you monitor for CSAM/compliance questions)  
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

**Suggested full description** (paste into Play Console; edit tone if you want):

```
Liberation Fleet helps adults (18+) form mutual-aid crews of up to 50 people, bound by crew bylaws and rules that the crew sets together.

Within a crew, members use end-to-end encrypted group chat and shared spaces to coordinate. Crews can also join a larger fleet—functioning together while each crew keeps its own culture and rules.

Mutual aid is inspired by rotating savings traditions (such as sou-sou): crewmates take turns receiving concentrated support from others. When someone has a spare dollar, they can choose to send it—on platforms like Venmo, PayPal, Cash App, or Zelle—to the person whose turn it is. Liberation Fleet helps organize and log that giving; it does not hold your money or process peer payments.

Generosity still matters when money is not involved. Logging financial aid, goods, or services raises a priority score—so scarce Library of Things items and the next turn in the giving season favor those who give, while also accounting for emergencies, disability, and systemic vulnerability. The Library of Things lets crewmates share durable goods, consumables, services, and digital offerings without a cash price tag—access is shaped by need, availability, and priority.

Crews are governed by the members: adding or removing crewmates and changing settings goes through time-limited democratic votes. Crewmates prioritize each other first; fleets let many crews cooperate without erasing what makes each crew distinct.

Optional donations through the app support Liberation Fleet Co. (hosting and development of the nonprofit platform). Those donations are separate from crew mutual aid and are processed by Stripe—Liberation Fleet does not store your card details.
```

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
- **On-device cache (Privacy Policy §2.13):** last-seen dashboard previews and decrypted profile/crew/fleet images may remain on the device after the app is closed until **sign-out**. That is not a third-party share and is **not** ephemeral. Do not describe those caches as memory-only.

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

Do this on a Mac with Xcode. A Windows PC cannot archive or upload an iOS build.

Recommended order: **2.1 enroll → 2.2 App ID → 2.3 create the App Store Connect record → 2.4–2.6 listing, age rating, and App Privacy (can overlap with the archive) → 2.7 upload a build → 2.8 TestFlight → 2.9 submit**. App Review will not start until the version page, privacy labels, and a processed build are all finished.

Bundle ID: **`com.liberationfleet.app`**. Sign-in today is **email and password only** (no Sign in with Apple, no Google sign-in). Do not turn on the Sign in with Apple capability until that feature exists in the app.

### 2.1 Enroll in the Apple Developer Program

Organization enrollment is the right account type for Liberation Fleet Co. An individual account lists a person’s legal name as the seller on the App Store.

1. On a Mac, open the **App Store** app → search **Xcode** → **Get** / **Install**. Wait until it finishes (several GB). Open Xcode once and let it install extra components if it asks.
2. In a browser, open [developer.apple.com/programs/enroll](https://developer.apple.com/programs/enroll) and sign in with the Apple ID that should own the account (the Account Holder).
3. Click **Start Your Enrollment** (or **Enroll**).
4. If Apple asks you to turn on two-factor authentication, do that on the Apple ID first (**Settings → [your name] → Sign-In & Security** on an Apple device), then return to enrollment.
5. Entity type: **Organization / Company / Nonprofit** — not Individual.
6. **Legal entity name** must match the nonprofit’s legal name and its D-U-N-S record exactly (same spelling, punctuation, and suffix). See [NONPROFIT-ENTITY-SETUP.md](./NONPROFIT-ENTITY-SETUP.md).
7. **D-U-N-S number:**
   - On the enrollment page, use **Look up your D-U-N-S**.
   - If a record exists, select it and continue.
   - If none exists, request one from the link Apple shows (Dun & Bradstreet). This is free and can take several days. Do not invent a number. Come back when you have it.
8. Enter the work phone and the website (`https://liberationfleet.org`).
9. Confirm you have authority to bind the organization to Apple’s agreements (Account Holder).
10. Submit. Apple emails if they need more proof (articles, a staff directory page, or a call). Reply from the domain email they expect.
11. When enrollment is accepted, pay the annual fee (about $99 USD) with a card in the organization’s name if you can.
12. Wait for the “Welcome to the Apple Developer Program” email before continuing. Approval after payment is often a day or two; organization checks can take longer.

**If this phone number is already on an individual membership:** do not start a second paid enrollment with the same number. Apple ties that number to the existing individual membership. Convert that membership into the organization instead.

Apple will not convert an individual membership to an organization while the membership is expired or while renewal is still in progress. Renew first, then submit the organization request. See [Program Renewal](https://developer.apple.com/help/account/membership/renewal/).

#### Agreements that say Expired

**View** on an expired Free Apps or Paid Apps row is read-only. The country menu only opens the old text for that country. Choosing **United States** does not accept anything, and a click that does nothing is that same view-only popup. There is no **View and Agree to Terms** on an expired row.

Sign in as the **Account Holder** (the Apple ID that owns the individual membership).

1. Open [developer.apple.com/account](https://developer.apple.com/account) → **Membership details**.
2. If the membership is expired or due, click **Renew Membership** and pay the annual fee. The receipt email arrives right away. The activation email can take up to **48 hours** (the payment screen often says 2 business days). Check **Membership details** on this page; when it says active, continue even if that second email has not arrived. If 48 hours pass and it is still not active, contact Apple with the order number. Do not pay again. Auto-renew, if it is offered, charges the card on the Apple ID.
3. On that same account page, if a banner says the Apple Developer Program License Agreement needs review, click **Review Agreement**, check the box, and click **Agree**. That is the Free Apps agreement. The country list in App Store Connect will not accept it.
4. Open [App Store Connect](https://appstoreconnect.apple.com) → **Business** → **Agreements**.
5. On **Paid Apps**, click **Renew Agreement** when it appears (it shows up after the membership renewal finishes). Enter the verification code if asked, check **I have read and agree**, then **Agree**.
6. Give App Store Connect up to a day to drop the **Expired** status after the membership page already says active.

If both rows stay **Expired**, **Renew Agreement** never appears, and **View** still only offers the country list:

1. Sign out of App Store Connect and the developer site. Open a private window in Safari and sign in again as the Account Holder.
2. If the rows are still expired, Apple has to resync the account. Go to [developer.apple.com/contact](https://developer.apple.com/contact/) → **Membership and Account** → **Program Purchase and Renewal** → request a call. Tell them the membership shows active on the developer site, and App Store Connect still shows Free Apps and Paid Apps as expired with only **View**.

#### Accept an agreement that is New (not Expired)

When a row offers **View and Agree to Terms**:

1. Click it. Enter the verification code sent to this Apple ID’s trusted phone if asked.
2. Check **I have read and agree to the terms** at the bottom. **Agree** stays disabled until that box is checked.
3. Click **Agree** (or **Submit**).
4. Repeat for every row that is not **Active**.

**Bank and tax on this screen:** agreeing to the terms is what unblocks the organization update. Liberation Fleet is a free app, and donations go through Stripe, so you do not need a bank account on this contract to ship. After you click **Agree**, fill **Contact** if Apple requires it. Leave **Bank Accounts** and **Tax Forms** empty if the page lets you return to Membership details.

Those tax and bank fields are still under your **personal** legal name until Apple finishes the organization change. After the change, Apple usually issues a new Paid Apps agreement. Put the nonprofit’s EIN and bank details on that new agreement, not on this one.

#### Submit the organization request

1. Sign in at [developer.apple.com/account](https://developer.apple.com/account) with the same Account Holder Apple ID.
2. Open **Membership details**.
3. Click **Update your information** (or the request link on that page).
4. Choose the option to update the **individual** membership to an **organization**. If that choice is not listed, use **Contact** on the account page and ask Apple to update this individual membership to an organization.
5. You must be the founder or cofounder. Enter the legal entity name, `https://liberationfleet.org`, a work email on the organization’s domain, and the D-U-N-S number.
6. Submit. Apple may ask for business documents (formation papers, the IRS determination letter, or proof you can bind the nonprofit). This review often takes days to a few weeks.
7. When Apple approves it, the same Apple ID, phone number, and Team ID remain. The App Store seller name becomes the organization’s legal name. Keep using this account for the rest of Step 2.

Until that approval, **Membership details** still says **Enrolled as Individual**, and Xcode’s Team menu shows your personal name twice. Pick the one **without** “(Personal Team)”. That is the paid membership. “(Personal Team)” is a free Xcode team and cannot upload to the App Store. The company name replaces your personal name only after Apple finishes the conversion.

Check progress at [developer.apple.com/account](https://developer.apple.com/account). A pending request shows as a banner on that page, and Apple emails if they need documents. There is no separate status page. If it has been more than a few weeks with no email, contact them: [developer.apple.com/contact](https://developer.apple.com/contact/) → **Membership and Account** → **Program Enrollment**.

Any apps already published on that individual account move to the organization seller name. If that account has personal apps that must stay under your own name, leave it as an individual. Enroll the nonprofit with a **new** Apple ID and a **different** phone number that can receive Apple’s text or call, then invite your personal Apple ID onto the organization team.

**Agreements (again, before the first submit):**

If you already accepted the agreements in order to request the organization change, open **Business → Agreements** once more after Apple approves that change. A new Paid Apps agreement often appears in the organization’s name. Accept it the same way. Bank and tax still stay empty for this free app unless that page blocks **Submit for Review**.

### 2.2 Register the App ID

Xcode can also create this ID the first time you turn on automatic signing. Registering it yourself first makes the Bundle ID show up in App Store Connect.

1. Open [developer.apple.com/account](https://developer.apple.com/account) → **Certificates, Identifiers & Profiles**.
2. Left side: **Identifiers** → **+**.
3. Select **App IDs** → **Continue**.
4. Select **App** → **Continue**.
5. **Description:** `Liberation Fleet`.
6. **Bundle ID:** select **Explicit** → type `com.liberationfleet.app`.
7. **Capabilities**:
   - **Push Notifications** — turn this **on**. The Xcode project already includes it. A binary with the Push entitlement and no matching capability on this App ID fails upload.
   - **Sign in with Apple** — not implemented. Leave off.
   - Associated Domains, App Groups, iCloud — leave off unless you have added that feature.
8. **Continue** → **Register**.

You do **not** create a distribution certificate or provisioning profile by hand. In 2.7, **Automatically manage signing** makes Xcode create them.

### 2.3 Create the App Store Connect app

1. [App Store Connect](https://appstoreconnect.apple.com) → **Apps**.
2. Click the blue **+** → **New App**.
3. **Platforms:** check **iOS** only.
4. **Name:** `Liberation Fleet` (30 characters max; this is the name on the store).
5. **Primary Language:** **English (U.S.)**.
6. **Bundle ID:** choose **`com.liberationfleet.app`** from the menu. If it is missing, finish 2.2, wait a minute, and reload. You cannot type a bundle ID that is not registered.
7. **SKU:** `liberation-fleet-ios` (an internal id shoppers never see; it cannot be changed later).
8. **User Access:** **Full Access**.
9. **Create**.

If Apple says **“Your user access settings could not be saved”** and that **all users currently have access**, the app still exists. Full Access is the right setting for this account. Continue to 2.4. Account Holder, Admin, and Finance can always open every app; that list cannot be narrowed. To change it later: the app → **General → App Information** → **Additional Information** → **Edit User Access**.

You land on the iOS version page (often **Distribution → iOS App → 1.0 Prepare for Submission**). The left nav labels shift between “App Store” and “Distribution”; both mean the same place.

### 2.4 Store listing, pricing, and URLs

Finish every field that shows a red or yellow dot. **Save** after each page.

#### A. Version page (what shoppers read)

Open the **1.0** version under **iOS App** (not TestFlight).

1. **Screenshots** — see **B** below. Upload at least one 6.7-inch iPhone set before submit. Three to six is a better listing.
2. **Promotional Text** (170 characters, can change without a new review). Paste:

   ```
   Adults form crews of up to 50, coordinate in encrypted chat, share a Library of Things, and take turns supporting each other. Donations fund the nonprofit platform.
   ```

3. **Description** (up to 4,000 characters). Paste the same text as Play, then edit tone if you want:

   ```
   Liberation Fleet helps adults (18+) form mutual-aid crews of up to 50 people, bound by crew bylaws and rules that the crew sets together.

   Within a crew, members use end-to-end encrypted group chat and shared spaces to coordinate. Crews can also join a larger fleet—functioning together while each crew keeps its own culture and rules.

   Mutual aid is inspired by rotating savings traditions (such as sou-sou): crewmates take turns receiving concentrated support from others. When someone has a spare dollar, they can choose to send it—on platforms like Venmo, PayPal, Cash App, or Zelle—to the person whose turn it is. Liberation Fleet helps organize and log that giving; it does not hold your money or process peer payments.

   Generosity still matters when money is not involved. Logging financial aid, goods, or services raises a priority score—so scarce Library of Things items and the next turn in the giving season favor those who give, while also accounting for emergencies, disability, and systemic vulnerability. The Library of Things lets crewmates share durable goods, consumables, services, and digital offerings without a cash price tag—access is shaped by need, availability, and priority.

   Crews are governed by the members: adding or removing crewmates and changing settings goes through time-limited democratic votes. Crewmates prioritize each other first; fleets let many crews cooperate without erasing what makes each crew distinct.

   Optional donations through the app support Liberation Fleet Co. (hosting and development of the nonprofit platform). Those donations are separate from crew mutual aid and are processed by Stripe—Liberation Fleet does not store your card details.
   ```

4. **Keywords** (100 characters, comma-separated, no space after commas). Do not repeat words already in the app name (`liberation`, `fleet`). Paste:

   ```
   mutual aid,crew,community,encrypted,chat,giving,library,nonprofit,solidarity,support
   ```

5. **Support URL** (required): `https://liberationfleet.org/how-to`  
   Open that URL in a private window and confirm it loads **without** signing in. If it does not, use another public HTTPS page that tells a user how to get help.
6. **Marketing URL** (optional): `https://liberationfleet.org`
7. **Version:** `1.0.0`. That value is already set in the Xcode project. If this field shows `1.0`, change it to `1.0.0` before you upload.
8. **Copyright:** `2026 Liberation Fleet Co.` — use the legal entity name and year.
9. **Routing App Coverage File:** leave empty.
10. Click **Save** (top right).

#### B. Screenshots (click-by-click)

Apple requires a **6.7-inch iPhone** set. This project is iPhone only, so there is no iPad screenshot set.

1. On the Mac, after the app is running in the Simulator (**2.7**, destination = a 6.7-inch iPhone):
   - Click the **Simulator** window (the fake phone), not the Xcode window.
   - Menu bar **File → Save Screen** (or **Cmd+S**). Files land on the Desktop.
   - Sign in as the demo account and repeat for each screen you need.
2. Capture at least: sign-in or home, a crew, chat, Library of Things, and the donation screen if you want reviewers to recognize it. Do not show another company’s trademarks as if they were yours.
3. Back in App Store Connect, on the version page, **iPhone** tab → **6.7" Display** → drag the PNGs into the well (or **Choose File**). Portrait only.
4. **Save**.

Accepted 6.7-inch pixel sizes include **1290×2796** and **1320×2868**. If upload rejects a size, recapture from a 6.7-inch simulator instead of resizing a phone photo.

There is no separate icon upload on this page. The store icon is the **1024×1024** image inside the Xcode asset catalog (`AppIcon`), with **no transparency and no rounded corners**.

#### C. App Information

1. Left side: **General → App Information** (older layout: **App Store → App Information**).
2. **Name** and **Subtitle**. Subtitle (30 characters). Paste:

   ```
   Mutual-aid crews for adults
   ```

3. **Category:**
   - **Primary:** Social Networking
   - **Secondary:** Lifestyle
4. **Content Rights:** check the box that you have the rights to the content in the app (your Terms of Use cover what members post).
5. **Age Rating:** click **Edit** / **Set Up Age Ratings** and complete **2.5**. Come back here if you opened it early.
6. **Save**.

#### D. Pricing and availability

1. Left side: **Pricing and Availability** (or **General → Pricing and Availability**).
2. **Price:** **Free** (price schedule **0 — Free**). Do not add In-App Purchases. Donations are Stripe Checkout, not Apple IAP.
3. **Availability:** click **Set Up Availability** / **Manage**.
   - For first launch, select **United States** only ([JURISDICTION-ASSUMPTIONS.md](./JURISDICTION-ASSUMPTIONS.md)).
   - Uncheck “available in all countries” if that is the default.
4. **Pre-order:** leave off.
5. **Save**.

#### E. App Review Information (on the version page)

Scroll to **App Review Information** on the **1.0** page.

1. **Sign-in required:** **Yes**.
2. **User name** and **Password:** the demo account from Step 0. It must work against **production**. Do not put MFA on this account unless you also paste a working code or backup path in Notes — reviewers will not call you for a code.
3. **Contact:** a first name, last name, phone, and email you actually read during the review window (usually 24–48 hours, sometimes longer).
4. **Notes:** paste the block in **2.10**.
5. **Save**.

#### F. Version release

On the same page, under **Version Release**:

1. Choose **Manually release this version**. After approval the app stays held until you click **Release This Version**, so you can install the approved build yourself first.
2. **Save**.

### 2.5 Age rating

1. **General → App Information → Age Ratings → Edit**, or the **Age Rating** link on the version page if that is where the dot is.
2. Answer from what the app actually does. Wording changes; the newer form uses **4+ / 9+ / 13+ / 16+ / 18+**, and older accounts still show **4+ / 9+ / 12+ / 17+**.
3. **Expected result:** the highest adult band the form offers (**18+** if that choice exists, otherwise **17+**). The product is 18+ only. If the calculator returns 4+, 9+, or 12+, you treated user-generated content as “none.” Go back and include it.

**First-party content** (screens and copy you ship): no violence, no gambling, no contests, no ads, no medical or clinical features. Disability level is a mutual-aid priority field, not health advice.

**User-generated content:** members can chat, post, and attach images, audio, and files. The app does not pre-screen end-to-end encrypted content. Members can report content and block other people. An adult-content preference can block, ask, or show mature material other members post.

| If the form asks | Suggested |
|------------------|-----------|
| Made for Kids / Designed for Children | **No** |
| Cartoon or fantasy violence, horror, gambling, contests, loot boxes, alcohol/tobacco/drug **features** | **None** (the app does not include these as features) |
| Profanity, mature themes, sexual content — when the question says to include **user-generated** content you do not filter | **Frequent** or the most intense option. Do not answer None |
| Unrestricted web access | **No**. The app is not a browser. Stripe Checkout opens the system browser for a donation page only |
| User-generated content, messaging, or chat | **Yes** |
| Users can see other users’ content | **Yes** |
| Advertising | **No** |
| Parental controls / age assurance | The signup flow requires the person to confirm they are **18+**. There is no Apple age-verification API and no government ID check. Do not claim a verified age-assurance system |
| In-app controls for sensitive content | **Yes** if asked — adult-content preference (block / ask / show), plus report and block |

4. **Save** / **Done**. Confirm the badge on App Information is **17+** or **18+**, not a child’s rating.

### 2.6 App Privacy (nutrition labels)

Aligned with the published Privacy Policy and with the Play **Data safety** answers in **1.2 C.1**. Confirm with counsel before you publish. Apple’s type names drift slightly.

1. Left side: **App Privacy** (or **Trust & Safety → App Privacy**).
2. Click **Get Started** / **Edit**.
3. **Do you or your third-party partners collect data from this app?** **Yes**.
4. **Data Used to Track You:** check **nothing**. Tracking means linking data with other companies’ data for advertising, or sharing with data brokers. Liberation Fleet does not do that. There is no ad SDK.
5. Select the data types below, then for **each** type set:
   - **Used for tracking:** No
   - **Linked to the user’s identity:** Yes (the data is tied to the account)
   - **Purposes:** **App Functionality** only, unless a row below adds another purpose
   - Do **not** check Third-Party Advertising, Developer’s Advertising or Marketing, or Analytics (no Google Analytics SDK; server logs that keep the app running are App Functionality, not Analytics)

| Apple type | Declare? | Notes |
|------------|----------|--------|
| **Contact Info → Name** | Yes | Username / display name |
| **Contact Info → Email Address** | Yes | Account email. Also check **Developer’s Advertising or Marketing** only if you send product news; password-reset mail is **App Functionality** |
| **Contact Info → Phone Number** | No | |
| **Contact Info → Physical Address** | No | Country and postal code are **Coarse Location**, not a street address |
| **Health & Fitness → Health / Fitness** | No | Do not declare clinical health data. Disability-level mutual-aid fields go under **Sensitive Info** |
| **Financial Info → Payment Info** | No | Card data stays with **Stripe** |
| **Financial Info → Other Financial Info** | Yes | Gift amounts, who-gave-whom, payment **handles** (Venmo and similar). Readable on the server — not E2EE |
| **Location → Precise Location** | No | No live GPS |
| **Location → Coarse Location** | Yes | Country and postal code for local crews and offerings |
| **Sensitive Info** | Yes | Optional identity-group tags (race, sexuality, and similar) and disability-level mutual-aid settings the user chooses |
| **Contacts** | No | |
| **User Content → Photos or Videos** | Yes | Avatars, chat, and library media. Usually E2EE ciphertext on the server |
| **User Content → Audio Data** | Yes | Audio attachments. Live voice is transported by **LiveKit** (not E2EE through Liberation Fleet’s content keys) |
| **User Content → Other User Content** (or Emails or Text Messages, if that is the chat bucket the form offers) | Yes | Chat, DMs, forums, proposals. Mostly **E2EE** ciphertext. Still **collected** — it leaves the device |
| **User Content → Customer Support** | Yes | Safety reports and related evidence |
| **Identifiers → User ID** | Yes | Account ids |
| **Identifiers → Device ID** | Yes | Auth tokens, remembered devices, APNs push token when push is on |
| **Purchases → Purchase History** | Yes | App donation amounts and Stripe session ids. **Shared** with Stripe for payment processing |
| **Usage Data → Product Interaction** | Yes | Server and request logs needed to run the app |
| **Diagnostics → Crash Data / Other Diagnostic Data** | Yes, if you keep server error logs | No third-party crash SDK is required for this answer |
| **Browsing History / Search History** | No | |

6. When a type asks whether it is shared with third parties, use the same split as Play:
   - Shown to other crewmates inside the product: still **collected** and linked. That is not a third-party SDK.
   - **Stripe** (donations): purchase history / donation metadata is processed by Stripe.
   - **LiveKit** (voice): audio is processed by the voice provider.
   - **Azure** hosting, email, and any report-moderation vendor: service providers for app functionality and safety.
7. **Publish** / **Save**. The App Privacy page should no longer say “Set Up.”
8. Privacy Policy URL, if a separate field is on **App Information** or the version page: `https://liberationfleet.org/privacy`. It must load over HTTPS with no login.

If Apple later rejects the labels, compare them to `liberationfleet.client/src/assets/privacy-policy.txt` sections 2–4.

### 2.7 Xcode — terminal, simulator, then archive

Xcode has no Terminal for these commands. The panel at the bottom of Xcode (**View → Debug Area → Activate Console**) only shows build and app logs. Type the `npm` commands in the Mac **Terminal** app.

#### A. Open Terminal and the project

1. Click the desktop so you are not typing in Xcode.
2. Press **Command (⌘) + Space**.
3. Type `Terminal` and press **Return**. A window with a text prompt opens. That is the Terminal.
4. Paste this and press **Return** (one line):
   ```bash
   cd /Users/kelliweaver/Documents/repos/LiberationFleet-master/liberationfleet.client
   ```
5. Install dependencies once on this Mac (skip if `npm install` already finished here):
   ```bash
   npm install
   ```
   Wait until it ends with no errors. If `npx cap add ios` prints **could not determine executable to run**, this step was skipped. `npx` then downloads an unrelated package named `cap`, which is not Capacitor.
6. If the `ios` folder does not exist yet, run:
   ```bash
   npx cap add ios
   ```
   If it says the ios platform already exists, that is fine. Continue.
7. Run:
   ```bash
   npm run cap:ios
   ```
   This builds the web app, copies it into the iOS project, and opens Xcode. The first run can take several minutes. Wait until the command finishes and Xcode is in front.
8. If Xcode shows the Welcome window instead of the project: menu **File → Open…** → go to `liberationfleet.client/ios/App/` → double-click **`App.xcworkspace`** (white icon). Do not open **`App.xcodeproj`** (blue icon) by itself.
9. If macOS asks to install additional components, or Xcode shows a license: click **Install** / **Agree** and wait. If it asks you to enable **Developer Mode**, turn it on in System Settings and restart if it says to.

#### B. Sign in and set the target

1. In the menu bar at the very top of the screen, click **Xcode → Settings…** (older Xcode: **Preferences…**).
2. In the left list of that Settings window, click **Apple Accounts** (older Xcode: **Accounts**).
3. If your Account Holder Apple ID is already in the list, you are signed in. The **+** button only adds another account. Skip it.
4. Close the Settings window. Click the red close button at its top left, or press **Cmd+W** while Settings is the front window. The project window is the larger window behind it. Signing controls are not inside Settings.
5. On the project window, look at the far left list. Ignore icon colors. The top line of text should be **App**, and under it names such as **AppDelegate.swift** and **Info.plist**. Click that top **App** line.
   - If the left list is **empty**, this window opened a workspace with no project in it. Menu **File → Open…**, press **Cmd+Shift+G**, paste  
     `/Users/kelliweaver/Documents/repos/LiberationFleet-master/liberationfleet.client/ios/App`  
     and press **Return**. Double-click **App.xcodeproj** (not **App.xcworkspace**). The left list should then show **App** at the top.
   - If the whole left sidebar is missing: menu **View → Navigators → Project** (or **Cmd+1**). The leftmost icon above the list looks like a folder. Click that icon if another list is showing.
6. The center of the window is now the project editor.
   - If you see a menu at the top of that editor, open it and choose the **App** target (the one that is not labeled Project).
   - If you see two columns labeled **PROJECT** and **TARGETS**, click **App** under **TARGETS**.
7. Click the **Signing & Capabilities** tab along the top of that editor.
8. If **Signing** is collapsed, click the triangle next to it. Check **Automatically manage signing**.
9. **Team:** choose your Apple Developer team. The bundle id row should show a provisioning profile. A red “No profiles” or “Failed to register bundle identifier” line means the membership is not active yet — finish the renewal in 2.1, then click **Try Again**.
10. These **General** and **Info** values are already in the project. You do not type them. If Xcode still has the project open from before this change, menu **File → Close Workspace**, then open `App.xcworkspace` again so it reloads.
    - **Display Name:** `Liberation Fleet`
    - **Bundle Identifier:** `com.liberationfleet.app`
    - **Version:** `1.0.0`
    - **Build:** `1`
    - **Supported Destinations:** iPhone only. iPad is already removed, so App Store Connect will not ask for iPad screenshots.
    - **Minimum Deployments:** `15.0`
    - **Privacy - Microphone Usage Description:** `Liberation Fleet needs the microphone for crew voice chat.`
11. **Push Notifications** and **Background Modes → Remote notifications** are already in the project. Do not add **Sign in with Apple**. On **Signing & Capabilities**, the Team must be your name **without** “(Personal Team)”. If that tab shows a red “No profiles” or “team has no devices” line, plug in a physical iPhone, unlock it, tap **Trust**, choose that iPhone in the destination menu, and click **Try Again**. A simulator cannot complete that step. After the profile exists, you can select the simulator again.

#### C. Run on the iOS Simulator

The Simulator is a separate app that looks like an iPhone. Xcode starts it when you press Run. You do not open it first.

1. Look at the top toolbar of the Xcode window, to the right of the play button. There is a destination control. It may say **Any iOS Device (arm64)** or an iPhone name.
2. Click that control.
3. Under **iOS Simulators**, click a **6.7-inch** phone: **iPhone 16 Pro Max**, **iPhone 17 Pro Max**, or whichever **Pro Max** or **Plus** is listed. That size matches the App Store screenshot slot.
4. If the menu has no iPhone simulators, or it says a runtime is not installed:
   1. Menu **Xcode → Settings… → Platforms** (older Xcode: **Components**).
   2. Find the latest **iOS** row → click **Get** / the download icon.
   3. Wait until the download finishes (several GB).
   4. Close Settings. Click the destination control again and pick the Pro Max simulator.
**iPhone Duo:** it is not in that list until Xcode **27.1** and the **iOS 27.1** simulator are installed. Menu **Xcode → About Xcode** must say 27.1 or newer. Then **Xcode → Settings… → Components** → **iOS 27.1** → **Get** or **Update**, and wait. Close Settings. Menu **Xcode → Open Developer Tool → Device Hub** → **+** at the top of the sidebar → under **Simulators** choose **iPhone Duo** → OS **iOS 27.1** → **Create**. It then appears in the destination control. Duo screenshots are optional until April 2027; a 6.7-inch iPhone set is still the one to upload for this release.

5. Click the **Run** triangle at the top left of the Xcode window, or menu **Product → Run**.
6. Watch the status at the top of the window: **Building…** then **Running App on iPhone…**. The first build often takes several minutes.
7. The Simulator window opens and Liberation Fleet should appear. If the phone is on the home screen, click the **Liberation Fleet** icon.
8. Sign in with the demo account and open a crew. This is the smoke test. Archive does not replace it.
9. If the build fails, open the log: menu **View → Debug Area → Activate Console** (or **Cmd+Shift+Y**). Read the red lines at the bottom. That pane is not a Terminal.

**Screenshots for 2.4 B:** click the Simulator window, then menu **File → Save Screen** (**Cmd+S**). Repeat on each screen. Files save to the Desktop.

**Physical iPhone (optional):** plug it in, unlock it, tap **Trust**. In the destination control, pick the phone under **iOS Devices** instead of a simulator, then press **Run**. If the phone says the developer is not trusted: on the iPhone, **Settings → General → VPN & Device Management** → your developer certificate → **Trust**, then press **Run** again.

#### D. Archive and upload

**Product → Archive** stays grayed out while a simulator is selected. Switch the destination first.

1. Click the destination control at the top of the Xcode window (it currently shows the iPhone simulator).
2. Choose **Any iOS Device (arm64)**. Do not pick an iPhone simulator.
3. Menu **Product → Archive**.
4. Wait until the build finishes. The **Organizer** window opens (if not: **Window → Organizer**).
5. Select the new archive (today’s date) → **Distribute App**.
6. **App Store Connect** → **Next**.
7. **Upload** → **Next**.
8. Leave **Upload your app’s symbols** and **Strip Swift symbols** checked. Leave **Manage Version and Build Number** unchecked if you already set **Build** in section B.
9. **Automatically manage signing** → **Next**.
10. Review the summary (bundle id `com.liberationfleet.app`, version, build) → **Upload**.
11. Wait for **Upload Successful**. A green check in Xcode only means Apple received the file. Processing comes next.

#### E. Export compliance (encryption)

The app uses HTTPS and standard end-to-end encryption (AES in the client). App Store Connect will ask about export compliance. Do not add `ITSAppUsesNonExemptEncryption` = `NO` to `Info.plist` just to skip the form — that claims an exemption you have not confirmed.

When the build shows **Missing Compliance** (TestFlight or the build picker):

1. Open the build → **Manage** / **Provide Export Compliance Information**.
2. If asked whether the app uses encryption: **Yes**.
3. If asked what kind: **standard encryption algorithms** (not proprietary). The app uses ordinary HTTPS plus standard AES. It does not implement a private cipher.
4. If asked whether you are offering the app in **France**: **No** while availability is United States only. Adding France later needs a separate French crypto declaration.
5. If asked whether the app qualifies for an exemption under Category 5 Part 2: stop and confirm with counsel before you check “exempt.” Many E2EE apps still file a year-end self-classification. Apple’s page is [Complying with Encryption Export Regulations](https://developer.apple.com/documentation/security/complying-with-encryption-export-regulations).
6. Save. The build should leave the “Missing Compliance” state. You cannot TestFlight or submit while that warning is up.

#### F. Wait until the build is selectable

1. App Store Connect → **TestFlight** (or the version page → **Build**).
2. The new build appears as **Processing** for a few minutes up to an hour or more. You will also get an email.
3. If an email says **Invalid Binary**, open it and fix that issue (icon alpha, missing usage string, Push entitlement mismatch). Bump **Build**, archive, and upload again.
4. When status is ready, continue to 2.8. You do not have to finish TestFlight before filling 2.4–2.6, but do TestFlight before **Submit for Review**.

### 2.8 TestFlight first

#### A. Internal testers (no Beta App Review)

Internal testers must be App Store Connect users on this team. The app installs through the TestFlight app, not the public App Store.

1. App Store Connect → **Users and Access** → **+**.
2. Enter the tester’s name and email. Role: **Developer**, **Marketing**, or **App Manager**. Access to this app: on.
3. They accept the invite email and turn on two-factor authentication if Apple requires it.
4. On the iPhone, App Store → install **TestFlight** → sign in with **that same Apple ID**.
5. App Store Connect → your app → **TestFlight** → **Internal Testing**.
6. If there is no group, click **+** to create one (or use the default internal group) and add those users.
7. Next to the build, answer **Missing Compliance** if you have not already (2.7 E).
8. Enable the build for the internal group (**Add Build** / the **+** on the group).
9. On the iPhone, open TestFlight → **Liberation Fleet** → **Install** / **Update**.
10. Smoke-test: sign in → crew → chat → attach an image → voice join → donation Checkout. Fix crashes before 2.9. Each fix is a new archive with a higher **Build** number.

#### B. External testers (optional — one Beta App Review)

Use this when people outside the App Store Connect team should install the beta.

1. **TestFlight → External Testing** → **+** to create a group (for example `Beta`).
2. **Add Build** → select the processed build.
3. Fill **Test Information**:
   - **Beta App Description:** one short paragraph of what the app does
   - **Feedback Email:** an inbox you read
   - **What to Test:** sign in, open a crew, send a chat, join voice
4. **Submit for Review**. The first external build is reviewed (shorter than a full App Store review, but it can still be rejected).
5. After it is approved, add tester emails on the group, or enable a **Public Link**.
6. Testers install **TestFlight**, accept the email or open the link, then install.

### 2.9 Submit for review

Do this only after an internal TestFlight smoke test passed on a physical iPhone, and 2.4–2.6 show no incomplete dots.

1. **Distribution → iOS App → 1.0 Prepare for Submission** (wording varies).
2. **Build** section → **Add Build** / **+** → select the TestFlight build you just smoked → **Done**. If the build is missing, it is still processing or still **Missing Compliance**.
3. Scroll the page and clear every remaining warning (screenshots, description, support URL, age rating, App Privacy, sign-in info).
4. **App Review Information → Notes** contains the 2.10 text, and the demo password still works.
5. Top right: **Add for Review** (or **Submit for Review**).
6. On the submission confirmation page, answer:
   - **Export compliance** — same answers as 2.7 E, if asked again
   - **Advertising Identifier (IDFA)** — **No**. This app does not use the advertising identifier
   - **Content rights** — you have rights to the content, including content members post under the Terms of Use
7. Click **Submit to App Review**.
8. Status moves to **Waiting for Review**, then **In Review**. Watch the contact email from step 2.4 E.
9. If Apple **rejects**: open **Resolution Center** on the version page, read the guideline number, reply or upload a new build. A new binary needs a higher **Build** number. Do not delete the version.
10. When status is **Pending Developer Release**, install that build yourself (TestFlight still has it, or release to yourself first). Then click **Release This Version**.
11. After it says **Ready for Sale**, install from the public App Store on a phone that is not only a TestFlight tester and run the same smoke test.

**If Submit stays disabled:** click the red dots on the version page. The usual blockers are a missing screenshot size, App Privacy not published, age rating unfinished, no build selected, or the Account Holder has a pending agreement under **Business**.

### 2.10 App Review notes (paste into Notes)

```
Demo account (email/password sign-in; no Sign in with Apple):
Email: <demo email>
Password: <demo password>

This account is already in a test crew. Please:
1. Sign in.
2. Open the crew.
3. Open chat and send a message.
4. Optional: join the crew voice channel (microphone permission) and leave.
5. Optional: open the donation screen. Donations are voluntary contributions to Liberation Fleet Co. for hosting and development. Checkout is Stripe in the system browser. They are not in-app purchases and they do not unlock features. Peer mutual aid is logged in the app and paid on Venmo, PayPal, Cash App, or Zelle — Liberation Fleet does not hold or transmit that money.

Message, post, and most media bodies are end-to-end encrypted. Reviewers can use the app’s own UI (chat, gift log, settings). Operators cannot read encrypted message bodies.

The app is for adults 18+. Members can report content and block other members. Community Standards: https://liberationfleet.org/community-standards
Privacy policy: https://liberationfleet.org/privacy
```

Confirm the donation wording against Guideline 3.1.1 and the nonprofit fundraising rules (3.2.1 / 3.2.2) with counsel before you submit. If the iOS binary should not offer Stripe donations until that review is done, remove or hide the donate entry on iOS and say so in the notes.

Sign in with Apple is not required while the only login method is email and password. If you later add Google or another third-party login, add Sign in with Apple in the same release.

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
