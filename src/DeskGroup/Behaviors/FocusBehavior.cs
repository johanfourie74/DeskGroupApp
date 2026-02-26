using System;
using System.Windows;
using System.Windows.Controls;

namespace DeskGroup.Behaviors;

public static class FocusBehavior
{
    public static readonly DependencyProperty FocusOnTrueProperty =
        DependencyProperty.RegisterAttached(
            "FocusOnTrue",
            typeof(bool),
            typeof(FocusBehavior),
            new PropertyMetadata(false, OnFocusOnTrueChanged));

    public static void SetFocusOnTrue(DependencyObject element, bool value)
        => element.SetValue(FocusOnTrueProperty, value);

    public static bool GetFocusOnTrue(DependencyObject element)
        => (bool)element.GetValue(FocusOnTrueProperty);

    private static void OnFocusOnTrueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe) return;

        var shouldFocus = e.NewValue is bool b && b;

        void DoFocus(object? s = null, RoutedEventArgs? args = null)
        {
            try
            {
                if (fe is TextBox tb)
                {
                    tb.Focus();
                    tb.SelectAll();
                }
                else
                {
                    fe.Focus();
                }
            }
            catch { }
            finally
            {
                // detach Loaded handler if it was used
                fe.Loaded -= DoFocus;
            }
        }

        if (shouldFocus)
        {
            if (fe.IsLoaded)
            {
                DoFocus();
            }
            else
            {
                // Delay until loaded, then focus
                fe.Loaded += DoFocus;
            }
        }
        else
        {
            // if toggled off, remove any Loaded handler
            fe.Loaded -= DoFocus;
        }
    }
}
