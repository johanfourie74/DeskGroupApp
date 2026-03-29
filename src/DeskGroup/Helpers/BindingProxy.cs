using System.Windows;

namespace DeskGroup.Helpers;

public class BindingProxy : Freezable
{
    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy));

    protected override Freezable CreateInstanceCore() => new BindingProxy();

    // CRITICAL: Prevent WPF from freezing this object!
    // We need to be able to set Data after resources are initialized
    protected override bool FreezeCore(bool isChecking)
    {
        // Return false to prevent freezing
        return false;
    }
}
