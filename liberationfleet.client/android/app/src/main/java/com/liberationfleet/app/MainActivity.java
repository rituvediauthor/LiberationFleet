package com.liberationfleet.app;

import android.os.Bundle;
import android.view.View;
import android.webkit.WebView;
import androidx.core.graphics.Insets;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowCompat;
import androidx.core.view.WindowInsetsCompat;
import com.getcapacitor.BridgeActivity;

/**
 * Capacitor host. On Android 15+ (targetSdk 35+), the WebView draws edge-to-edge;
 * Chromium often reports env(safe-area-inset-*) as 0px, so we inject --lf-inset-*
 * from real WindowInsets for bottom nav / action bars.
 */
public class MainActivity extends BridgeActivity {
    private Insets lastSystemInsets = Insets.NONE;

    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        WindowCompat.setDecorFitsSystemWindows(getWindow(), false);

        final View root = findViewById(android.R.id.content);
        ViewCompat.setOnApplyWindowInsetsListener(root, (v, windowInsets) -> {
            Insets bars = windowInsets.getInsets(
                WindowInsetsCompat.Type.systemBars() | WindowInsetsCompat.Type.displayCutout()
            );
            lastSystemInsets = bars;
            injectInsetCss(bars);
            return windowInsets;
        });
        ViewCompat.requestApplyInsets(root);
        root.post(this::reinjectInsetCss);
    }

    @Override
    public void onResume() {
        super.onResume();
        reinjectInsetCss();
    }

    private void reinjectInsetCss() {
        injectInsetCss(lastSystemInsets);
    }

    private void injectInsetCss(Insets bars) {
        if (getBridge() == null) {
            return;
        }
        WebView webView = getBridge().getWebView();
        if (webView == null) {
            return;
        }

        float density = getResources().getDisplayMetrics().density;
        if (density <= 0f) {
            density = 1f;
        }

        String top = (bars.top / density) + "px";
        String right = (bars.right / density) + "px";
        String bottom = (bars.bottom / density) + "px";
        String left = (bars.left / density) + "px";

        String js =
            "(function(){"
                + "var r=document.documentElement;"
                + "r.style.setProperty('--lf-inset-top','"
                + top
                + "');"
                + "r.style.setProperty('--lf-inset-right','"
                + right
                + "');"
                + "r.style.setProperty('--lf-inset-bottom','"
                + bottom
                + "');"
                + "r.style.setProperty('--lf-inset-left','"
                + left
                + "');"
                + "})();";

        webView.evaluateJavascript(js, null);
    }
}
