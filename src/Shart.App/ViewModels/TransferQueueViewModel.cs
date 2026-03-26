using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shart.Core.Models;
using Shart.Core.Services;
using Shart.Core.Transfer;

namespace Shart.App.ViewModels;

/// <summary>
/// ViewModel for the Transfer Queue dashboard.
/// </summary>
public partial class TransferQueueViewModel : ObservableObject
{
    private readonly ITransferService _transferService;

    public ObservableCollection<TransferJob> ActiveTransfers { get; } = [];
    public ObservableCollection<TransferJob> CompletedTransfers { get; } = [];

    [ObservableProperty]
    private string _overallSpeed = "0 MB/s";

    [ObservableProperty]
    private int _activeCount;

    [ObservableProperty]
    private int _completedCount;

    [ObservableProperty]
    private long _totalBytesTransferred;

    public TransferQueueViewModel(ITransferService transferService)
    {
        _transferService = transferService;

        _transferService.TransferProgressChanged += OnTransferProgressChanged;
        _transferService.TransferCompleted += OnTransferCompleted;

        // Add demo data for UI development
        var demoJob = new TransferJob
        {
            FileMetadata = new FileMetadata
            {
                FileName = "vacation_video.mp4",
                FileSizeBytes = 2_500_000_000L,
            },
            Direction = TransferDirection.Incoming,
            RemoteDevice = new DeviceProfile
            {
                DeviceId = "demo-1",
                DeviceName = "Pixel 8 Pro"
            }
        };
        demoJob.UpdateProgress(1_200_000_000L, 125_000_000);
        demoJob.Status = TransferStatus.InProgress;
        ActiveTransfers.Add(demoJob);

        var demoJob2 = new TransferJob
        {
            FileMetadata = new FileMetadata
            {
                FileName = "photos_backup.zip",
                FileSizeBytes = 800_000_000L,
            },
            Direction = TransferDirection.Outgoing,
            RemoteDevice = new DeviceProfile
            {
                DeviceId = "demo-1",
                DeviceName = "Pixel 8 Pro"
            }
        };
        demoJob2.Status = TransferStatus.Pending;
        ActiveTransfers.Add(demoJob2);

        ActiveCount = ActiveTransfers.Count;
    }

    private void OnTransferProgressChanged(object? sender, TransferProgressEventArgs e)
    {
        // Update corresponding job in the collection
        var job = ActiveTransfers.FirstOrDefault(j => j.JobId == e.JobId);
        if (job != null)
        {
            // Calculate overall speed
            double totalSpeed = ActiveTransfers
                .Where(j => j.Status == TransferStatus.InProgress)
                .Sum(j => j.SpeedBytesPerSecond);
            OverallSpeed = FormatSpeed(totalSpeed);
        }
    }

    private void OnTransferCompleted(object? sender, TransferCompletedEventArgs e)
    {
        var job = ActiveTransfers.FirstOrDefault(j => j.JobId == e.JobId);
        if (job != null)
        {
            ActiveTransfers.Remove(job);
            CompletedTransfers.Insert(0, job);
            ActiveCount = ActiveTransfers.Count;
            CompletedCount = CompletedTransfers.Count;
        }
    }

    [RelayCommand]
    private async Task PauseTransfer(TransferJob job) => await _transferService.PauseTransferAsync(job.JobId);

    [RelayCommand]
    private async Task ResumeTransfer(TransferJob job) => await _transferService.ResumeTransferAsync(job.JobId);

    [RelayCommand]
    private async Task CancelTransfer(TransferJob job) => await _transferService.CancelTransferAsync(job.JobId);

    private static string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond < 1024) return $"{bytesPerSecond:F0} B/s";
        if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024:F1} KB/s";
        if (bytesPerSecond < 1024 * 1024 * 1024) return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
        return $"{bytesPerSecond / (1024.0 * 1024 * 1024):F2} GB/s";
    }
}
