using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Diagnostics;

namespace VpnMonitor.WUI.Services
{
    /// <summary>
    /// Windows notifications (toasts) through the Windows App SDK.
    /// </summary>
    public class NotificationService
    {
        private bool _registered;

        /// <summary>Raised on a background thread when the user clicks a notification.</summary>
        public event Action? Invoked;

        public void Register()
        {
            try
            {
                // The handler must be attached before Register
                AppNotificationManager.Default.NotificationInvoked += (_, _) => Invoked?.Invoke();
                AppNotificationManager.Default.Register();
                _registered = true;
            }
            catch (Exception ex)
            {
                // The app keeps working without notifications
                Debug.WriteLine($"Notifications are not available: {ex.Message}");
            }
        }

        public void Show(string title, string text)
        {
            if (!_registered)
                return;

            var notification = new AppNotificationBuilder()
                .AddText(title)
                .AddText(text)
                .BuildNotification();

            AppNotificationManager.Default.Show(notification);
        }

        public void Unregister()
        {
            if (_registered)
                AppNotificationManager.Default.Unregister();
        }
    }
}
