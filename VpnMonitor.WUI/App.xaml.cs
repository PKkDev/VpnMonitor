using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using VpnMonitor.Data.Context;
using VpnMonitor.Data.Options;
using VpnMonitor.WUI.MVVM.View;
using VpnMonitor.WUI.MVVM.ViewModel;
using VpnMonitor.WUI.Services;

namespace VpnMonitor.WUI
{
    public partial class App : Application
    {
        public static Window MainWindow = null!;
        public static IHost Host = null!;

        private static DispatcherQueue? _dispatcherQueue;
        private static bool _exiting;

        private UIElement? _shell { get; set; }

        private static string IconPath => Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");

        public App()
        {
            InitializeComponent();

            var builder = Microsoft.Extensions.Hosting.Host.CreateEmptyApplicationBuilder(null);

            builder.Environment.ContentRootPath = AppContext.BaseDirectory;

            builder.Configuration.Sources.Clear();
            builder.Configuration.AddJsonFile("appsettings.Private.json", optional: false);

            builder.Services.Configure<DataBaseOption>(builder.Configuration.GetSection("NpgSql"));
            var connect = builder.Configuration.GetSection("NpgSql").GetValue<string>("ConnectionString");

            // A context per load: pages and the background refresh read the database at the same time
            builder.Services.AddDbContextFactory<DataContext>(options => options.UseNpgsql(connect));

            builder.Services.AddTransient<ShellView>();
            builder.Services.AddTransient<ShellViewModel>();

            builder.Services.AddTransient<ActualClientsView>();
            builder.Services.AddTransient<ActualClientsViewModel>();
            builder.Services.AddTransient<ClientDetailsViewModel>();
            builder.Services.AddTransient<TrafficStatisticsViewModel>();
            builder.Services.AddTransient<ServerOverviewViewModel>();

            builder.Services.AddSingleton<NavigationHelperService>();
            builder.Services.AddSingleton<InfoBarService>();
            builder.Services.AddSingleton<NotificationService>();
            builder.Services.AddSingleton<TrayIconService>();
            builder.Services.AddSingleton<ClientStatusService>();

            Host = builder.Build();
        }

        public static T GetService<T>() where T : class
        {
            if (App.Host.Services.GetService(typeof(T)) is not T service)
                throw new ArgumentException($"{typeof(T)} is not registered in app");

            return service;
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            MainWindow = new MainWindow();

            _shell = App.GetService<ShellView>();
            MainWindow.Content = _shell ?? new Frame();

            SetWindowSize(MainWindow);
            MainWindow.AppWindow.SetIcon(IconPath);

            // Closing the window keeps the app in the tray: monitoring and notifications go on
            MainWindow.AppWindow.Closing += (sender, e) =>
            {
                if (_exiting)
                    return;

                e.Cancel = true;
                sender.Hide();
            };

            var notifications = GetService<NotificationService>();
            notifications.Invoked += ShowMainWindow;
            notifications.Register();

            var tray = GetService<TrayIconService>();
            tray.Create(IconPath, ShowMainWindow, ExitApplication);

            var clientStatus = GetService<ClientStatusService>();
            clientStatus.StatusChanged += tray.SetStatus;
            clientStatus.Start();

            MainWindow.Activate();
        }

        /// <summary>Can be called from any thread: by the tray, a notification or a second launch of the app.</summary>
        public static void ShowMainWindow()
        {
            _dispatcherQueue?.TryEnqueue(() =>
            {
                if (MainWindow == null)
                    return;

                MainWindow.AppWindow.Show();
                MainWindow.Activate();
            });
        }

        private static void ExitApplication()
        {
            _dispatcherQueue?.TryEnqueue(() =>
            {
                _exiting = true;

                GetService<TrayIconService>().Dispose();
                GetService<NotificationService>().Unregister();

                MainWindow.Close();
                Current.Exit();
            });
        }

        private void SetWindowSize(Window mainWindow)
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            // Wide enough for all the menu items in the top navigation
            appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 720, Height = 760 });
        }
    }
}
