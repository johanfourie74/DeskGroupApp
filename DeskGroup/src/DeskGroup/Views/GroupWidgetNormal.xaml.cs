using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;

namespace DeskGroup.Views
{
    public partial class GroupWidgetNormal : UserControl
    {
        public GroupWidgetNormal()
        {
            InitializeComponent();
        }

        private void GroupNameText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                GroupNameText.Visibility = Visibility.Collapsed;
                GroupNameEdit.Visibility = Visibility.Visible;
                GroupNameEdit.Focus();
            }
        }

        private void GroupNameEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                GroupNameEdit.Visibility = Visibility.Collapsed;
                GroupNameText.Visibility = Visibility.Visible;
            }
        }

        private void GroupNameEdit_LostFocus(object sender, RoutedEventArgs e)
        {
            GroupNameEdit.Visibility = Visibility.Collapsed;
            GroupNameText.Visibility = Visibility.Visible;
        }

        private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
        {
            // intentionally left minimal; resizing handled elsewhere if needed
        }
    }
}
