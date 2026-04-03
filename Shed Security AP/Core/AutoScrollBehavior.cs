using System.Windows;
using System.Windows.Controls;

namespace Shed_Security_AP.Core;

/// <summary>
/// Attached behavior that makes a <see cref="ScrollViewer"/> automatically stick to the
/// bottom when new content arrives. Used on the console log so new lines are always visible
/// without the user having to scroll manually.
/// </summary>
public static class AutoScrollBehavior
{
    public static readonly DependencyProperty AutoScrollProperty =
        DependencyProperty.RegisterAttached(
            "AutoScroll",
            typeof(bool),
            typeof(AutoScrollBehavior),
            new PropertyMetadata(false, OnAutoScrollChanged));

    public static bool GetAutoScroll(DependencyObject dp) => (bool)dp.GetValue(AutoScrollProperty);
    public static void SetAutoScroll(DependencyObject dp, bool value) => dp.SetValue(AutoScrollProperty, value);

    private static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer scrollViewer)
            return;

        if ((bool)e.NewValue)
            scrollViewer.ScrollChanged += OnScrollChanged;
        else
            scrollViewer.ScrollChanged -= OnScrollChanged;
    }

    private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeightChange > 0 && sender is ScrollViewer sv)
            sv.ScrollToBottom();
    }
}
