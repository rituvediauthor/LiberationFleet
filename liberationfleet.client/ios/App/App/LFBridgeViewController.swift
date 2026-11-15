import Capacitor
import UIKit

/// Registers the in-app AppIconBadge plugin. cap sync rewrites packageClassList
/// from npm plugins only, so this must stay in the App target.
///
/// Also mirrors Android's MainActivity inset injection: WebKit can report
/// env(safe-area-inset-*) as 0 on some poses (notably iPhone Duo outer/camera
/// rail), so we push UIKit safeAreaInsets into --lf-inset-* for CSS chrome.
@objc(LFBridgeViewController)
class LFBridgeViewController: CAPBridgeViewController {
    override func capacitorDidLoad() {
        bridge?.registerPluginInstance(AppIconBadgePlugin())
    }

    override func viewDidLayoutSubviews() {
        super.viewDidLayoutSubviews()
        injectSafeAreaInsetCss()
    }

    override func viewSafeAreaInsetsDidChange() {
        super.viewSafeAreaInsetsDidChange()
        injectSafeAreaInsetCss()
    }

    private func injectSafeAreaInsetCss() {
        guard let webView else {
            return
        }

        let insets = view.safeAreaInsets
        let js = """
        (function(){
          var r=document.documentElement;
          r.style.setProperty('--lf-inset-top','\(insets.top)px');
          r.style.setProperty('--lf-inset-right','\(insets.right)px');
          r.style.setProperty('--lf-inset-bottom','\(insets.bottom)px');
          r.style.setProperty('--lf-inset-left','\(insets.left)px');
          if(r.dataset){ delete r.dataset.lfDuoRailFallback; }
        })();
        """
        webView.evaluateJavaScript(js, completionHandler: nil)
    }
}
