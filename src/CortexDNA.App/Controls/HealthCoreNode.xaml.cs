using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CortexDNA.UI;
using CortexDNA.ViewModels.Pages;
namespace CortexDNA.Controls;

public partial class HealthCoreNode : System.Windows.Controls.UserControl
{
    private HealthNodeViewModel? _node;
    private Storyboard? _pulse;
    private readonly TranslateTransform _slide = new();
    private readonly ScaleTransform _scale = new(1, 1);
    public HealthCoreNode()
    {
        InitializeComponent();
        Chip.RenderTransform = new TransformGroup { Children = { _scale, _slide } };
        Chip.RenderTransformOrigin = new(.5, .5);
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        IsVisibleChanged += (_, _) => UpdateMotion();
        DataContextChanged += (_, _) => { Detach(); if (IsLoaded) Attach(); };
    }
    public bool HasActiveAnimations => _pulse != null || Glow.HasAnimatedProperties || _slide.HasAnimatedProperties || _scale.HasAnimatedProperties;
    private void Attach()
    {
        Detach(); _node = DataContext as HealthNodeViewModel;
        if (_node != null) _node.PropertyChanged += NodeChanged;
        UpdateMotion();
    }
    private void Detach() { StopMotion(); if (_node != null) _node.PropertyChanged -= NodeChanged; _node = null; }
    private void NodeChanged(object? sender, PropertyChangedEventArgs args) { if (args.PropertyName == nameof(HealthNodeViewModel.IsCurrent)) UpdateMotion(); }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (args.Property == Motion.IsEnabledProperty && IsLoaded) UpdateMotion();
    }
    private void UpdateMotion()
    {
        StopMotion();
        Glow.Opacity = IsVisible && _node?.IsCurrent == true ? .28 : 0;
        if (!IsLoaded || !IsVisible || !Motion.GetIsEnabled(this) || _node?.IsCurrent != true) return;
        var storyboard = new Storyboard { FillBehavior = FillBehavior.Stop };
        Add(Glow, UIElement.OpacityProperty, .28, .7);
        Add(_slide, TranslateTransform.YProperty, 0, -3);
        Add(_scale, ScaleTransform.ScaleXProperty, 1, 1.025);
        Add(_scale, ScaleTransform.ScaleYProperty, 1, 1.025);
        void Add(DependencyObject target, DependencyProperty property, double from, double to)
        {
            var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(300)) { AutoReverse = true };
            Storyboard.SetTarget(animation, target); Storyboard.SetTargetProperty(animation, new PropertyPath(property));
            storyboard.Children.Add(animation);
        }
        storyboard.Completed += (_, _) => { if (ReferenceEquals(_pulse, storyboard)) { storyboard.Remove(this); _pulse = null; } };
        _pulse = storyboard; storyboard.Begin(this, true);
    }
    public void StopMotion()
    {
        _pulse?.Remove(this); _pulse = null;
        // Storyboard.Remove is deferred to a timing tick; hidden windows may not receive that tick.
        Glow.BeginAnimation(UIElement.OpacityProperty, null);
        _slide.BeginAnimation(TranslateTransform.YProperty, null);
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
    }
}
