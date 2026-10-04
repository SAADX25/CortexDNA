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
    public sealed class DiskCleanupService : ICleanupService
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
                    Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
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

        private static readonly SemaphoreSlim _operation = new(1, 1);
        private readonly IReadOnlyList<CleanupLocationItem>? _testLocations;
        public DiskCleanupService() { }
        internal DiskCleanupService(IReadOnlyList<CleanupLocationItem> testLocations) => _testLocations = testLocations;

        private void Validate(IReadOnlyList<CleanupLocationItem> locations)
        {
            var allowed = _testLocations ?? CreateDefaultLocations();
            foreach (var location in locations)
            {
                string full = Path.GetFullPath(location.Path);
                if (full.Equals(Path.GetPathRoot(full), StringComparison.OrdinalIgnoreCase) ||
                    !allowed.Any(a => a.Id == location.Id &&
                        Path.TrimEndingDirectorySeparator(Path.GetFullPath(a.Path)).Equals(
                            Path.TrimEndingDirectorySeparator(full), StringComparison.OrdinalIgnoreCase) &&
                        a.RequiresUpdateServices == location.RequiresUpdateServices))
                    throw new InvalidOperationException("Cleanup location is not an approved root.");
            }
        }

        public async Task<CleanupScanResult> ScanAsync(IReadOnlyList<CleanupLocationItem> locations,
            IProgress<CleanupProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            locations = locations.ToArray();
            Validate(locations);
            await _operation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { return await Task.Run(() => Scan(locations, progress, cancellationToken), cancellationToken).ConfigureAwait(false); }
            finally { _operation.Release(); }
        }

        public async Task<CleanupCleanResult> CleanAsync(IReadOnlyList<CleanupLocationItem> selectedLocations,
            IProgress<CleanupProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            selectedLocations = selectedLocations.ToArray();
            Validate(selectedLocations);
            await _operation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { return await Task.Run(() => Clean(selectedLocations, progress, cancellationToken), cancellationToken).ConfigureAwait(false); }
            finally { _operation.Release(); }
        }

        private static IEnumerable<string> EnumerateSafe(string directory, bool recurse, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using var guard = CleanupPathGuard.Acquire(directory);
            foreach (string file in Directory.EnumerateFiles(directory))
            {
                token.ThrowIfCancellationRequested();
                if (!ShouldSkipFile(file)) yield return file;
            }
            if (!recurse) yield break;
            foreach (string child in Directory.EnumerateDirectories(directory))
            {
                token.ThrowIfCancellationRequested();
                // A refused subtree must not abort other safe siblings.
                IEnumerator<string>? iterator = null;
                try { iterator = EnumerateSafe(child, true, token).GetEnumerator(); }
                catch (IOException) { }
                if (iterator == null) continue;
                using (iterator)
                {
                    while (true)
                    {
                        bool next;
                        try { next = iterator.MoveNext(); }
                        catch (IOException) { break; }
                        catch (UnauthorizedAccessException) { break; }
                        if (!next) break;
                        yield return iterator.Current;
                    }
                }
            }
        }

        private CleanupScanResult Scan(IReadOnlyList<CleanupLocationItem> locations,
            IProgress<CleanupProgress>? progress, CancellationToken token)
        {
            var scanned = new List<CleanupLocationItem>();
            foreach (var location in locations)
            {
                token.ThrowIfCancellationRequested();
                long bytes = 0;
                int files = 0;
                try
                {
                    foreach (string file in EnumerateSafe(location.Path, location.Id != CleanupCategoryId.Recent, token))
                    {
                        try { bytes += new FileInfo(file).Length; files++; }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                }
                catch (IOException ex) { Logger.Log($"Scan skipped {location.Name}: {ex.Message}"); }
                catch (UnauthorizedAccessException ex) { Logger.Log($"Scan skipped {location.Name}: {ex.Message}"); }
                // Return detached items: never raise UI-bound PropertyChanged on a worker.
                scanned.Add(new CleanupLocationItem {
                    Id = location.Id, Name = location.Name, Path = location.Path,
                    IsSelected = location.IsSelected, IsRecommended = location.IsRecommended,
                    RequiresUpdateServices = location.RequiresUpdateServices, Warning = location.Warning,
                    Bytes = bytes, FileCount = files, FormattedSize = FormatByteSize(bytes)
                });
                progress?.Report(new CleanupProgress { Message = $"Scanning {location.Name}...",
                    Percent = scanned.Count * 100 / Math.Max(1, locations.Count) });
            }
            return new CleanupScanResult { Locations = scanned, FileCount = scanned.Sum(x => x.FileCount),
                TotalBytes = scanned.Sum(x => x.Bytes) };
        }

        private CleanupCleanResult Clean(IReadOnlyList<CleanupLocationItem> locations,
            IProgress<CleanupProgress>? progress, CancellationToken token)
        {
            long bytes = 0;
            int deleted = 0, failed = 0;
            string? error = null;
            UpdateServiceLease? services = null;
            try
            {
                token.ThrowIfCancellationRequested();
                if (locations.Any(l => l.RequiresUpdateServices)) services = UpdateServiceLease.Acquire(token);
                foreach (var location in locations)
                {
                    token.ThrowIfCancellationRequested();
                    if (location.RequiresUpdateServices) services!.VerifyStopped();
                    progress?.Report(new CleanupProgress { Message = $"Cleaning {location.Name}..." });
                    try
                    {
                        foreach (string file in EnumerateSafe(location.Path, location.Id != CleanupCategoryId.Recent, token))
                        {
                            token.ThrowIfCancellationRequested();
                            if (location.RequiresUpdateServices) services!.VerifyStopped();
                            try
                            {
                                // Ancestor leases remain alive across the iterator yield.
                                if (ShouldSkipFile(file)) continue;
                                long size = new FileInfo(file).Length;
                                File.Delete(file); // Do not alter read-only attributes or recursively delete folders.
                                bytes += size;
                                deleted++;
                            }
                            catch (IOException) { failed++; }
                            catch (UnauthorizedAccessException) { failed++; }
                        }
                    }
                    catch (IOException ex) { failed++; Logger.Log(ex); }
                    catch (UnauthorizedAccessException ex) { failed++; Logger.Log(ex); }
                }
            }
            catch (OperationCanceledException) { error = "Cleanup cancelled"; }
            catch (Exception ex) { error = ex.Message; Logger.Log(ex); }
            finally
            {
                try { services?.Dispose(); }
                catch (Exception ex) { error = (error == null ? "" : error + " ") + ex.Message; }
            }
            progress?.Report(new CleanupProgress { Message = error ?? "Cleanup complete", Percent = 100 });
            return new CleanupCleanResult { FreedBytes = bytes, DeletedFiles = deleted, FailedFiles = failed, ErrorMessage = error };
        }

        private static bool ShouldSkipFile(string path)
        {
            try
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return true;
                string name = Path.GetFileName(path);
                return SkippedFilePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            }
            catch (IOException) { return true; }
            catch (UnauthorizedAccessException) { return true; }
        }

        public static string FormatByteSize(long bytes)
        {
            if (bytes <= 0) return "0 KB";
            if (bytes < 1048576) return $"{bytes / 1024.0:F0} KB";
            double mb = bytes / 1048576.0;
            return mb >= 1024 ? $"{mb / 1024.0:F2} GB" : $"{mb:F1} MB";
        }
    }
}
