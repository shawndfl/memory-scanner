using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace MemoryScanner;

public partial class MainWindow : Window
{
    private const int DisplayLimit = 5_000;
    private readonly ProcessMemoryService _memory = new();
    private readonly ObservableCollection<ScanResult> _results = [];
    private readonly ObservableCollection<SavedAddress> _savedAddresses = [];
    private readonly string _savedAddressesPath;
    private List<long> _candidateAddresses = [];
    private CancellationTokenSource? _scanCancellation;
    private ProcessItem? _attachedProcess;
    private ValueTypeKind _scanType;
    private Endianness _scanEndianness;

    public MainWindow()
    {
        InitializeComponent();
        ResultsGrid.ItemsSource = _results;
        SavedGrid.ItemsSource = _savedAddresses;
        ValueTypeComboBox.ItemsSource = Enum.GetValues<ValueTypeKind>();
        ValueTypeComboBox.SelectedItem = ValueTypeKind.Int32;
        EndiannessComboBox.ItemsSource = Enum.GetValues<Endianness>();
        EndiannessComboBox.SelectedItem = Endianness.LittleEndian;

        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MemoryScanner");
        _savedAddressesPath = Path.Combine(dataDirectory, "saved-addresses.json");

        LoadSavedAddresses();
        RefreshProcesses();
    }

    private void RefreshProcesses_Click(object sender, RoutedEventArgs e) => RefreshProcesses();

    private void RefreshProcesses()
    {
        var selectedId = (ProcessComboBox.SelectedItem as ProcessItem)?.Id;
        var processes = Process.GetProcesses()
            .Select(process =>
            {
                try { return new ProcessItem(process.Id, process.ProcessName); }
                catch { return null; }
                finally { process.Dispose(); }
            })
            .Where(item => item is not null)
            .OrderBy(item => item!.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item!.Id)
            .ToList();

        ProcessComboBox.ItemsSource = processes;
        ProcessComboBox.SelectedItem = processes.FirstOrDefault(item => item!.Id == selectedId) ?? processes.FirstOrDefault();
        StatusTextBlock.Text = _attachedProcess is null ? $"Found {processes.Count} processes" : $"Attached to {_attachedProcess.DisplayName}";
    }

    private void Attach_Click(object sender, RoutedEventArgs e)
    {
        if (ProcessComboBox.SelectedItem is not ProcessItem process)
            return;

        try
        {
            _memory.Attach(process.Id);
            _attachedProcess = process;
            ResetScan();
            StatusTextBlock.Text = $"Attached to {process.DisplayName}";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async void FirstScan_Click(object sender, RoutedEventArgs e)
    {
        if (!TryPrepareScan(out var target, out var type, out var endianness))
            return;

        _scanType = type;
        _scanEndianness = endianness;
        var progress = new Progress<ScanProgress>(UpdateScanProgress);
        await RunScanAsync(token => _memory.FirstScan(target, progress, token));
    }

    private async void NextScan_Click(object sender, RoutedEventArgs e)
    {
        if (!TryPrepareScan(out var target, out var type, out var endianness))
            return;
        if (type != _scanType || endianness != _scanEndianness)
        {
            MessageBox.Show(this, "The value type and endianness must stay the same during a scan. Start a new scan to change them.", "Memory Scanner", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var candidates = _candidateAddresses.ToArray();
        await RunScanAsync(token => _memory.NextScan(candidates, target, token));
    }

    private bool TryPrepareScan(out byte[] target, out ValueTypeKind type, out Endianness endianness)
    {
        target = [];
        type = (ValueTypeKind)(ValueTypeComboBox.SelectedItem ?? ValueTypeKind.Int32);
        endianness = (Endianness)(EndiannessComboBox.SelectedItem ?? Endianness.LittleEndian);
        if (!_memory.IsAttached)
        {
            MessageBox.Show(this, "Attach to a process first.", "Memory Scanner", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        try
        {
            target = ValueConverter.Parse(ScanValueTextBox.Text, type, endianness);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            MessageBox.Show(this, "Enter a valid value for the selected type.", "Memory Scanner", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private async Task RunScanAsync(Func<CancellationToken, List<long>> scan)
    {
        SetScanning(true);
        _scanCancellation = new CancellationTokenSource();
        StatusTextBlock.Text = "Scanning memory…";
        ScanProgressBar.Value = 0;
        ScanProgressTextBlock.Text = "0 addresses found — 0.0%";

        try
        {
            _candidateAddresses = await Task.Run(() => scan(_scanCancellation.Token));
            RefreshDisplayedResults();
            NextScanButton.IsEnabled = _candidateAddresses.Count > 0;
            StatusTextBlock.Text = _scanCancellation.IsCancellationRequested
                ? $"Scan cancelled — kept {_candidateAddresses.Count:N0} matches"
                : _candidateAddresses.Count > DisplayLimit
                ? $"Found {_candidateAddresses.Count:N0} matches (showing first {DisplayLimit:N0})"
                : $"Found {_candidateAddresses.Count:N0} matches";
        }
        catch (OperationCanceledException)
        {
            StatusTextBlock.Text = "Scan cancelled";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            _scanCancellation.Dispose();
            _scanCancellation = null;
            SetScanning(false);
        }
    }

    private void UpdateScanProgress(ScanProgress progress)
    {
        ScanProgressBar.Value = progress.Percentage;
        ScanProgressTextBlock.Text = $"{progress.MatchCount:N0} addresses found — {progress.Percentage:F1}%";
    }

    private void RefreshDisplayedResults()
    {
        _results.Clear();
        var size = ValueConverter.SizeOf(_scanType);
        foreach (var address in _candidateAddresses.Take(DisplayLimit))
        {
            var value = _memory.TryRead(address, size, out var bytes)
                ? ValueConverter.Format(bytes, _scanType, _scanEndianness)
                : "<unreadable>";
            _results.Add(new ScanResult { Address = address, Value = value });
        }
    }

    private void WriteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (ResultsGrid.SelectedItem is not ScanResult result)
        {
            MessageBox.Show(this, "Select a result to write.", "Memory Scanner", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var bytes = ValueConverter.Parse(WriteValueTextBox.Text, _scanType, _scanEndianness);
            _memory.Write(result.Address, bytes);
            result.Value = ValueConverter.Format(bytes, _scanType, _scanEndianness);
            ResultsGrid.Items.Refresh();
            StatusTextBlock.Text = $"Wrote {result.Value} to {result.AddressText}";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void SaveAddress_Click(object sender, RoutedEventArgs e)
    {
        if (ResultsGrid.SelectedItem is not ScanResult result || _attachedProcess is null)
        {
            MessageBox.Show(this, "Select a result to save.", "Memory Scanner", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _savedAddresses.Add(new SavedAddress
        {
            Description = $"{_scanType} value",
            ProcessName = _attachedProcess.Name,
            Address = result.Address,
            ValueType = _scanType,
            Endianness = _scanEndianness,
        });
        SaveSavedAddresses();
        StatusTextBlock.Text = $"Saved {result.AddressText}";
    }

    private void CancelScan_Click(object sender, RoutedEventArgs e) => _scanCancellation?.Cancel();

    private void SetScanning(bool scanning)
    {
        FirstScanButton.IsEnabled = !scanning;
        NextScanButton.IsEnabled = !scanning && _candidateAddresses.Count > 0;
        CancelScanButton.IsEnabled = scanning;
        ProcessComboBox.IsEnabled = !scanning;
        ValueTypeComboBox.IsEnabled = !scanning;
        EndiannessComboBox.IsEnabled = !scanning;
    }

    private void ResetScan()
    {
        _candidateAddresses.Clear();
        _results.Clear();
        NextScanButton.IsEnabled = false;
    }

    private void LoadSavedAddresses()
    {
        try
        {
            if (!File.Exists(_savedAddressesPath))
                return;
            var items = JsonSerializer.Deserialize<List<SavedAddress>>(File.ReadAllText(_savedAddressesPath)) ?? [];
            foreach (var item in items)
                _savedAddresses.Add(item);
        }
        catch
        {
            StatusTextBlock.Text = "Saved addresses could not be loaded";
        }
    }

    private void SaveSavedAddresses()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_savedAddressesPath)!);
        File.WriteAllText(_savedAddressesPath, JsonSerializer.Serialize(_savedAddresses, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void ShowError(Exception ex)
    {
        StatusTextBlock.Text = ex.Message;
        MessageBox.Show(this, ex.Message, "Memory Scanner", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _scanCancellation?.Cancel();
        _memory.Dispose();
    }
}
