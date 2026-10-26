import Capacitor
import UIKit

/// Registers the in-app AppIconBadge plugin. cap sync rewrites packageClassList
/// from npm plugins only, so this must stay in the App target.
@objc(LFBridgeViewController)
class LFBridgeViewController: CAPBridgeViewController {
    override func capacitorDidLoad() {
        bridge?.registerPluginInstance(AppIconBadgePlugin())
    }
}
