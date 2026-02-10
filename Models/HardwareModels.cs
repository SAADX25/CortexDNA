using System.Collections.ObjectModel;
using CortexDNA.Core;

namespace CortexDNA.Models
{
    public class AppSystemInfo : ObservableObject
    {
        private string _osName = "Detecting...";
        public string OsName
        {
            get => _osName;
            set => SetProperty(ref _osName, value);
        }

        private string _biosInfo = "Detecting...";
        public string BiosInfo
        {
            get => _biosInfo;
            set => SetProperty(ref _biosInfo, value);
        }

        private string _motherboardModel = "Detecting...";
        public string MotherboardModel
        {
            get => _motherboardModel;
            set => SetProperty(ref _motherboardModel, value);
        }

        private string _biosVersion = "Detecting...";
        public string BiosVersion
        {
            get => _biosVersion;
            set => SetProperty(ref _biosVersion, value);
        }

        private string _biosDate = "Detecting...";
        public string BiosDate
        {
            get => _biosDate;
            set => SetProperty(ref _biosDate, value);
        }

        private string _ramTotal = "Detecting...";
        public string RamTotal
        {
            get => _ramTotal;
            set => SetProperty(ref _ramTotal, value);
        }

        private string _ramType = "Detecting...";
        public string RamType
        {
            get => _ramType;
            set => SetProperty(ref _ramType, value);
        }

        private string _ramInfo = "Detecting...";
        public string RamInfo
        {
            get => _ramInfo;
            set => SetProperty(ref _ramInfo, value);
        }

        // New Visual RAM Properties
        private double _ramUsagePercent;
        public double RamUsagePercent
        {
            get => _ramUsagePercent;
            set => SetProperty(ref _ramUsagePercent, value);
        }

        private string _ramUsageText = "Calculaing...";
        public string RamUsageText
        {
            get => _ramUsageText;
            set => SetProperty(ref _ramUsageText, value);
        }

        // New Network Properties
        private string _networkDownload = "0 KB/s";
        public string NetworkDownload
        {
            get => _networkDownload;
            set => SetProperty(ref _networkDownload, value);
        }

        private string _networkUpload = "0 KB/s";
        public string NetworkUpload
        {
            get => _networkUpload;
            set => SetProperty(ref _networkUpload, value);
        }

        // New Uptime Property
        private string _uptime = "00:00:00";
        public string Uptime
        {
            get => _uptime;
            set => SetProperty(ref _uptime, value);
        }
    }

    public class SensorInfo : ObservableObject
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _value = string.Empty;
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        private string _type = string.Empty;
        public string Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }
    }

    public class HardwareItem : ObservableObject
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _type = string.Empty;
        public string Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public ObservableCollection<SensorInfo> Sensors { get; set; } = new ObservableCollection<SensorInfo>();
    }

    public class StorageDrive : ObservableObject
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _label = string.Empty;
        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        private string _totalSize = string.Empty;
        public string TotalSize
        {
            get => _totalSize;
            set => SetProperty(ref _totalSize, value);
        }

        private string _freeSpace = string.Empty;
        public string FreeSpace
        {
            get => _freeSpace;
            set => SetProperty(ref _freeSpace, value);
        }

        private double _usagePercentage;
        public double UsagePercentage
        {
            get => _usagePercentage;
            set => SetProperty(ref _usagePercentage, value);
        }

        private string _usageText = string.Empty;
        public string UsageText
        {
            get => _usageText;
            set => SetProperty(ref _usageText, value);
        }

        private string _usedColor = "#00ADEF";
        public string UsedColor
        {
            get => _usedColor;
            set => SetProperty(ref _usedColor, value);
        }
    }
}
