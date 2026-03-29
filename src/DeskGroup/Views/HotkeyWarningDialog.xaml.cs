using System.Windows;
using System.Windows.Controls;

namespace DeskGroup.Views;

public partial class HotkeyWarningDialog : Window
{
    public bool ForcedAnyway { get; private set; }

    // Public accessors for buttons so MainViewModel can customize them
    public Button CancelButtonControl => CancelButton;
    public Button ForceButtonControl => ForceButton;

    public HotkeyWarningDialog(string title, string message)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        ForcedAnyway = false;
        DialogResult = false;
        Close();
    }

    private void ForceButton_Click(object sender, RoutedEventArgs e)
    {
        ForcedAnyway = true;
        DialogResult = true;
        Close();
    }
}
