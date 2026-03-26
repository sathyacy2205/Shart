using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shart.Core.Services;
using Shart.Core.Transfer;
using Shart.Data.Settings;

namespace Shart.App.ViewModels;

/// <summary>
/// Main ViewModel — orchestrates navigation and global state.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ITransferService _transferService;
    private readonly ISessionService _sessionService;
    private readonly AppSettingsManager _settingsManager;

    [ObservableProperty]
    private string _currentView = "Transfers";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectedDeviceName = "No device";

    [ObservableProperty]
    private string _statusMessage = "Ready to connect";

    [ObservableProperty]
    private int _activeTransferCount;

    public RadarViewModel RadarVm { get; }
    public TransferQueueViewModel TransferQueueVm { get; }
    public QrCodeViewModel QrCodeVm { get; }

    public MainViewModel(
        ITransferService transferService,
        ISessionService sessionService,
        AppSettingsManager settingsManager,
        RadarViewModel radarVm,
        TransferQueueViewModel transferQueueVm,
        QrCodeViewModel qrCodeVm)
    {
        _transferService = transferService;
        _sessionService = sessionService;
        _settingsManager = settingsManager;
        RadarVm = radarVm;
        TransferQueueVm = transferQueueVm;
        QrCodeVm = qrCodeVm;

        // Wire up events
        _sessionService.ConnectionStateChanged += (_, e) =>
        {
            IsConnected = e.IsConnected;
            ConnectedDeviceName = e.IsConnected ? e.Device?.DeviceName ?? "Unknown" : "No device";
            StatusMessage = e.IsConnected ? $"Connected to {ConnectedDeviceName}" : "Disconnected";
        };

        _transferService.TransferProgressChanged += (_, _) =>
        {
            ActiveTransferCount = _transferService.GetActiveTransfers().Count;
        };
    }

    [RelayCommand]
    private void NavigateTo(string viewName) => CurrentView = viewName;

    [RelayCommand]
    private async Task Disconnect() => await _sessionService.DisconnectAsync();
}
