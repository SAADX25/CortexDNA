using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace CortexDNA.UI;

public static class Motion
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(Motion),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.Inherits, Changed));
    public static bool GetIsEnabled(DependencyObject target) => (bool)target.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject target, bool value) => target.SetValue(IsEnabledProperty, value);
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is true || target is not UIElement element) return;
        element.BeginAnimation(UIElement.OpacityProperty, null);
        if (element.RenderTransform is TranslateTransform slide && !slide.IsFrozen)
        { slide.BeginAnimation(TranslateTransform.XProperty, null); slide.BeginAnimation(TranslateTransform.YProperty, null); }
    }
    public static void Stop(FrameworkElement host)
    {
        host.BeginAnimation(UIElement.OpacityProperty, null); host.Opacity = 1;
        if (host.RenderTransform is TranslateTransform transform)
        { transform.BeginAnimation(TranslateTransform.XProperty, null); transform.X = 0; }
    }
    public static void Enter(FrameworkElement host, bool enabled)
    {
        Stop(host);
        if (!enabled || !host.IsVisible) return;
        var duration = host.TryFindResource("PageTransitionDuration") is Duration configured ? configured : new Duration(TimeSpan.FromMilliseconds(180));
        host.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, duration) { FillBehavior = FillBehavior.Stop });
        var slide = host.RenderTransform as TranslateTransform;
        if (slide == null) { slide = new TranslateTransform(); host.RenderTransform = slide; }
        slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(8, 0, duration)
        { FillBehavior = FillBehavior.Stop, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }
}
