using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shart.Core.Models;
using Shart.Core.Services;

namespace Shart.App.ViewModels;

/// <summary>
/// ViewModel for the Radar/Discovery view.
/// </summary>
public partial class RadarViewModel : ObservableObject
{
    private readonly IDiscoveryService? _discoveryService;
    private readonly ISessionService _sessionService;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanStatus = "Tap to scan for nearby devices";

    public ObservableCollection<DeviceProfile> DiscoveredDevices { get; } = [];

    public RadarViewModel(ISessionService sessionService)
    {
        _sessionService = sessionService;

        // Add demo devices for UI development
        DiscoveredDevices.Add(new DeviceProfile
        {
            DeviceId = "demo-1",
            DeviceName = "Pixel 8 Pro",
            IpAddress = "192.168.49.2",
            SignalStrength = -35,
            Type = DeviceType.Android
        });
        DiscoveredDevices.Add(new DeviceProfile
        {
            DeviceId = "demo-2",
            DeviceName = "Galaxy S24 Ultra",
            IpAddress = "192.168.49.3",
            SignalStrength = -52,
            Type = DeviceType.Android
        });
    }

    [RelayCommand]
    private async Task ToggleScan()
    {
        if (IsScanning)
        {
            IsScanning = false;
            ScanStatus = "Scan stopped";
        }
        else
        {
            IsScanning = true;
            ScanStatus = "Scanning for nearby devices...";
            // Will wire to actual discovery service
            await Task.Delay(1000); // Simulate scan
        }
    }

    [RelayCommand]
    private async Task ConnectToDevice(DeviceProfile device)
    {
        ScanStatus = $"Connecting to {device.DeviceName}...";
        var success = await _sessionService.ConnectAsync(device);
        ScanStatus = success ? $"Connected to {device.DeviceName}" : "Connection failed";
    }
}
