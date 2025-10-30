using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AntMissionManager.Models;
using AntMissionManager.Services;

namespace AntMissionManager.Views;

public partial class VehicleDetailWindow : Window
{
    private readonly DispatcherTimer _updateTimer;
    private readonly string _vehicleName;
    private readonly AntApiService _apiService;

    public VehicleDetailWindow(Vehicle vehicle)
    {
        InitializeComponent();
        DataContext = vehicle;

        _vehicleName = vehicle.Name;
        _apiService = AntApiService.Instance;

        // Setup timer to update every 1 second
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();
    }

    private async void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            var vehicles = await _apiService.GetAllVehiclesAsync();
            var updatedVehicle = vehicles.FirstOrDefault(v => v.Name == _vehicleName);

            if (updatedVehicle != null)
            {
                DataContext = updatedVehicle;
            }
        }
        catch (Exception ex)
        {
            // Log error but don't stop the timer
            System.Diagnostics.Debug.WriteLine($"Failed to update vehicle detail: {ex.Message}");
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            this.DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _updateTimer.Stop();
        this.Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _updateTimer.Stop();
        base.OnClosed(e);
    }
}
