using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CortexDNA.Core;
using CortexDNA.Models;

namespace CortexDNA
{
    public partial class CleanConfirmationWindow : Window
    {
        private readonly List<CleanupLocationItem> _locations;

        public IReadOnlyList<CleanupLocationItem> SelectedLocations { get; private set; } = new List<CleanupLocationItem>();

        public CleanConfirmationWindow(IReadOnlyList<CleanupLocationItem> locations)
        {
            InitializeComponent();

            _locations = locations?.ToList() ?? new List<CleanupLocationItem>();

            MouseDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    try { DragMove(); } catch { }
                }
            };

            foreach (var item in _locations)
                item.PropertyChanged += Location_PropertyChanged;

            ListLocations.ItemsSource = _locations;
            RefreshSummary();
        }

        private void Location_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CleanupLocationItem.IsSelected))
                RefreshSummary();
        }

        private void RefreshSummary()
        {
            var selected = _locations.Where(l => l.IsSelected).ToList();
            int files = selected.Sum(l => l.FileCount);
            long bytes = selected.Sum(l => l.Bytes);

            TxtSummary.Text = selected.Count == 0
                ? "Select at least one location to clean"
                : $"Selected: {files:N0} files · {DiskCleanupService.FormatByteSize(bytes)}";

            BtnClean.IsEnabled = selected.Count > 0;
        }

        private void BtnClean_Click(object sender, RoutedEventArgs e)
        {
            SelectedLocations = _locations.Where(l => l.IsSelected).ToList();
            if (SelectedLocations.Count == 0)
                return;

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            foreach (var item in _locations)
                item.PropertyChanged -= Location_PropertyChanged;
            base.OnClosed(e);
        }
    }
}
