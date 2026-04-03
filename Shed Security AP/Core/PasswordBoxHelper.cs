using System.Windows;
using System.Windows.Controls;

namespace Shed_Security_AP.Core;

/// <summary>
/// WPF's <see cref="PasswordBox"/> doesn't support binding its password for security reasons.
/// This attached property works around that so we can still use MVVM without code-behind hacks.
/// The <c>_isUpdating</c> flag prevents infinite update loops between the control and the binding.
/// </summary>
public static class PasswordBoxHelper
{
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword",
            typeof(string),
            typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

    public static readonly DependencyProperty AttachProperty =
        DependencyProperty.RegisterAttached(
            "Attach",
            typeof(bool),
            typeof(PasswordBoxHelper),
            new PropertyMetadata(false, OnAttachChanged));

    private static bool _isUpdating;

    public static string GetBoundPassword(DependencyObject dp) => (string)dp.GetValue(BoundPasswordProperty);
    public static void SetBoundPassword(DependencyObject dp, string value) => dp.SetValue(BoundPasswordProperty, value);

    public static bool GetAttach(DependencyObject dp) => (bool)dp.GetValue(AttachProperty);
    public static void SetAttach(DependencyObject dp, bool value) => dp.SetValue(AttachProperty, value);

    private static void OnAttachChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox passwordBox) return;

        if ((bool)e.OldValue)
            passwordBox.PasswordChanged -= OnPasswordChanged;

        if ((bool)e.NewValue)
            passwordBox.PasswordChanged += OnPasswordChanged;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdating || sender is not PasswordBox passwordBox) return;

        _isUpdating = true;
        SetBoundPassword(passwordBox, passwordBox.Password);
        _isUpdating = false;
    }

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (_isUpdating || d is not PasswordBox passwordBox) return;

        _isUpdating = true;
        passwordBox.Password = (string)e.NewValue;
        _isUpdating = false;
    }
}
