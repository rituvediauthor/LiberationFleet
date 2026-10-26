package com.liberationfleet.app;

import android.app.NotificationManager;
import android.content.Context;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;

@CapacitorPlugin(name = "AppIconBadge")
public class AppIconBadgePlugin extends Plugin {

    @PluginMethod
    public void set(PluginCall call) {
        Integer countObj = call.getInt("count", 0);
        int count = countObj == null ? 0 : Math.max(0, countObj);
        if (count == 0) {
            NotificationManager manager =
                (NotificationManager) getContext().getSystemService(Context.NOTIFICATION_SERVICE);
            if (manager != null) {
                manager.cancelAll();
            }
        }
        call.resolve();
    }
}
