using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using CortexDNA.UI;
using CortexDNA.ViewModels.Pages;
namespace CortexDNA.Views.Pages;

public partial class HealthPage : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty AnimatedScoreProperty = DependencyProperty.Register(nameof(AnimatedScore), typeof(double), typeof(HealthPage),
        new PropertyMetadata(0d, ScoreChanged));
    private HealthViewModel? _vm;
    private Storyboard? _score;
    public double AnimatedScore { get => (double)GetValue(AnimatedScoreProperty); set => SetValue(AnimatedScoreProperty, value); }
    public bool HasActiveAnimations => _score != null || HasAnimatedProperties;
    public HealthPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Attach(); Unloaded += (_, _) => Detach();
        DataContextChanged += (_, _) => { Detach(); if (IsLoaded) Attach(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { StopMotion(); _vm?.CancelScan(); } };
    }
    private void Attach() { Detach(); _vm = DataContext as HealthViewModel; if (_vm != null) _vm.PropertyChanged += Changed; }
    private void Detach() { StopMotion(); if (_vm != null) _vm.PropertyChanged -= Changed; _vm = null; }
    private void Changed(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(HealthViewModel.Result)) return;
        StopMotion();
        if (_vm?.Result?.Score is not { } score || score.AssessedPoints == 0 || !IsVisible || !Motion.GetIsEnabled(this)) return;
        AnimatedScore = score.EarnedPoints;
        var animation = new DoubleAnimation(0, score.EarnedPoints, TimeSpan.FromMilliseconds(650));
        Storyboard.SetTarget(animation, this); Storyboard.SetTargetProperty(animation, new PropertyPath(AnimatedScoreProperty));
        var storyboard = new Storyboard { FillBehavior = FillBehavior.Stop }; storyboard.Children.Add(animation);
        storyboard.Completed += (_, _) => { if (ReferenceEquals(_score, storyboard)) { StopMotion(); RestoreScore(); } };
        _score = storyboard; storyboard.Begin(this, true);
    }
    private static void ScoreChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        var page = (HealthPage)target;
        if (page._score != null && page._vm?.Result is { } result)
            page.ScoreText.Text = $"{(double)args.NewValue:F0}/{result.Score.AssessedPoints}";
    }
    private void RestoreScore()
    {
        ScoreText.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new System.Windows.Data.Binding("Result.Score.Display") { TargetNullValue = "Not rated" });
    }
    public void StopMotion()
    {
        _score?.Remove(this); _score = null;
        BeginAnimation(AnimatedScoreProperty, null);
        if (ScoreText != null) RestoreScore();
        CoreScene?.StopMotion();
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (args.Property == Motion.IsEnabledProperty && args.NewValue is false && IsLoaded) StopMotion();
    }
}
