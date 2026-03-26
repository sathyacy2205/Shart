using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace Shart.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // ═══════════ TITLE BAR ═══════════
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized 
                    ? WindowState.Normal 
                    : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    
    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized 
            ? WindowState.Normal 
            : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ═══════════ NAVIGATION ═══════════
    private void HideAllViews()
    {
        TransferView.Visibility = Visibility.Collapsed;
        RadarViewPanel.Visibility = Visibility.Collapsed;
        DropZoneViewPanel.Visibility = Visibility.Collapsed;
        QrCodeViewPanel.Visibility = Visibility.Collapsed;
        HistoryViewPanel.Visibility = Visibility.Collapsed;
        SettingsViewPanel.Visibility = Visibility.Collapsed;
    }

    private void ShowView(UIElement view)
    {
        HideAllViews();
        view.Visibility = Visibility.Visible;
    }

    private void NavTransfers_Checked(object sender, RoutedEventArgs e) => ShowView(TransferView);
    private void NavRadar_Checked(object sender, RoutedEventArgs e) => ShowView(RadarViewPanel);
    private void NavDropZone_Checked(object sender, RoutedEventArgs e) => ShowView(DropZoneViewPanel);
    private void NavQrCode_Checked(object sender, RoutedEventArgs e) => ShowView(QrCodeViewPanel);
    private void NavHistory_Checked(object sender, RoutedEventArgs e) => ShowView(HistoryViewPanel);
    private void NavSettings_Checked(object sender, RoutedEventArgs e) => ShowView(SettingsViewPanel);

    // ═══════════ DROP ZONE ═══════════
    private void DropArea_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DropArea.BorderBrush = (System.Windows.Media.Brush)FindResource("AccentPrimaryBrush");
            DropArea.Background = (System.Windows.Media.Brush)FindResource("BgElevatedBrush");
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void DropArea_DragLeave(object sender, DragEventArgs e)
    {
        DropArea.Background = (System.Windows.Media.Brush)FindResource("BgSurfaceBrush");
    }

    private void DropArea_Drop(object sender, DragEventArgs e)
    {
        DropArea.Background = (System.Windows.Media.Brush)FindResource("BgSurfaceBrush");

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            HandleDroppedFiles(files);
        }
    }

    private void BrowseFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = "Select files to send"
        };

        if (dialog.ShowDialog() == true)
        {
            HandleDroppedFiles(dialog.FileNames);
        }
    }

    private void HandleDroppedFiles(string[] filePaths)
    {
        // TODO: Wire to TransferManager.SendFilesAsync
        var fileList = string.Join("\n", filePaths.Select(System.IO.Path.GetFileName));
        MessageBox.Show($"Files queued for transfer:\n\n{fileList}", "Shart",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}