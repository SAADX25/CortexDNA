using System;
using System.Windows.Input;
using CortexDNA.Core;

namespace CortexDNA.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public PrivacyViewModel PrivacyVM { get; } = new PrivacyViewModel();
        public HardwareViewModel HardwareVM { get; } = new HardwareViewModel();

        private bool _runOnStartup;
        public bool RunOnStartup
        {
            get => _runOnStartup;
            set
            {
                if (SetProperty(ref _runOnStartup, value))
                {
                    SetStartupRegistry(value);
                }
            }
        }

        public MainViewModel()
        {
            _runOnStartup = CheckStartupRegistry();
        }

        private bool CheckStartupRegistry()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    return key?.GetValue("CortexDNA") != null;
                }
            }
            catch { return false; }
        }

        private void SetStartupRegistry(bool enable)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (enable)
                    {
                        string path = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                        if (path != null)
                        {
                            key.SetValue("CortexDNA", $"\"{path}\"");
                            Logger.Log($"Startup Enabled: {path}");
                        }
                    }
                    else
                    {
                        key.DeleteValue("CortexDNA", false);
                        Logger.Log("Startup Disabled");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Startup Registry Error: {ex.Message}");
            }
        }
    }
}
