# Native iOS & Android (Capacitor) — click-by-click

Liberation Fleet ships three clients from one Angular codebase:

| Surface | How it runs |
|---------|-------------|
| **Web** | Same-origin SPA inside the ASP.NET container (Azure App Service) |
| **iOS** | Capacitor shell → App Store (**Mac required**) |
| **Android** | Capacitor shell → Google Play (Windows, Mac, or Linux) |

Related: [AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md), [STORE-SUBMISSION.md](./STORE-SUBMISSION.md), [LAUNCH-CHECKLIST.md](./LAUNCH-CHECKLIST.md).

Bundle / application ID: **`com.liberationfleet.app`**

**Where you are:** If `liberationfleet.client/android` already exists, skip **Step 4** (do not re-add the platform). Go to **Step 5**.

---

## Prerequisites (do this once)

### A. Node.js

1. Open a browser → go to [https://nodejs.org](https://nodejs.org).
2. Download the **LTS** installer for your OS.
3. Run the installer → accept defaults → finish.
4. Open **Terminal** (Mac) or **PowerShell** (Windows).
5. Run:
   ```bash
   node -v
   ```
6. Confirm you see `v20` or higher.

### B. Android Studio (Android only — Windows / Mac / Linux)

1. Open a browser → go to [https://developer.android.com/studio](https://developer.android.com/studio).
2. Click **Download Android Studio**.
3. Accept the terms → download the installer.
4. Run the installer → **Next** through defaults (include Android SDK).
5. Launch **Android Studio**.
6. If the **Setup Wizard** appears:
   1. Click **Next**.
   2. Choose **Standard** → **Next**.
   3. Pick a UI theme → **Next**.
   4. Click **Finish** and wait for components to download.
7. When the welcome screen appears, you are done for now (leave it open or quit).

Optional — create an emulator now (or do this later in Step 6):

1. On the Android Studio welcome screen, click **More Actions** → **Virtual Device Manager**  
   (or from an open project: **Tools** → **Device Manager**).
2. Click **Create Device**.
3. Pick a phone (e.g. **Pixel 7**) → **Next**.
4. Download a system image if needed (e.g. latest **API 35** or **36**) → wait → **Next**.
5. Click **Finish**.

### C. Xcode + CocoaPods (iOS only — Mac)

1. Open the **App Store** on your Mac.
2. Search **Xcode** → click **Get** / **Install**.
3. Wait for the large download to finish.
4. Open **Xcode** once → accept the license → install extra components when prompted.
5. Open **Terminal**.
6. Run:
   ```bash
   sudo gem install cocoapods
   ```
7. Enter your Mac password when asked → wait until it finishes.

### D. Backend API

You need a live HTTPS API (staging or production). Follow [AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md) if you do not have one yet.

---

## Step 1 — Install JS dependencies

1. Open **PowerShell** (Windows) or **Terminal** (Mac).
2. Go to the client folder:
   ```bash
   cd path\to\LiberationFleet\liberationfleet.client
   ```
   Example on this machine:
   ```bash
   cd C:\Users\james\source\repos\LiberationFleet\liberationfleet.client
   ```
3. Run:
   ```bash
   npm install
   ```
4. Wait until the command finishes with no errors.

---

## Step 2 — Point native builds at your API

1. In Cursor / VS Code, open the file:
   `liberationfleet.client/src/environments/environment.native.ts`
2. Find the line:
   ```ts
   apiBaseUrl: '...'
   ```
3. Set it to your real HTTPS API origin (no trailing slash), for example:
   ```ts
   apiBaseUrl: 'https://app-lfleet-production.azurewebsites.net'
   ```
   or your custom domain:
   ```ts
   apiBaseUrl: 'https://your.domain'
   ```
4. Save the file (**Ctrl+S** / **Cmd+S**).
5. Confirm it is **not** still `REPLACE_WITH_YOUR_API_ORIGIN`.

How it works:

1. `apiBaseUrl` is the Azure (or custom domain) origin.
2. `ApiBaseUrlInterceptor` rewrites `/api/...` to that origin.
3. SignalR hubs use `ApiUrlService.resolveHub(...)`.
4. CORS on the API must allow Capacitor origins (Terraform already sets these on App Service):
   - `capacitor://localhost`
   - `ionic://localhost`
   - `https://localhost` / `http://localhost`
5. If you use a **custom web domain**, also add `Cors__AllowedOrigins__N` = `https://your.domain` on App Service — [AZURE-GO-LIVE Step 10](./AZURE-GO-LIVE.md#step-10--custom-domain--tls).

---

## Step 3 — Build the native web assets

1. In the same terminal, still inside `liberationfleet.client`.
2. Run:
   ```bash
   npm run build:native
   ```
3. Wait until you see **Application bundle generation complete**.
4. If the build fails on **budget** errors, fix those first (CSS/bundle size), then re-run this step.

---

## Step 4 — Add native platforms (one time only)

### Android

1. In the terminal (`liberationfleet.client`), run:
   ```bash
   npx cap add android
   ```
2. If you see **`android platform already exists`**:
   - **Do nothing.** The platform is already there. Skip to Step 5.
   - Do **not** delete `android/` unless you intend to wipe all Android customizations.
3. If it succeeds, you should see a new `liberationfleet.client/android` folder.

### iOS (Mac only)

1. On a Mac, in the terminal (`liberationfleet.client`), run:
   ```bash
   npx cap add ios
   ```
2. Same rule: if it says the platform already exists, skip re-adding.
3. Commit `android/` and `ios/` when they are new (usual Capacitor practice).

---

## Step 5 — Sync after every Angular or env change

Do this whenever you change Angular code or `environment.native.ts`.

1. Terminal → `liberationfleet.client`.
2. Run:
   ```bash
   npm run cap:sync
   ```
3. This will:
   1. Run `build:native`.
   2. Copy the web build into the native projects.
   3. Update Capacitor plugins.
4. Wait until it finishes without errors.

Shortcut that also opens the IDE: use Step 6 commands (`cap:android` / `cap:ios`), which run sync first.

---

## Step 6 — Open the IDE and run on a device / emulator

### Android (click-by-click)

**Important:** An emulator that is “running” is only the fake phone. The Liberation Fleet app does **not** install until Gradle finishes and you click **Run** in Android Studio.

#### 6a. Point Gradle at JDK 17 (required once on this machine)

Your Windows `JAVA_HOME` may still point at an old JDK 9. Android Gradle Plugin 8.x needs **JDK 17**. Android Studio’s bundled JDK may be too new (e.g. 25) for Gradle 8.2.

1. Install Temurin 17 if needed (PowerShell as yourself):
   ```powershell
   winget install --id EclipseAdoptium.Temurin.17.JDK -e
   ```
2. Typical install path:
   `C:\Program Files\Eclipse Adoptium\jdk-17.0.xx.x-hotspot`
3. In **Android Studio**:
   1. **File** → **Settings** (or **Ctrl+Alt+S**).
   2. **Build, Execution, Deployment** → **Build Tools** → **Gradle**.
   3. **Gradle JDK** → click the dropdown → **Add JDK…** / **Download JDK…** if 17 is not listed.
   4. Select the Temurin **17** folder → **OK**.
   5. Click **Apply** → **OK**.
4. Click **File** → **Sync Project with Gradle Files** (elephant icon).
5. Wait until the bottom status bar finishes with no red errors. Open **View** → **Tool Windows** → **Build** if something failed.

#### 6b. Open and run

1. Terminal → `liberationfleet.client`.
2. Run:
   ```bash
   npm run cap:android
   ```
3. Wait for **Android Studio** to open the `android` project (not the repo root).
4. If a **Trust Project** / Gradle prompt appears → click **Trust Project** / **OK**.
5. Confirm Gradle JDK is 17 (see 6a). Wait for sync to finish.
6. At the top toolbar, find the device dropdown (emulator name or **No Devices**).
7. Click that dropdown:
   - **Physical phone:** plug in USB → enable Developer options + USB debugging → accept the RSA prompt → select the phone.
   - **Emulator:** pick your AVD. If the emulator window is open but missing from the list, wait until `adb devices` shows `device` (not `offline`).
8. Click the green **Run** triangle (or **Shift+F10**).
9. Watch **Build** at the bottom. First build can take several minutes. Success looks like **Install successfully finished** / app opens on the emulator.
10. Look for the **Liberation Fleet** icon on the emulator home screen / app drawer if it installed but did not auto-launch.
11. In the app, open **Sign in** and confirm login works against `apiBaseUrl`.

### iOS (Mac only — click-by-click)

1. Terminal → `liberationfleet.client`.
2. Run:
   ```bash
   npm run cap:ios
   ```
3. Wait for **Xcode** to open the `ios/App` workspace.
4. In the left Project Navigator, click the blue **App** project.
5. Under **TARGETS**, click **App**.
6. Open the **Signing & Capabilities** tab.
7. Check **Automatically manage signing**.
8. Click the **Team** dropdown → select your Apple Developer team (or add an account under **Xcode** → **Settings** → **Accounts** first).
9. At the top, click the destination control (device / simulator name).
10. Pick an iPhone simulator or a plugged-in physical iPhone.
11. Click the **Play** (Run) button, or press **Cmd+R**.
12. On a physical device, if iOS says the developer is untrusted:
    1. On the iPhone: **Settings** → **General** → **VPN & Device Management** (wording varies by iOS version).
    2. Tap your developer certificate → **Trust**.
    3. Re-run from Xcode.
13. Confirm sign-in works against `apiBaseUrl`.

---

## Step 7 — Permissions (voice / media)

| Feature | iOS (`Info.plist`) | Android (`AndroidManifest.xml`) |
|---------|--------------------|----------------------------------|
| Network | ATS HTTPS (default) | `INTERNET`, `ACCESS_NETWORK_STATE` |
| Microphone (voice + audio notes) | `NSMicrophoneUsageDescription` | `RECORD_AUDIO`, `MODIFY_AUDIO_SETTINGS` |
| Bluetooth headset (voice) | (system) | `BLUETOOTH` (≤API 30), `BLUETOOTH_CONNECT` |
| Camera | only if you add capture | **Do not declare** until used |
| Photos / files | `NSPhotoLibraryUsageDescription` if needed | System file picker — **no** `READ_MEDIA_*` unless you bypass the picker |

### Android — current manifest permissions

Open `android/app/src/main/AndroidManifest.xml`. You should see:

- `INTERNET` + `ACCESS_NETWORK_STATE`
- `RECORD_AUDIO` + `MODIFY_AUDIO_SETTINGS`
- `BLUETOOTH` (`maxSdkVersion="30"`) + `BLUETOOTH_CONNECT`
- `uses-feature` microphone / bluetooth with `android:required="false"`

Save → rebuild / **Run**. On first mic use, tap **Allow**. Bluetooth headset may prompt for nearby-devices / Bluetooth on Android 12+.

### iOS — add mic usage string (if missing)

1. In Xcode, open `Info.plist` (or **App** target → **Info** tab).
2. Add key **Privacy - Microphone Usage Description**.
3. Set the value to something like:
   > Liberation Fleet needs the microphone for crew voice chat.
4. Save → re-run.

LiveKit WebRTC works inside the Capacitor WebView when mic permission is granted. See [LIVEKIT-SETUP.md](./LIVEKIT-SETUP.md).

---

## Step 8 — Store assets you must supply

| Asset | iOS | Android |
|-------|-----|---------|
| App icon | 1024×1024 App Store | Adaptive icon (foreground/background) |
| Splash | Launch screen / Cap splash | Splash theme |
| Screenshots | 6.7", 6.1", iPad if supported | Phone (+ tablets if you declare them) |
| Privacy policy URL | Required | Required |
| Support URL | Required | Required |

Use `@capacitor/assets` or IDE asset catalogs once branding art is final. Submission flow: [STORE-SUBMISSION.md](./STORE-SUBMISSION.md).

---

## Stripe donations on mobile

Stripe Checkout opens an external browser / Custom Tabs. Frame donations as **voluntary tips to Liberation Fleet**, not unlockable digital goods. Confirm with counsel before store submission (Apple/Google payment rules). Setup: [DONATION-SETUP.md](./DONATION-SETUP.md).

---

## Push notifications (background — APNs + FCM)

In-app alerts use **SignalR** while the app is open. Background OS push is wired in code; you still must create Apple/Firebase credentials and plug them into Key Vault.

### What the codebase already does

| Layer | Behavior |
|-------|----------|
| Client | `@capacitor/push-notifications` registers a token after login; `PUT /api/notifications/push-tokens`; unregisters on logout; taps open `actionUrl` |
| Server | Stores `DevicePushTokens`; on each `NotificationService.Notify*`, sends FCM HTTP v1 (Android) and/or APNs (iOS) when configured |
| Secrets | Key Vault `Push-FcmServiceAccountJson`, `Push-ApnsKeyP8`; app settings `Push__FcmProjectId`, `Push__ApnsKeyId`, `Push__ApnsTeamId`, `Push__ApnsBundleId`, `Push__ApnsUseSandbox` |

Until secrets are real (not `change-me` / empty), the sender **no-ops** — SignalR still works.

### Your setup steps (required)

#### A. Firebase (Android FCM)

Firebase and Google Cloud share one project. Creating the Firebase project **already created** the Cloud project — do **not** create a second Cloud project and try to name it after the Firebase project id.

##### A.1 Register the Android app and download config

1. Open [Firebase Console](https://console.firebase.google.com) (same Google account you will use in Cloud Console).
2. **Add project** (or open an existing one).
3. **Add app** → Android → package name **`com.liberationfleet.app`** → register.
4. Download **`google-services.json`** → save as  
   `liberationfleet.client/android/app/google-services.json`  
   (gitignored — do not commit).
5. Open that file and note **`project_info.project_id`** (example shape: `liberationfleet-2bea7`). You will need this id later.

##### A.2 “Add Firebase SDK” wizard (click through — do not edit Gradle)

Firebase shows a multi-step wizard with **Next** / **Previous** only (no Skip). Capacitor already wires Gradle for FCM.

1. On the Gradle syntax chooser, select **Groovy (`build.gradle`)** — not Kotlin DSL. This repo uses `android/build.gradle` and `android/app/build.gradle`.
2. **Do not** paste Firebase’s sample `plugins { … }`, BoM, or `firebase-analytics` into the project. Already present:
   - project `classpath 'com.google.gms:google-services:…'` in `android/build.gradle`
   - app plugin applied when `google-services.json` exists (`android/app/build.gradle`)
   - FCM via `@capacitor/push-notifications` (not Analytics)
3. Click **Next** through remaining steps until the Android app registration finishes.

##### A.3 Open the matching Google Cloud **project** (not the organization)

The Cloud Console project picker may show an **organization** (e.g. `liberationfleetco.org`) and other projects. Organization settings show **Organization name / Organization ID** — that is **not** Project info.

1. Open [Google Cloud Console](https://console.cloud.google.com) with the **same** Google account as Firebase.
2. Click the top **project picker**.
3. Do **not** stop on the organization row. Select a **project**, or search for the id from `google-services.json` (`project_info.project_id`).
4. Shortcut: Firebase → gear → **Project settings** → use **Open in Google Cloud** / the Project ID link if shown.
5. Confirm you are on a project: **IAM & Admin** → **Settings** (or Home dashboard **Project info**) shows **Project ID** matching `google-services.json`.  
   If you only see Organization name / Organization ID / Access Transparency, you are still on the **org** — go back to the picker and select the project.

##### A.4 Service account private key (server → FCM)

**A.4.1 Download the key from Firebase**

1. Firebase Console → your project → gear → **Project settings**.
2. Open the **Service accounts** tab.
3. Click **Generate new private key** → **Generate key** (confirm the warning).
4. Save the downloaded `.json` file somewhere safe (password manager / secure folder). You will paste its **entire contents** into Azure — do not commit it to git.

**A.4.2 Put the JSON in Azure Key Vault**

Do this for each environment you care about (typical names after Terraform):

| Environment | Key Vault | Web App |
|-------------|-----------|---------|
| Staging | `lfleetstagingkv` | `app-lfleet-staging` |
| Production | `lfleetproductionkv` | `app-lfleet-production` |

1. Open [Azure Portal](https://portal.azure.com).
2. Search bar → type the vault name (e.g. `lfleetproductionkv`) → open the **Key Vault**.
3. If you get access denied: **Access control (IAM)** → grant your user **Key Vault Secrets Officer**, wait a minute, refresh.
4. Left menu → **Objects** → **Secrets** (or **Secrets**).
5. Find **`Push-FcmServiceAccountJson`**:
   - **If it exists** (Terraform placeholder): click the secret name → **New version** → **Secret value** → open the Firebase JSON in Notepad → **Select All** → **Copy** → paste the **entire** JSON into the value box (must start with `{` and end with `}`) → **Create**.
   - **If it does not exist yet**: **+ Generate/Import** → **Name** = `Push-FcmServiceAccountJson` → paste the entire JSON as the value → **Create**.
6. Repeat for the other vault if you run both staging and production.

**A.4.3 Set the Firebase project id on the Web App**

This is a plain App Setting (not a Key Vault secret). Value = `project_info.project_id` from `google-services.json` (e.g. `liberationfleet-2bea7`).

**Portal (fastest for a one-time set):**

1. Portal → resource group (e.g. `rg-lfleet-production`) → Web App **`app-lfleet-production`** (or staging equivalent).
2. Left menu → **Settings** → **Environment variables** (older UI: **Configuration** → **Application settings**).
3. Search for **`Push__FcmProjectId`**.
4. If it exists: open it → set **Value** to the project id → **Apply**.
5. If it does not exist: **+ Add** → **Name** = `Push__FcmProjectId` → **Value** = the project id → **Apply**.
6. Click **Apply** / **Confirm** if the portal asks to save all settings.
7. Left menu → **Overview** → **Restart** → confirm. Wait until the app is running again.

**Durable / next Terraform apply:** also set in env tfvars so deploys do not wipe a portal-only value:

```hcl
push_fcm_project_id = "liberationfleet-2bea7"  # your real project_id
```

Then `terraform apply` for that environment (or let the pipeline apply). See [AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md) for apply flow.

**A.4.4 Sync and run the Android app**

1. Open **PowerShell** or **Terminal**.
2. Go to the client folder:
   ```bash
   cd path\to\LiberationFleet\liberationfleet.client
   ```
3. Run:
   ```bash
   npm run cap:sync
   ```
   Wait until sync finishes (builds native web assets and copies them into `android/`).
4. Open Android Studio (or run `npm run cap:android`).
5. Wait for Gradle sync → select your emulator/device → green **Run**.
6. Sign in on the device → when prompted, **Allow** notifications.

##### A.5 If key creation is blocked by organization policy

Error: *“Key creation is not allowed on this service account… restricted by organization policies.”*

Google Workspace / Cloud orgs often enforce **`iam.disableServiceAccountKeyCreation`**. You need **Organization Policy Administrator** (or an admin) on the org — project Owner alone is not enough.

**Preferred: not enforced for this one project**

1. Cloud Console → project picker → select the **organization** (e.g. `liberationfleetco.org`) if required for org policies, or open **IAM & Admin** → **Organization policies**.
2. Search **Disable service account key creation** (`iam.disableServiceAccountKeyCreation`).
3. **Manage policy** / **Edit**.
4. **Customize** / **Override parent’s policy** as needed.
5. Either:
   - Switch context to the **Firebase Cloud project** → same policy → **Override** → **Not enforced** → Save, or  
   - Add a rule: enforcement **Off** / not enforced, with a **condition** limited to that project’s resource hierarchy.
6. Wait a few minutes → retry Firebase **Generate new private key**.

**Alternatives**

- Ask the org admin to allow key creation for this project.
- Or create the Firebase project under a **personal Gmail** (no org key block), re-download `google-services.json`, and use that project id + service-account JSON in Key Vault.

Do **not** keep clicking Generate until the policy allows keys or you use an unrestricted project.

#### B. Apple APNs (iOS — Mac required)

1. [developer.apple.com](https://developer.apple.com) → **Certificates, Identifiers & Profiles** → **Keys** → **+** → enable **Apple Push Notifications service (APNs)** → download the **`.p8`** once.
2. Note **Key ID** and your **Team ID**.
3. Ensure App ID **`com.liberationfleet.app`** has **Push Notifications** enabled.
4. Paste `.p8` PEM contents into Key Vault **`Push-ApnsKeyP8`**.
5. Set `Push__ApnsKeyId`, `Push__ApnsTeamId`, `Push__ApnsBundleId=com.liberationfleet.app`.
6. **`Push__ApnsUseSandbox=true`** for debug/TestFlight; **`false`** for App Store production.
7. On a Mac: Xcode → App target → **Signing & Capabilities** → **+ Capability** → **Push Notifications** (and **Background Modes → Remote notifications** if prompted).
8. `npm run cap:sync` → run on a **physical iPhone** (simulator push is limited).

#### C. Terraform / Azure after first apply

1. `terraform apply` creates the Key Vault secret **placeholders**.
2. Portal / CLI: replace secret **values** (lifecycle ignores Terraform value changes).
3. Set `push_fcm_project_id`, `push_apns_key_id`, `push_apns_team_id` in env tfvars (or App Service Configuration).
4. Restart App Service.

#### D. Smoke test

1. Sign in on a device → allow notifications.
2. Confirm `DevicePushTokens` has a row for your user.
3. Background the app → trigger a notification from another account.
4. Tap the system notification → app opens the `actionUrl`.

Track checklist: [LAUNCH-CHECKLIST B.9](./LAUNCH-CHECKLIST.md). Also [AZURE-GO-LIVE §16.3](./AZURE-GO-LIVE.md#step-16--auth--safety-service-hookups-mfa-push-ncmec).

---

## Everyday update loop (after first setup)

1. Change Angular / `environment.native.ts`.
2. Terminal → `liberationfleet.client` → `npm run cap:android` (or `cap:ios` on Mac).
3. In the IDE, click **Run** again.

You do **not** need `npx cap add android` / `ios` again.

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `android platform already exists` | Expected if `android/` is present — skip add; run `npm run cap:sync` |
| Emulator runs but app never appears | Emulator ≠ install. In Android Studio click green **Run**. Also fix Gradle JDK (see Step 6a) — sync/build must succeed first |
| Gradle: compatible with Java 9 / `JAVA_HOME` is jdk-9 | Install Temurin 17; set **Gradle JDK** to 17 in Android Studio (Step 6a) |
| Gradle: Unsupported class file major version 69 | Gradle 8.2 cannot use JDK 25 — switch Gradle JDK to **17**, not Embedded JDK 25 |
| API calls fail on device | Wrong/empty `apiBaseUrl`; CORS missing Capacitor origins |
| SignalR disconnects | Same as above; WebSockets enabled on App Service (Terraform sets this) |
| Blank screen | Run `npm run build:native` then `npm run cap:sync` |
| Mic denied | Add usage strings / `RECORD_AUDIO` |
| Bottom nav / action bar under Android gesture or Home buttons | Native only: `html.lf-native-shell` enables `--lf-safe-bottom`; `MainActivity` injects `--lf-inset-*`. Web/PWA keeps bottom spacer off to avoid double home-indicator padding |
| iOS build fails on Windows | Use a Mac (or cloud Mac) for `cap add ios` / Archive |
| Login works on web, not device | Device hitting HTTP or old `apiBaseUrl`; re-sync after env change |
| Gradle sync forever / fails | **File** → **Sync Project with Gradle Files**; set Gradle JDK 17; check internet |
| No device in toolbar | Start an emulator in Device Manager, or plug in phone with USB debugging on |
| Firebase “Add SDK” only has Next/Previous | Select **Groovy**, then **Next** — do not edit Gradle (see § A.2) |
| Cloud Console shows Organization name, no Project info | You selected the **org** in the picker — select the Firebase **project** (§ A.3) |
| Project picker missing `liberationfleet-…` id | Search by id from `google-services.json`; confirm same Google account as Firebase |
| “Key creation is not allowed… organization policies” | Org policy blocks SA keys — § A.5 (project override or personal Firebase project) |
