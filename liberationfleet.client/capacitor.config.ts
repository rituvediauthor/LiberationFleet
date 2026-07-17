import type { CapacitorConfig } from '@capacitor/cli';

/**
 * Capacitor wraps the Angular SPA for App Store / Google Play.
 * Build with: npm run build:native && npx cap sync
 *
 * Set the production API URL in src/environments/environment.native.ts
 * before shipping (apiBaseUrl). CORS on the API must allow Capacitor origins
 * (see docs/NATIVE-APPS.md).
 */
const config: CapacitorConfig = {
  appId: 'com.liberationfleet.app',
  appName: 'Liberation Fleet',
  webDir: 'dist/liberationfleet.client/browser',
  server: {
    // Leave androidScheme https for modern WebView cookie/CORS behavior.
    androidScheme: 'https',
    iosScheme: 'capacitor'
  },
  plugins: {
    SplashScreen: {
      launchAutoHide: true,
      backgroundColor: '#0b1220',
      showSpinner: false
    },
    StatusBar: {
      style: 'DARK',
      backgroundColor: '#0b1220'
    },
    Keyboard: {
      resize: 'body'
    }
  }
};

export default config;
