using Microsoft.Win32;
using CortexDNA.Models;

namespace CortexDNA.Core.Startup
{
    /// <summary>
    /// Enables/disables startup items using the same StartupApproved registry
    /// values Task Manager writes.
    /// </summary>
    public sealed class StartupApprovalService
    {
        public void ApplyState(IEnumerable<StartupItem> items)
        {
            foreach (var item in items)
            {
                try
                {
                    if (item.Location == StartupLocationKind.PackagedApp)
                        continue;

                    item.IsEnabled = IsEnabled(item);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Startup approval read failed ({item.Name}): {ex.Message}");
                    item.IsEnabled = true;
                }
            }
        }

        public bool IsEnabled(StartupItem item)
        {
            byte[]? user = ReadApproval(RegistryHive.CurrentUser, StartupPaths.ApprovalSubKey(item.Location), item.ApprovalValueName);
            if (user is { Length: > 0 })
                return IsApprovedEnabled(user);

            if (StartupPaths.Hive(item.Location) != RegistryHive.CurrentUser)
            {
                byte[]? machine = ReadApproval(RegistryHive.LocalMachine, StartupPaths.ApprovalSubKey(item.Location), item.ApprovalValueName);
                if (machine is { Length: > 0 })
                    return IsApprovedEnabled(machine);
            }

            return true;
        }

        internal static bool? ReadApprovedEnabled(params string[] names)
        {
            string[] subs =
            {
                StartupPaths.ApprovedRun,
                StartupPaths.ApprovedRun32,
                StartupPaths.ApprovedFolder
            };

            foreach (string name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                foreach (string sub in subs)
                {
                    byte[]? data = ReadApproval(RegistryHive.CurrentUser, sub, name);
                    if (data is { Length: > 0 })
                        return IsApprovedEnabled(data);
                }
            }

            return null;
        }

        private static bool IsApprovedEnabled(byte[] data) => data[0] is 0x02 or 0x06;

        public void SetEnabled(StartupItem item, bool enabled)
        {
            if (item.Location == StartupLocationKind.PackagedApp)
            {
                StartupPackagedCatalog.SetEnabled(item, enabled);
                return;
            }

            if (!item.CanModify)
                throw new InvalidOperationException("This startup item cannot be changed.");

            byte[] value = BuildApproval(enabled);
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            using var key = baseKey.CreateSubKey(StartupPaths.ApprovalSubKey(item.Location), true)
                ?? throw new InvalidOperationException("Could not open startup approval registry key.");

            key.SetValue(item.ApprovalValueName, value, RegistryValueKind.Binary);
            item.IsEnabled = enabled;
        }

        private static byte[]? ReadApproval(RegistryHive hive, string subKey, string valueName)
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(subKey);
            if (key == null) return null;
            return key.GetValue(valueName) as byte[];
        }

        private static byte[] BuildApproval(bool enabled)
        {
            var data = new byte[12];
            data[0] = enabled ? (byte)0x02 : (byte)0x03;
            long fileTime = DateTime.Now.ToFileTime();
            BitConverter.GetBytes(fileTime).CopyTo(data, 4);
            return data;
        }
    }
}
