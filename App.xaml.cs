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
    private static EventWaitHandle? _eventWaitHandle = null;

    protected override void OnStartup(StartupEventArgs e)
    {
        const string mutexName = "Global\\CortexDNA_Mutex";
        const string eventName = "Global\\CortexDNA_Signal";

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
        Task.Run(() =>
        {
            while (true)
            {
                _eventWaitHandle.WaitOne();
                Dispatcher.Invoke(() =>
                {
                    var mw = System.Windows.Application.Current.MainWindow;
                    if (mw != null)
                    {
                        if (mw.WindowState == WindowState.Minimized)
                        {
                            mw.Show(); // Ensure it's visible (handling the tray case)
                            mw.WindowState = WindowState.Normal;
                        }
                        
                        // Handle the case where it's just hidden (Tray only) but not minimized state-wise
                        if (!mw.IsVisible)
                        {
                            mw.Show();
                        }

                        mw.Activate();
                        mw.Topmost = true;  // Temporarily force top
                        mw.Topmost = false;
                        mw.Focus();
                    }
                });
            }
        });

        // 2. Maximum Stability: Force Software Rendering
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        
        // Log Startup
        Logger.Log("Application Starting (v1.0.9) - RenderMode: SoftwareOnly");

        base.OnStartup(e);

        // Global Exception Handling
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            Logger.Log(args.ExceptionObject as Exception);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            Logger.Log(args.Exception);
            args.Handled = true; // Prevent crash if possible
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
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

