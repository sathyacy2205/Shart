using CommunityToolkit.Mvvm.ComponentModel;

namespace Shart.Core.Transfer;

/// <summary>
/// Real-time progress snapshot for a transfer, used for UI updates.
/// </summary>
public partial class TransferProgress : ObservableObject
{
    [ObservableProperty]
    private long _totalBytes;

    [ObservableProperty]
    private long _bytesTransferred;

    [ObservableProperty]
    private double _percentage;

    [ObservableProperty]
    private double _speedBytesPerSecond;

    [ObservableProperty]
    private int _activeStreams;

    [ObservableProperty]
    private TimeSpan _elapsed;

    [ObservableProperty]
    private TimeSpan _estimatedRemaining;

    /// <summary>Chunks completed out of total.</summary>
    [ObservableProperty]
    private int _chunksCompleted;

    [ObservableProperty]
    private int _totalChunks;

    public void Update(long bytesTransferred, double speedBps, int activeStreams)
    {
        BytesTransferred = bytesTransferred;
        SpeedBytesPerSecond = speedBps;
        ActiveStreams = activeStreams;

        if (TotalBytes > 0)
        {
            Percentage = (double)bytesTransferred / TotalBytes * 100.0;

            if (speedBps > 0)
            {
                EstimatedRemaining = TimeSpan.FromSeconds((TotalBytes - bytesTransferred) / speedBps);
            }
        }
    }
}
