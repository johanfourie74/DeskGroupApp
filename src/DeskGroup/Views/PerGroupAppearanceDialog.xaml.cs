using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DeskGroup.ViewModels;

namespace DeskGroup.Views;

public partial class PerGroupAppearanceDialog : Window
{
    private readonly Action _resetAction;

    public PerGroupAppearanceDialog(GroupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _resetAction = viewModel.ResetGroupAppearance;
    }

    public PerGroupAppearanceDialog(EdgeDockViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _resetAction = viewModel.ResetDockAppearance;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _resetAction();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Slider_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not Slider slider) return;
        double delta = slider.SmallChange * (e.Delta > 0 ? 1 : -1);
        slider.Value = Math.Clamp(slider.Value + delta, slider.Minimum, slider.Maximum);
        e.Handled = true;
    }
}
