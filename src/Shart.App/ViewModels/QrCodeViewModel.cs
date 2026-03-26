using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shart.Core.Session;
using Shart.Core.Services;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Shart.App.ViewModels;

/// <summary>
/// ViewModel for the QR Code pairing view.
/// </summary>
public partial class QrCodeViewModel : ObservableObject
{
    private readonly SessionController _sessionController;

    [ObservableProperty]
    private string _qrContent = "";

    [ObservableProperty]
    private string _pin = "";

    [ObservableProperty]
    private string _machineName = Environment.MachineName;

    [ObservableProperty]
    private string _ipAddress = "Detecting...";

    [ObservableProperty]
    private int _port = 9876;

    public QrCodeViewModel(SessionController sessionController)
    {
        _sessionController = sessionController;
        GenerateQrCode();
    }

    [RelayCommand]
    private void GenerateQrCode()
    {
        IpAddress = GetLocalIpAddress();
        var connectionInfo = _sessionController.GetConnectionInfo(MachineName, IpAddress);
        Pin = connectionInfo.Pin;
        QrContent = connectionInfo.ToQrString();
    }

    [RelayCommand]
    private void RefreshCode() => GenerateQrCode();

    private static string GetLocalIpAddress()
    {
        try
        {
            // Get the first active IPv4 address that's not loopback
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return addr.Address.ToString();
                    }
                }
            }
        }
        catch { }

        return "0.0.0.0";
    }
}
