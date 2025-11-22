using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using VpnMonitor.Data.Options;
using VpnMonitor.WUI.MVVM.View;
using VpnMonitor.WUI.MVVM.ViewModel;
using VpnMonitor.WUI.Services;
using Microsoft.Extensions.Configuration;
using VpnMonitor.Data.Context;

namespace VpnMonitor.WUI
{
    public partial class App : Application
    {
        public static Window MainWindow;
        public static IHost Host;

        private UIElement? _shell { get; set; }

        public App()
        {
            InitializeComponent();

            var builder = Microsoft.Extensions.Hosting.Host.CreateEmptyApplicationBuilder(null);

            builder.Environment.ContentRootPath = AppContext.BaseDirectory;

            builder.Configuration.Sources.Clear();
            builder.Configuration.AddJsonFile("appsettings.Private.json", optional: false);

            builder.Services.Configure<DataBaseOption>(builder.Configuration.GetSection("NpgSql"));
            var connect = builder.Configuration.GetSection("NpgSql").GetValue<string>("ConnectionString");
            builder.Services.AddNpgsql<DataContext>(connect);

            builder.Services.AddTransient<ShellView>();
            builder.Services.AddTransient<ShellViewModel>();

            builder.Services.AddTransient<ActualClientsView>();
            builder.Services.AddTransient<ActualClientsViewModel>();

            builder.Services.AddSingleton<NavigationHelperService>();
            builder.Services.AddSingleton<InfoBarService>();

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
            MainWindow = new MainWindow();

            _shell = App.GetService<ShellView>();
            MainWindow.Content = _shell ?? new Frame();

            SetWindowSize(MainWindow);

            MainWindow.Activate();
        }

        private void SetWindowSize(Window mainWindow)
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 500, Height = 700 });
        }
    }
}
