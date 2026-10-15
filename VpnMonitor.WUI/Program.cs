using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace VpnMonitor.WUI
{
    /// <summary>
    /// Keeps a single instance: the app lives in the tray, so a second launch only shows the window of the first one.
    /// Based on the Windows App SDK single-instancing sample.
    /// </summary>
    public static class Program
    {
        private const string InstanceKey = "VpnMonitor.WUI";

        [STAThread]
        private static int Main(string[] args)
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();

            if (RedirectToMainInstance())
                return 0;

            Application.Start(p =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });

            return 0;
        }

        private static bool RedirectToMainInstance()
        {
            var mainInstance = AppInstance.FindOrRegisterForKey(InstanceKey);
            if (mainInstance.IsCurrent)
            {
                mainInstance.Activated += (_, _) => App.ShowMainWindow();
                return false;
            }

            // The new process was started by the user and may pass the right to come to the foreground
            AllowSetForegroundWindow((int)mainInstance.ProcessId);

            // Waiting must not block the STA thread, otherwise the redirection deadlocks
            var redirected = CreateEvent(IntPtr.Zero, true, false, null);
            var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();

            Task.Run(() =>
            {
                mainInstance.RedirectActivationToAsync(activationArgs).AsTask().Wait();
                SetEvent(redirected);
            });

            _ = CoWaitForMultipleObjects(0, 0xFFFFFFFF, 1, new[] { redirected }, out _);

            return true;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string? lpName);

        [DllImport("kernel32.dll")]
        private static extern bool SetEvent(IntPtr hEvent);

        [DllImport("ole32.dll")]
        private static extern uint CoWaitForMultipleObjects(uint dwFlags, uint dwMilliseconds, ulong nHandles, IntPtr[] pHandles, out uint dwIndex);

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);
    }
}
