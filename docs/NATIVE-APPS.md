# Native iOS & Android (Capacitor)

Liberation Fleet ships three clients from one Angular codebase:

| Surface | How it runs |
|---------|-------------|
| **Web** | Same-origin SPA inside the ASP.NET container (Azure App Service) |
| **iOS** | Capacitor shell → App Store |
| **Android** | Capacitor shell → Google Play |

## Prerequisites

- Node 20+
- For Android: Android Studio + JDK 17 + Android SDK
- For iOS: **macOS** + Xcode 15+ + CocoaPods (`sudo gem install cocoapods`)
- A deployed HTTPS API (Azure) with CORS allowing Capacitor origins

## One-time setup

```bash
cd liberationfleet.client
npm install

# Set your production API origin (required for native)
# Edit src/environments/environment.native.ts → apiBaseUrl
# Example: 'https://app-lfleet-production.azurewebsites.net'

npm run build:native
npx cap add android   # Windows or Mac
npx cap add ios       # Mac only
```

Commit the generated `android/` and `ios/` folders (industry default for Capacitor).

## Day-to-day build

```bash
cd liberationfleet.client
# After changing Angular code or apiBaseUrl:
npm run cap:sync

# Open native IDEs:
npm run cap:android   # Android Studio
npm run cap:ios       # Xcode (Mac)
```

## How the native app talks to Azure

1. `environment.native.ts` sets `apiBaseUrl` to the Azure HTTPS origin.
2. `ApiBaseUrlInterceptor` rewrites `/api/...` requests to that origin.
3. SignalR hubs (`/hubs/chat`, `/hubs/notifications`, `/hubs/voice`) use `ApiUrlService.resolveHub(...)`.
4. API CORS must include Capacitor origins (already in `appsettings.json`):
   - `capacitor://localhost`
   - `ionic://localhost`
   - `https://localhost` / `http://localhost`

Production Azure: also set `Cors__AllowedOrigins__N` to your **custom web domain** and keep the Capacitor origins.

## Store assets you must supply

| Asset | iOS | Android |
|-------|-----|---------|
| App icon | 1024×1024 App Store | Adaptive icon (foreground/background) |
| Splash | Launch screen storyboard / Cap splash | Splash theme |
| Screenshots | 6.7", 6.1", iPad if supported | Phone + 7" / 10" tablets if needed |
| Privacy policy URL | Required | Required |
| Support URL | Required | Required |

Use `@capacitor/assets` or Android Studio / Xcode asset catalogs once branding art is final.

## Capabilities to enable in native projects

| Feature | iOS (`Info.plist`) | Android (`AndroidManifest.xml`) |
|---------|--------------------|----------------------------------|
| Microphone (voice) | `NSMicrophoneUsageDescription` | `RECORD_AUDIO` |
| Camera (if you add capture) | `NSCameraUsageDescription` | `CAMERA` |
| Photo library | `NSPhotoLibraryUsageDescription` | Read media permissions (API 33+) |
| Network | ATS HTTPS (default) | Internet permission (default) |
| Background audio (optional) | Background Modes → Audio | Foreground service if needed |

LiveKit WebRTC works inside Capacitor WebView when mic permission is granted.

## Stripe donations on mobile

Stripe Checkout opens an external browser / Custom Tabs. That is allowed for **digital donations to your org** in many cases, but Apple/Google policies around payments are strict for digital goods. Keep donations as **voluntary tips to Liberation Fleet** (already the product intent) and avoid selling digital unlockables outside IAP. Confirm with counsel before submission.

## Push notifications (not yet wired)

In-app notifications use SignalR. Store apps will need **APNs + FCM** (or Azure Notification Hubs) for background push — tracked as a follow-up. See `docs/LAUNCH-CHECKLIST.md`.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| API calls fail on device | `apiBaseUrl` empty/wrong; CORS missing Capacitor origins |
| SignalR disconnects | Same as above; ensure WebSockets enabled on App Service |
| Blank screen | Run `npm run cap:sync` after Angular changes |
| Mic denied | Add usage strings / permissions above |
| iOS build fails on Windows | Use a Mac or cloud Mac CI for `cap add ios` / Archive |
