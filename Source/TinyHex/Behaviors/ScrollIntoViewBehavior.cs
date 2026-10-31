using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TinyHex.Models;

namespace TinyHex.Behaviors;

/// <summary>
/// Scrolls the associated (virtualizing) list to the index requested through a <see cref="ScrollRequestNotifier"/>.
/// </summary>
public sealed class ScrollIntoViewBehavior : Behavior<ListBox>
{
    public static readonly DependencyProperty NotifierProperty =
        DependencyProperty.Register(
            nameof(Notifier),
            typeof(ScrollRequestNotifier),
            typeof(ScrollIntoViewBehavior),
            new PropertyMetadata(null, OnNotifierChanged));

    public ScrollRequestNotifier? Notifier
    {
        get => (ScrollRequestNotifier?)GetValue(NotifierProperty);
        set => SetValue(NotifierProperty, value);
    }

    private static void OnNotifierChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollIntoViewBehavior behavior || behavior.AssociatedObject == null)
            return;

        if (e.OldValue is ScrollRequestNotifier oldNotifier)
            oldNotifier.ScrollRequested -= behavior.OnScrollRequested;
        if (e.NewValue is ScrollRequestNotifier newNotifier)
            newNotifier.ScrollRequested += behavior.OnScrollRequested;
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (Notifier != null)
            Notifier.ScrollRequested += OnScrollRequested;
    }

    protected override void OnDetaching()
    {
        if (Notifier != null)
            Notifier.ScrollRequested -= OnScrollRequested;
        base.OnDetaching();
    }

    private void OnScrollRequested(int index)
    {
        var panel = FindVisualChild<VirtualizingPanel>(AssociatedObject);
        panel?.BringIndexIntoViewPublic(index);
    }

    private static T? FindVisualChild<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj is T found) return found;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }
}
