using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Threading;
using System.Diagnostics;
using CortexDNA.Core;

namespace CortexDNA;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static Mutex? _mutex = null;
    private RegisteredWaitHandle? _signalRegistration;
    private static EventWaitHandle? _eventWaitHandle = null;

    protected override void OnStartup(StartupEventArgs e)
    {
        string scope = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
        string mutexName = "Local\\CortexDNA_Mutex_" + scope;
        string eventName = "Local\\CortexDNA_Signal_" + scope;

        bool createdNew;
        _mutex = new Mutex(true, mutexName, out createdNew);
        _eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);

        if (!createdNew)
        {
            // App is already running!
            // Signal the existing instance to bring itself to front
            _eventWaitHandle.Set();
            Shutdown();
            return;
        }

        // Start a thread to listen for signals from subsequent instances
        _signalRegistration = ThreadPool.RegisterWaitForSingleObject(_eventWaitHandle, (_, _) =>
        {
            if (Dispatcher.HasShutdownStarted) return;
            Dispatcher.BeginInvoke(() =>
            {
                if (Dispatcher.HasShutdownStarted) return;
                var window = MainWindow;
                if (window == null) return;
                window.Show();
                window.WindowState = WindowState.Normal;
                window.Activate();
            });
        }, null, Timeout.Infinite, false);
        // Use GPU/hardware rendering for smooth UI (SoftwareOnly caused constant lag with shadows)
        RenderOptions.ProcessRenderMode = RenderMode.Default;
        
        // Log Startup
        Logger.Log("Application Starting (v2.1.0) - RenderMode: Default");

        base.OnStartup(e);

        // Global Exception Handling
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            Logger.Log(args.ExceptionObject as Exception);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            Logger.Log(args.Exception);
            // Leave unexpected UI failures unhandled; suppressing them can retain corrupt state.
        };
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        base.OnSessionEnding(e);
        if (MainWindow is CortexDNA.MainWindow window)
        {
            // Session shutdown bypasses cancellable Window.Closing. Defer it and use
            // the existing asynchronous exit path so service restoration can complete.
            e.Cancel = true;
            window.RequestExit();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _signalRegistration?.Unregister(null);
        if (_mutex != null)
        {
            _mutex.Dispose();
        }
        if (_eventWaitHandle != null)
        {
            _eventWaitHandle.Dispose();
        }
        base.OnExit(e);
    }
}

