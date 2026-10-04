using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CortexDNA.Models
{
    public enum CleanupCategoryId
    {
        UserTemp,
        SystemTemp,
        Prefetch,
        Recent,
        WindowsUpdate,
        WerLogs
    }

    public sealed class CleanupLocationItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private long _bytes;
        private string _formattedSize = "0 MB";
        private int _fileCount;

        public CleanupCategoryId Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Path { get; init; } = string.Empty;
        public bool IsRecommended { get; init; }
        public bool RequiresUpdateServices { get; init; }
        public string? Warning { get; init; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public long Bytes
        {
            get => _bytes;
            set
            {
                if (_bytes == value) return;
                _bytes = value;
                OnPropertyChanged();
            }
        }

        public string FormattedSize
        {
            get => _formattedSize;
            set
            {
                if (_formattedSize == value) return;
                _formattedSize = value;
                OnPropertyChanged();
            }
        }

        public int FileCount
        {
            get => _fileCount;
            set
            {
                if (_fileCount == value) return;
                _fileCount = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed class CleanupScanResult
    {
        public IReadOnlyList<CleanupLocationItem> Locations { get; init; } = Array.Empty<CleanupLocationItem>();
        public int FileCount { get; init; }
        public long TotalBytes { get; init; }
        public double TotalSizeMB => TotalBytes / (1024.0 * 1024.0);
    }

    public sealed class CleanupCleanResult
    {
        public long FreedBytes { get; init; }
        public int DeletedFiles { get; init; }
        public int FailedFiles { get; init; }
        public string? ErrorMessage { get; init; }
        public bool Success => string.IsNullOrEmpty(ErrorMessage);
    }

    public sealed class CleanupProgress
    {
        public string Message { get; init; } = string.Empty;
        public string? CurrentLocation { get; init; }
        public int Percent { get; init; }
    }

    public sealed class RamOptimizeResult
    {
        public float AvailableBeforeMb { get; init; }
        public float AvailableAfterMb { get; init; }
        public float ReclaimedMb => Math.Max(0, AvailableAfterMb - AvailableBeforeMb);
        public int ProcessesTouched { get; init; }
        public bool Success { get; init; } = true;
        public string? ErrorMessage { get; init; }
    }
}
