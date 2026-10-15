using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;
using System;

namespace VpnMonitor.WUI.Services
{
    /// <summary>
    /// Icon in the notification area: the app keeps running there after its window is closed.
    /// </summary>
    public sealed class TrayIconService : IDisposable
    {
        private const string Title = "VPN Monitor";

        private TaskbarIcon? _icon;

        public void Create(string iconPath, Action show, Action exit)
        {
            var menu = new MenuFlyout();
            menu.Items.Add(new MenuFlyoutItem { Text = "Открыть", Command = new RelayCommand(show) });
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(new MenuFlyoutItem { Text = "Выход", Command = new RelayCommand(exit) });

            _icon = new TaskbarIcon
            {
                ToolTipText = Title,
                Icon = new System.Drawing.Icon(iconPath),
                ContextMenuMode = ContextMenuMode.PopupMenu,
                ContextFlyout = menu,
                LeftClickCommand = new RelayCommand(show),
                NoLeftClickDelay = true,
            };

            // Efficiency mode would throttle the app, and with it the refresh timer and the notifications
            _icon.ForceCreate(enablesEfficiencyMode: false);
        }

        public void SetStatus(string status)
        {
            if (_icon != null)
                _icon.ToolTipText = $"{Title}\n{status}";
        }

        public void Dispose()
        {
            _icon?.Dispose();
            _icon = null;
        }
    }
}
