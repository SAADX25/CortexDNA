using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CortexDNA.Models;

namespace CortexDNA.Core
{
    /// <summary>
    /// Safe junk-file scanner/cleaner with selectable categories and progress.
    /// </summary>
    public sealed class DiskCleanupService
    {
        private static readonly HashSet<string> SkippedFilePrefixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "thumbcache_",
            "iconcache_"
        };

        public IReadOnlyList<CleanupLocationItem> CreateDefaultLocations()
        {
            string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            return new List<CleanupLocationItem>
            {
                new()
                {
                    Id = CleanupCategoryId.UserTemp,
                    Name = "User Temporary Files",
                    Path = Path.GetTempPath(),
                    IsSelected = true,
                    IsRecommended = true
                },
                new()
                {
                    Id = CleanupCategoryId.SystemTemp,
                    Name = "System Temp Folder",
                    Path = Path.Combine(systemRoot, "Temp"),
                    IsSelected = true,
                    IsRecommended = true
                },
                new()
                {
                    Id = CleanupCategoryId.Recent,
                    Name = "Recent Items",
                    Path = Environment.GetFolderPath(Environment.SpecialFolder.Recent),
                    IsSelected = true,
                    IsRecommended = true
                },
                new()
                {
                    Id = CleanupCategoryId.WerLogs,
                    Name = "Error Reporting Logs",
                    Path = Path.Combine(programData, @"Microsoft\Windows\WER"),
                    IsSelected = true,
                    IsRecommended = true
                },
                new()
                {
                    Id = CleanupCategoryId.Prefetch,
                    Name = "Windows Prefetch",
                    Path = Path.Combine(systemRoot, "Prefetch"),
                    IsSelected = false,
                    IsRecommended = false,
                    Warning = "May slow the next launch of some apps"
                },
                new()
                {
                    Id = CleanupCategoryId.WindowsUpdate,
                    Name = "Windows Update Cache",
                    Path = Path.Combine(systemRoot, @"SoftwareDistribution\Download"),
                    IsSelected = false,
                    IsRecommended = false,
                    RequiresUpdateServices = true,
                    Warning = "Temporarily stops Windows Update services"
                }
            };
        }

        public Task<CleanupScanResult> ScanAsync(
            IReadOnlyList<CleanupLocationItem> locations,
            IProgress<CleanupProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Scan(locations, progress, cancellationToken), cancellationToken);
        }

        public Task<CleanupCleanResult> CleanAsync(
            IReadOnlyList<CleanupLocationItem> selectedLocations,
            IProgress<CleanupProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Clean(selectedLocations, progress, cancellationToken), cancellationToken);
        }

        private CleanupScanResult Scan(
            IReadOnlyList<CleanupLocationItem> locations,
            IProgress<CleanupProgress>? progress,
            CancellationToken cancellationToken)
        {
            int totalFiles = 0;
            long totalBytes = 0;
            int index = 0;

            foreach (var location in locations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                index++;

                progress?.Report(new CleanupProgress
                {
                    Message = $"Scanning {location.Name}...",
                    CurrentLocation = location.Name,
                    Percent = (int)(index * 100.0 / Math.Max(1, locations.Count))
                });

                long locationBytes = 0;
                int locationFiles = 0;

                if (Directory.Exists(location.Path))
                {
                    try
                    {
                        var options = new EnumerationOptions
                        {
                            IgnoreInaccessible = true,
                            RecurseSubdirectories = true,
                            AttributesToSkip = 0,
                            ReturnSpecialDirectories = false
                        };

                        foreach (var file in Directory.EnumerateFiles(location.Path, "*", options))
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (ShouldSkipFile(file))
                                continue;

                            try
                            {
                                long size = new FileInfo(file).Length;
                                locationBytes += size;
                                locationFiles++;
                            }
                            catch
                            {
                                // Locked / inaccessible — skip
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Scan skipped '{location.Name}': {ex.Message}");
                    }
                }

                location.Bytes = locationBytes;
                location.FileCount = locationFiles;
                location.FormattedSize = FormatByteSize(locationBytes);
                totalBytes += locationBytes;
                totalFiles += locationFiles;
            }

            progress?.Report(new CleanupProgress
            {
                Message = "Scan complete",
                Percent = 100
            });

            return new CleanupScanResult
            {
                Locations = locations.ToList(),
                FileCount = totalFiles,
                TotalBytes = totalBytes
            };
        }

        private CleanupCleanResult Clean(
            IReadOnlyList<CleanupLocationItem> selectedLocations,
            IProgress<CleanupProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (selectedLocations == null || selectedLocations.Count == 0)
            {
                return new CleanupCleanResult { FreedBytes = 0, DeletedFiles = 0 };
            }

            long freedBytes = 0;
            int deletedFiles = 0;
            int failedFiles = 0;
            bool stoppedServices = false;

            try
            {
                if (selectedLocations.Any(l => l.RequiresUpdateServices))
                {
                    progress?.Report(new CleanupProgress
                    {
                        Message = "Stopping Windows Update services...",
                        Percent = 2
                    });
                    stoppedServices = StopUpdateServices();
                }

                string recentPath = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
                int index = 0;

                foreach (var location in selectedLocations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    index++;

                    int percent = 5 + (int)(index * 90.0 / Math.Max(1, selectedLocations.Count));
                    progress?.Report(new CleanupProgress
                    {
                        Message = $"Cleaning {location.Name}...",
                        CurrentLocation = location.Name,
                        Percent = Math.Min(95, percent)
                    });

                    if (!Directory.Exists(location.Path))
                        continue;

                    bool isRecentFolder = string.Equals(location.Path, recentPath, StringComparison.OrdinalIgnoreCase);

                    try
                    {
                        // Top-level files
                        foreach (var file in SafeGetFiles(location.Path))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (TryDeleteFile(file, out long size))
                            {
                                freedBytes += size;
                                deletedFiles++;
                            }
                            else
                            {
                                failedFiles++;
                            }
                        }

                        // Subfolders (keep Recent root; only clear files there)
                        if (!isRecentFolder)
                        {
                            foreach (var dir in SafeGetDirectories(location.Path))
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                var (bytes, deleted, failed) = DeleteDirectoryContents(dir, cancellationToken);
                                freedBytes += bytes;
                                deletedFiles += deleted;
                                failedFiles += failed;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Clean error in '{location.Name}': {ex.Message}");
                    }
                }

                progress?.Report(new CleanupProgress
                {
                    Message = "Cleanup complete",
                    Percent = 100
                });

                return new CleanupCleanResult
                {
                    FreedBytes = freedBytes,
                    DeletedFiles = deletedFiles,
                    FailedFiles = failedFiles
                };
            }
            catch (OperationCanceledException)
            {
                return new CleanupCleanResult
                {
                    FreedBytes = freedBytes,
                    DeletedFiles = deletedFiles,
                    FailedFiles = failedFiles,
                    ErrorMessage = "Cleanup cancelled"
                };
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return new CleanupCleanResult
                {
                    FreedBytes = freedBytes,
                    DeletedFiles = deletedFiles,
                    FailedFiles = failedFiles,
                    ErrorMessage = ex.Message
                };
            }
            finally
            {
                if (stoppedServices)
                {
                    progress?.Report(new CleanupProgress
                    {
                        Message = "Restarting Windows Update services...",
                        Percent = 98
                    });
                    StartUpdateServices();
                }
            }
        }

        private static (long Bytes, int Deleted, int Failed) DeleteDirectoryContents(string targetDir, CancellationToken cancellationToken)
        {
            long freedBytes = 0;
            int deleted = 0;
            int failed = 0;

            foreach (var file in SafeGetFiles(targetDir))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (TryDeleteFile(file, out long size))
                {
                    freedBytes += size;
                    deleted++;
                }
                else
                {
                    failed++;
                }
            }

            foreach (var dir in SafeGetDirectories(targetDir))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var nested = DeleteDirectoryContents(dir, cancellationToken);
                freedBytes += nested.Bytes;
                deleted += nested.Deleted;
                failed += nested.Failed;
            }

            try
            {
                // Only remove empty leftover folders; never force-delete roots we didn't own entirely
                if (!Directory.EnumerateFileSystemEntries(targetDir).Any())
                    Directory.Delete(targetDir, false);
            }
            catch { }

            return (freedBytes, deleted, failed);
        }

        private static bool TryDeleteFile(string file, out long size)
        {
            size = 0;
            if (ShouldSkipFile(file))
                return false;

            try
            {
                var fi = new FileInfo(file);
                if (!fi.Exists)
                    return false;

                size = fi.Length;
                if ((fi.Attributes & FileAttributes.ReadOnly) != 0)
                    fi.Attributes &= ~FileAttributes.ReadOnly;

                fi.Delete();
                return true;
            }
            catch
            {
                size = 0;
                return false;
            }
        }

        private static bool ShouldSkipFile(string path)
        {
            string name = Path.GetFileName(path);
            foreach (var prefix in SkippedFilePrefixes)
            {
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static IEnumerable<string> SafeGetFiles(string path)
        {
            try
            {
                return Directory.GetFiles(path);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static IEnumerable<string> SafeGetDirectories(string path)
        {
            try
            {
                return Directory.GetDirectories(path);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static bool StopUpdateServices()
        {
            bool ok1 = RunHiddenCmd("net stop wuauserv /y");
            bool ok2 = RunHiddenCmd("net stop bits /y");
            return ok1 || ok2;
        }

        private static void StartUpdateServices()
        {
            RunHiddenCmd("net start bits");
            RunHiddenCmd("net start wuauserv");
        }

        private static bool RunHiddenCmd(string cmd)
        {
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", "/c " + cmd)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var process = Process.Start(psi);
                if (process == null) return false;
                return process.WaitForExit(8000);
            }
            catch (Exception ex)
            {
                Logger.Log($"Service command failed '{cmd}': {ex.Message}");
                return false;
            }
        }

        public static string FormatByteSize(long bytes)
        {
            if (bytes <= 0) return "0 KB";
            if (bytes < 1048576) return $"{bytes / 1024.0:F0} KB";
            double mb = bytes / 1048576.0;
            if (mb >= 1024) return $"{mb / 1024.0:F2} GB";
            return $"{mb:F1} MB";
        }
    }
}
