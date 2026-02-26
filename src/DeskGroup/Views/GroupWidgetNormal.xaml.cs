using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using DeskGroup.ViewModels;


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
                var vm = DataContext as GroupViewModel;
                if (vm?.BeginRenameCommand != null && vm.BeginRenameCommand.CanExecute(null))
                    vm.BeginRenameCommand.Execute(null);

                // Focusing the edit box is handled by the FocusBehavior when IsRenaming becomes true.
            }
        }

        private void GroupNameEdit_KeyDown(object sender, KeyEventArgs e)
        {
            var edit = sender as TextBox;
            if (edit == null) return;

            var vm = DataContext as GroupViewModel;

            if (e.Key == Key.Enter)
            {
                var newName = edit.Text ?? string.Empty;
                if (vm?.RenameCommand != null && vm.RenameCommand.CanExecute(newName))
                    vm.RenameCommand.Execute(newName);
            }
            else if (e.Key == Key.Escape)
            {
                if (vm?.CancelRenameCommand != null && vm.CancelRenameCommand.CanExecute(null))
                    vm.CancelRenameCommand.Execute(null);
            }
        }

        private void GroupNameEdit_LostFocus(object sender, RoutedEventArgs e)
        {
            var edit = sender as TextBox;
            if (edit == null) return;

            var vm = DataContext as GroupViewModel;
            var newName = edit.Text ?? string.Empty;
            if (vm?.RenameCommand != null && vm.RenameCommand.CanExecute(newName))
                vm.RenameCommand.Execute(newName);
        }

        private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
        {
            // Update viewmodel size while dragging so layout and persistence are driven by VM
            var vm = DataContext as GroupViewModel;
            if (vm == null) return;

            // Apply deltas; enforce minimum size
            var minW = 120.0;
            var minH = 80.0;
            vm.Width = Math.Max(minW, vm.Width + e.HorizontalChange);
            vm.Height = Math.Max(minH, vm.Height + e.VerticalChange);
        }
    }
}
