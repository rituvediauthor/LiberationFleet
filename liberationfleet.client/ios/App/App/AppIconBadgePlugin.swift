import Capacitor
import UIKit
import UserNotifications

@objc(AppIconBadgePlugin)
public class AppIconBadgePlugin: CAPPlugin, CAPBridgedPlugin {
    public let identifier = "AppIconBadgePlugin"
    public let jsName = "AppIconBadge"
    public let pluginMethods: [CAPPluginMethod] = [
        CAPPluginMethod(name: "set", returnType: CAPPluginReturnPromise)
    ]

    @objc func set(_ call: CAPPluginCall) {
        let count = max(0, call.getInt("count") ?? 0)
        DispatchQueue.main.async {
            if #available(iOS 16.0, *) {
                UNUserNotificationCenter.current().setBadgeCount(count) { _ in }
            } else {
                UIApplication.shared.applicationIconBadgeNumber = count
            }
        }
        call.resolve()
    }
}
