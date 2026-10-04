using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CortexDNA.Core.Health;
using CortexDNA.UI;
using CortexDNA.ViewModels.Pages;
namespace CortexDNA.Controls;

public partial class CortexHealthCore : System.Windows.Controls.UserControl
{
    private HealthViewModel? _vm;
    private Storyboard? _circuit;
    private static readonly IReadOnlyDictionary<HealthNode, string> Routes = new Dictionary<HealthNode, string>
    {
        [HealthNode.Cpu] = "M451,213 L405,180 L388,144",
        [HealthNode.Gpu] = "M451,213 L503,244 L561,214",
        [HealthNode.Memory] = "M451,213 L451,123 L547,72",
        [HealthNode.Storage] = "M451,213 L550,270 L663,304",
        [HealthNode.Network] = "M451,213 L202,208",
        [HealthNode.Startup] = "M451,213 L372,264 L305,300",
        [HealthNode.Security] = "M451,213 L350,162 L282,106",
        [HealthNode.Cleanup] = "M451,213 L423,281 L466,345",
        [HealthNode.Updates] = "M451,213 L600,215 L711,163"
    };
    public CortexHealthCore()
    {
        InitializeComponent();
        Loaded += (_, _) => Attach(); Unloaded += (_, _) => Detach();
        DataContextChanged += (_, _) => { Detach(); if (IsLoaded) Attach(); };
        IsVisibleChanged += (_, _) => { StopMotion(); if (!IsVisible) _vm?.CancelScan(); };
    }
    public bool HasActiveAnimations => _circuit != null || Circuit.HasAnimatedProperties;
    private void Attach() { Detach(); _vm = DataContext as HealthViewModel; if (_vm != null) _vm.PropertyChanged += Changed; }
    private void Detach() { StopMotion(); if (_vm != null) _vm.PropertyChanged -= Changed; _vm = null; }
    private void Changed(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(HealthViewModel.Status) or nameof(HealthViewModel.IsScanning))
        {
            StopMotion();
            if (IsVisible && Motion.GetIsEnabled(this) && _vm?.IsScanning == true && _vm.Nodes.Any(n => n.IsCurrent))
            {
                var geometry = Geometry.Parse(string.Join(" ", _vm.Nodes.Where(n => n.IsCurrent).Select(n => Routes[n.Node])));
                geometry.Freeze(); Circuit.Data = geometry;
                var animation = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(420));
                Storyboard.SetTarget(animation, Circuit); Storyboard.SetTargetProperty(animation, new PropertyPath(System.Windows.Shapes.Shape.StrokeDashOffsetProperty));
                var storyboard = new Storyboard { FillBehavior = FillBehavior.Stop }; storyboard.Children.Add(animation);
                storyboard.Completed += (_, _) => { if (ReferenceEquals(_circuit, storyboard)) { storyboard.Remove(this); _circuit = null; } };
                _circuit = storyboard; storyboard.Begin(this, true);
            }
        }
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (args.Property == Motion.IsEnabledProperty && args.NewValue is false && IsLoaded) StopMotion();
    }
    public void StopMotion()
    {
        _circuit?.Remove(this); _circuit = null;
        Circuit.BeginAnimation(System.Windows.Shapes.Shape.StrokeDashOffsetProperty, null);
        Circuit.Data = Geometry.Empty;
    }
}
