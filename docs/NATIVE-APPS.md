# Native iOS & Android (Capacitor) — step-by-step

Liberation Fleet ships three clients from one Angular codebase:

| Surface | How it runs |
|---------|-------------|
| **Web** | Same-origin SPA inside the ASP.NET container (Azure App Service) |
| **iOS** | Capacitor shell → App Store |
| **Android** | Capacitor shell → Google Play |

Related: [AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md), [STORE-SUBMISSION.md](./STORE-SUBMISSION.md), [LAUNCH-CHECKLIST.md](./LAUNCH-CHECKLIST.md).

Bundle / application ID: **`com.liberationfleet.app`**

---

## Prerequisites

| Tool | Notes |
|------|--------|
| Node 20+ | `node -v` |
| npm | Comes with Node |
| Android | Android Studio + JDK 17 + Android SDK (Windows, macOS, or Linux) |
| iOS | **macOS** + Xcode 15+ + CocoaPods (`sudo gem install cocoapods`) |
| Backend | Deployed HTTPS API ([AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md)) with CORS for Capacitor origins |

---

## Step 1 — Install JS dependencies

```bash
cd liberationfleet.client
npm install
```

---

## Step 2 — Point native builds at your API

1. Open `liberationfleet.client/src/environments/environment.native.ts`.
2. Set:
   ```ts
   apiBaseUrl: 'https://your-production-or-staging-host'
   ```
   - No trailing slash.
   - Must be HTTPS in production.
   - Never ship the placeholder `REPLACE_WITH_YOUR_API_ORIGIN`.
3. Save the file.

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

```bash
cd liberationfleet.client
npm run build:native
```

---

## Step 4 — Add native platforms (one time)

```bash
cd liberationfleet.client
npx cap add android   # Windows, Mac, or Linux
npx cap add ios       # Mac only
```

Commit the generated `android/` and `ios/` folders (usual Capacitor practice).

---

## Step 5 — Sync after every Angular or env change

```bash
cd liberationfleet.client
npm run cap:sync
```

This copies the web build into the native projects and updates plugins.

---

## Step 6 — Open IDEs and run on a device

```bash
npm run cap:android   # opens Android Studio
npm run cap:ios       # opens Xcode (Mac)
```

### Android Studio

1. Wait for Gradle sync.
2. Select a physical device or emulator → **Run**.
3. Confirm login works against `apiBaseUrl`.

### Xcode

1. Select your Team for signing.
2. Pick a physical device (recommended) or simulator → **Run**.
3. Trust the developer cert on the device if prompted.

---

## Step 7 — Permissions (voice / media)

| Feature | iOS (`Info.plist`) | Android (`AndroidManifest.xml`) |
|---------|--------------------|----------------------------------|
| Microphone (voice) | `NSMicrophoneUsageDescription` | `RECORD_AUDIO` |
| Camera (if native capture later) | `NSCameraUsageDescription` | `CAMERA` |
| Photo library | `NSPhotoLibraryUsageDescription` | Read media permissions (API 33+) |
| Network | ATS HTTPS (default) | Internet (default) |

Example mic string:

> Liberation Fleet needs the microphone for crew voice chat.

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

## Push notifications (not yet wired)

In-app notifications use SignalR (works while the app is open). Background push needs **APNs + FCM** (or Azure Notification Hubs) — tracked in [LAUNCH-CHECKLIST.md](./LAUNCH-CHECKLIST.md).

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| API calls fail on device | Wrong/empty `apiBaseUrl`; CORS missing Capacitor origins |
| SignalR disconnects | Same as above; WebSockets enabled on App Service (Terraform sets this) |
| Blank screen | Run `npm run build:native` then `npm run cap:sync` |
| Mic denied | Add usage strings / `RECORD_AUDIO` |
| iOS build fails on Windows | Use a Mac or cloud Mac for `cap add ios` / Archive |
| Login works on web, not device | Device hitting HTTP or old `apiBaseUrl`; re-sync after env change |
