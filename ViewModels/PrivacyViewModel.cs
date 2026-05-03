using System;
using System.Diagnostics;
using System.Windows.Input;
using Microsoft.Win32;
using CortexDNA.Core;

namespace CortexDNA.ViewModels
{
    public class PrivacyViewModel : ViewModelBase
    {
        public ICommand OpenPrivacyRegistryCommand { get; }

        public PrivacyViewModel()
        {
            OpenPrivacyRegistryCommand = new RelayCommand<string>(OpenPrivacyRegistry);

            // Notify UI of live privacy settings state on initialization
            OnPropertyChanged(nameof(PrivacyDiagnosticDataEnabled));
            OnPropertyChanged(nameof(PrivacySettingsSuggestionsEnabled));
            OnPropertyChanged(nameof(PrivacyWebSearchEnabled));
        }

        public bool PrivacyDiagnosticDataEnabled
        {
            get
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection"))
                    {
                        if (key != null)
                        {
                            var val = key.GetValue("AllowTelemetry");
                            if (val != null)
                                return Convert.ToInt32(val) > 0;
                        }
                        return true; // Default Windows state
                    }
                }
                catch
                {
                    return true;
                }
            }
            set
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", true))
                    {
                        if (key != null)
                        {
                            key.SetValue("AllowTelemetry", value ? 3 : 0, RegistryValueKind.DWord);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error setting PrivacyDiagnosticDataEnabled: {ex.Message}");
                }
                finally
                {
                    OnPropertyChanged(nameof(PrivacyDiagnosticDataEnabled));
                }
            }
        }

        public bool PrivacySettingsSuggestionsEnabled
        {
            get
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager"))
                    {
                        if (key != null)
                        {
                            var val = key.GetValue("SystemPaneSuggestionsEnabled");
                            if (val != null)
                                return Convert.ToInt32(val) != 0;
                        }
                        return true; // Default Windows state
                    }
                }
                catch
                {
                    return true;
                }
            }
            set
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", true))
                    {
                        if (key != null)
                        {
                            key.SetValue("SystemPaneSuggestionsEnabled", value ? 1 : 0, RegistryValueKind.DWord);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error setting PrivacySettingsSuggestionsEnabled: {ex.Message}");
                }
                finally
                {
                    OnPropertyChanged(nameof(PrivacySettingsSuggestionsEnabled));
                }
            }
        }

        public bool PrivacyWebSearchEnabled
        {
            get
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Policies\Microsoft\Windows\Explorer"))
                    {
                        if (key != null)
                        {
                            var val = key.GetValue("DisableSearchBoxSuggestions");
                            if (val != null)
                                return Convert.ToInt32(val) == 0;
                        }
                        return true; // Default Windows state
                    }
                }
                catch
                {
                    return true;
                }
            }
            set
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\Explorer", true))
                    {
                        if (key != null)
                        {
                            key.SetValue("DisableSearchBoxSuggestions", value ? 0 : 1, RegistryValueKind.DWord);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error setting PrivacyWebSearchEnabled: {ex.Message}");
                }
                finally
                {
                    OnPropertyChanged(nameof(PrivacyWebSearchEnabled));
                }
            }
        }

        private void OpenPrivacyRegistry(string? settingType)
        {
            if (string.IsNullOrEmpty(settingType)) return;

            string keyPath = "";
            switch (settingType)
            {
                case "Diagnostic":
                    keyPath = @"Computer\HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection";
                    break;
                case "Settings":
                    keyPath = @"Computer\HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
                    break;
                case "Search":
                    keyPath = @"Computer\HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Explorer";
                    break;
            }

            // 1. Re-verify setting status before opening
            OnPropertyChanged(nameof(PrivacyDiagnosticDataEnabled));
            OnPropertyChanged(nameof(PrivacySettingsSuggestionsEnabled));
            OnPropertyChanged(nameof(PrivacyWebSearchEnabled));

            // 2. Modify Regedit LastKey to jump straight to the key
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit", true))
                {
                    if (key != null)
                    {
                        key.SetValue("LastKey", keyPath, RegistryValueKind.String);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Could not set Regedit LastKey: {ex.Message}");
            }

            // 3. Open Regedit
            try
            {
                var psi = new ProcessStartInfo("regedit.exe")
                {
                    UseShellExecute = true
                };
                using (var process = Process.Start(psi))
                {
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Could not launch regedit: {ex.Message}");
            }
        }
    }
}
