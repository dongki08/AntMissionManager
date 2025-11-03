using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AntMissionManager.Models;
using AntMissionManager.ViewModels;

namespace AntMissionManager.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            this.DragMove();
        }
        else if (e.ClickCount == 2)
        {
            MaximizeButton_Click(sender, e);
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (this.WindowState == WindowState.Maximized)
        {
            this.WindowState = WindowState.Normal;
        }
        else
        {
            this.WindowState = WindowState.Maximized;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void EditRoute_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is MissionRoute route && DataContext is MainViewModel vm)
        {
            vm.EditRouteCommand.Execute(route);
        }
    }

    private void DeleteRoute_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is MissionRoute route && DataContext is MainViewModel vm)
        {
            vm.DeleteRouteCommand.Execute(route);
        }
    }

    private void VehicleRow_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.DataGridRow row && row.Item is Vehicle vehicle)
        {
            var detailWindow = new VehicleDetailWindow(vehicle);
            detailWindow.ShowDialog();
        }
    }

    private void AlarmDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (sender is not DataGrid dataGrid)
        {
            return;
        }

        e.Handled = true;

        var sortMemberPath = GetSortMemberPath(e.Column);
        if (string.IsNullOrWhiteSpace(sortMemberPath))
        {
            return;
        }

        var newDirection = e.Column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;

        vm.ApplyAlarmColumnSort(sortMemberPath, newDirection);

        e.Column.SortDirection = newDirection;

        foreach (var column in dataGrid.Columns)
        {
            if (!ReferenceEquals(column, e.Column))
            {
                column.SortDirection = null;
            }
        }
    }

    private void VehicleDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (sender is not DataGrid dataGrid)
        {
            return;
        }

        e.Handled = true;

        var sortMemberPath = GetSortMemberPath(e.Column);
        if (string.IsNullOrWhiteSpace(sortMemberPath))
        {
            return;
        }

        var newDirection = e.Column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;

        vm.ApplyVehicleColumnSort(sortMemberPath, newDirection);

        e.Column.SortDirection = newDirection;

        foreach (var column in dataGrid.Columns)
        {
            if (!ReferenceEquals(column, e.Column))
            {
                column.SortDirection = null;
            }
        }
    }

    private void AlarmDataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGrid dataGrid || DataContext is not MainViewModel vm)
        {
            return;
        }

        var sortDirection = vm.AlarmSortAscending ? ListSortDirection.Ascending : ListSortDirection.Descending;
        foreach (var column in dataGrid.Columns)
        {
            if (string.Equals(GetSortMemberPath(column), nameof(AlarmInfo.Timestamp), StringComparison.Ordinal))
            {
                column.SortDirection = sortDirection;
            }
            else
            {
                column.SortDirection = null;
            }
        }
    }

    private void VehicleDataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGrid dataGrid || DataContext is not MainViewModel vm)
        {
            return;
        }

        foreach (var column in dataGrid.Columns)
        {
            if (string.Equals(GetSortMemberPath(column), vm.VehicleSortProperty, StringComparison.Ordinal))
            {
                column.SortDirection = vm.VehicleSortDirection;
            }
            else
            {
                column.SortDirection = null;
            }
        }
    }

    private void MissionDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (sender is not DataGrid dataGrid)
        {
            return;
        }

        e.Handled = true;

        var sortMemberPath = GetSortMemberPath(e.Column);
        if (string.IsNullOrWhiteSpace(sortMemberPath))
        {
            return;
        }

        var newDirection = e.Column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;

        vm.ApplyMissionColumnSort(sortMemberPath, newDirection);

        e.Column.SortDirection = newDirection;

        foreach (var column in dataGrid.Columns)
        {
            if (!ReferenceEquals(column, e.Column))
            {
                column.SortDirection = null;
            }
        }
    }

    private void MissionDataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGrid dataGrid || DataContext is not MainViewModel vm)
        {
            return;
        }

        foreach (var column in dataGrid.Columns)
        {
            if (string.Equals(GetSortMemberPath(column), vm.MissionSortProperty, StringComparison.Ordinal))
            {
                column.SortDirection = vm.MissionSortDirection;
            }
            else
            {
                column.SortDirection = null;
            }
        }
    }

    private static string? GetSortMemberPath(DataGridColumn column)
    {
        var sortMemberPath = column.SortMemberPath;
        if (string.IsNullOrWhiteSpace(sortMemberPath) && column is DataGridBoundColumn boundColumn)
        {
            if (boundColumn.Binding is Binding binding && binding.Path != null)
            {
                sortMemberPath = binding.Path.Path;
            }
        }

        return sortMemberPath;
    }

    private void AlarmSortOrder_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item && DataContext is MainViewModel vm)
        {
            var tag = item.Tag?.ToString();
            vm.AlarmSortAscending = tag == "asc";

            if (vm.RefreshAlarmsCommand.CanExecute(null))
            {
                vm.RefreshAlarmsCommand.Execute(null);
            }
        }
    }

    private void AlarmLimit_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item && DataContext is MainViewModel vm)
        {
            var tag = item.Tag?.ToString();
            if (int.TryParse(tag, out int limit))
            {
                vm.AlarmLimit = limit;

                if (vm.RefreshAlarmsCommand.CanExecute(null))
                {
                    vm.RefreshAlarmsCommand.Execute(null);
                }
            }
        }
    }
}
