using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using FolderComparer.Models;
using FolderComparer.Services;
using Microsoft.Win32;

namespace FolderComparer.ViewModels;

public class MainViewModel : ViewModelBase
{
    private const int MaxRecentPairs = 5;
    private readonly FolderComparisonService _comparisonService = new();
    private readonly SyncService _syncService = new();
    private CancellationTokenSource? _cts;

    private string _sourcePath = string.Empty;
    public string SourcePath
    {
        get => _sourcePath;
        set => SetProperty(ref _sourcePath, value);
    }

    private string _destinationPath = string.Empty;
    public string DestinationPath
    {
        get => _destinationPath;
        set => SetProperty(ref _destinationPath, value);
    }

    private string _statusText = "Ready";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private string _summaryText = string.Empty;
    public string SummaryText
    {
        get => _summaryText;
        set => SetProperty(ref _summaryText, value);
    }

    private bool _isComparing;
    public bool IsComparing
    {
        get => _isComparing;
        set
        {
            SetProperty(ref _isComparing, value);
            OnPropertyChanged(nameof(IsNotComparing));
        }
    }

    public bool IsNotComparing => !IsComparing;

    private string _filterText = string.Empty;
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
                ApplyFilter();
        }
    }

    private bool _showMatches = true;
    public bool ShowMatches
    {
        get => _showMatches;
        set { if (SetProperty(ref _showMatches, value)) ApplyFilter(); }
    }

    private bool _showSourceOnly = true;
    public bool ShowSourceOnly
    {
        get => _showSourceOnly;
        set { if (SetProperty(ref _showSourceOnly, value)) ApplyFilter(); }
    }

    private bool _showDestOnly = true;
    public bool ShowDestOnly
    {
        get => _showDestOnly;
        set { if (SetProperty(ref _showDestOnly, value)) ApplyFilter(); }
    }

    private bool _showDifferent = true;
    public bool ShowDifferent
    {
        get => _showDifferent;
        set { if (SetProperty(ref _showDifferent, value)) ApplyFilter(); }
    }

    private bool _overwriteMismatches;
    public bool OverwriteMismatches
    {
        get => _overwriteMismatches;
        set => SetProperty(ref _overwriteMismatches, value);
    }

    private bool _isSyncing;
    public bool IsSyncing
    {
        get => _isSyncing;
        set
        {
            SetProperty(ref _isSyncing, value);
            OnPropertyChanged(nameof(IsNotSyncing));
        }
    }

    public bool IsNotSyncing => !IsSyncing;

    private RecentPair? _selectedRecentPair;
    public RecentPair? SelectedRecentPair
    {
        get => _selectedRecentPair;
        set => SetProperty(ref _selectedRecentPair, value);
    }

    private List<FileComparisonResult> _allResults = [];
    public ObservableCollection<FileComparisonResult> Results { get; } = [];
    public ObservableCollection<RecentPair> RecentPairs { get; } = [];

    public RelayCommand BrowseSourceCommand { get; }
    public RelayCommand BrowseDestinationCommand { get; }
    public RelayCommand CompareCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand SyncBidirectionalCommand { get; }
    public RelayCommand SyncSourceToDestCommand { get; }
    public RelayCommand SyncDestToSourceCommand { get; }
    public RelayCommand UseRecentPairCommand { get; }
    public RelayCommand CompareRecentPairCommand { get; }
    public RelayCommand SyncRecentPairCommand { get; }

    public MainViewModel()
    {
        BrowseSourceCommand = new RelayCommand(_ => BrowseFolder(path => SourcePath = path));
        BrowseDestinationCommand = new RelayCommand(_ => BrowseFolder(path => DestinationPath = path));
        CompareCommand = new RelayCommand(async _ => await RunComparisonAsync(), _ => IsNotComparing && IsNotSyncing);
        CancelCommand = new RelayCommand(_ => _cts?.Cancel(), _ => IsComparing || IsSyncing);
        SyncBidirectionalCommand = new RelayCommand(async _ => await RunSyncAsync(SyncDirection.Bidirectional), _ => IsNotComparing && IsNotSyncing);
        SyncSourceToDestCommand = new RelayCommand(async _ => await RunSyncAsync(SyncDirection.SourceToDestination), _ => IsNotComparing && IsNotSyncing);
        SyncDestToSourceCommand = new RelayCommand(async _ => await RunSyncAsync(SyncDirection.DestinationToSource), _ => IsNotComparing && IsNotSyncing);
        UseRecentPairCommand = new RelayCommand(_ => ApplySelectedRecentPair(), _ => SelectedRecentPair is not null && IsNotComparing && IsNotSyncing);
        CompareRecentPairCommand = new RelayCommand(async _ => await RunRecentPairComparisonAsync(), _ => SelectedRecentPair is not null && IsNotComparing && IsNotSyncing);
        SyncRecentPairCommand = new RelayCommand(async _ => await RunRecentPairSyncAsync(), _ => SelectedRecentPair is not null && IsNotComparing && IsNotSyncing);

        LoadRecentPairs();
    }

    private static string RecentPairsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FolderComparer",
        "recent-sync-pairs.json");

    private static void BrowseFolder(Action<string> setPath)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Folder"
        };

        if (dialog.ShowDialog() == true)
        {
            setPath(dialog.FolderName);
        }
    }

    private async Task RunComparisonAsync()
    {
        if (string.IsNullOrWhiteSpace(SourcePath) || string.IsNullOrWhiteSpace(DestinationPath))
        {
            MessageBox.Show("Please specify both source and destination folder paths.",
                "Missing Paths", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AddOrUpdateRecentPair(SourcePath, DestinationPath);

        IsComparing = true;
        Results.Clear();
        _allResults.Clear();
        SummaryText = string.Empty;
        _cts = new CancellationTokenSource();

        var progress = new Progress<string>(msg => StatusText = msg);

        try
        {
            var (results, summary) = await _comparisonService.CompareAsync(
                SourcePath, DestinationPath, progress, _cts.Token);

            _allResults = results;
            ApplyFilter();

            SummaryText = $"Total: {summary.TotalFiles}  |  " +
                          $"Match: {summary.MatchCount}  |  " +
                          $"Source only: {summary.SourceOnlyCount}  |  " +
                          $"Dest only: {summary.DestinationOnlyCount}  |  " +
                          $"Different: {summary.DifferentCount}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Comparison cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = "Error occurred.";
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsComparing = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    private void ApplyFilter()
    {
        Results.Clear();

        foreach (var result in _allResults)
        {
            if (!PassesStatusFilter(result.Status))
                continue;

            if (!string.IsNullOrWhiteSpace(FilterText) &&
                !result.RelativePath.Contains(FilterText, StringComparison.OrdinalIgnoreCase))
                continue;

            Results.Add(result);
        }
    }

    private bool PassesStatusFilter(ComparisonStatus status)
    {
        return status switch
        {
            ComparisonStatus.Match => ShowMatches,
            ComparisonStatus.SourceOnly => ShowSourceOnly,
            ComparisonStatus.DestinationOnly => ShowDestOnly,
            ComparisonStatus.SizeMismatch or
            ComparisonStatus.DateMismatch or
            ComparisonStatus.SizeAndDateMismatch => ShowDifferent,
            _ => true
        };
    }

    private async Task RunSyncAsync(SyncDirection direction)
    {
        if (string.IsNullOrWhiteSpace(SourcePath) || string.IsNullOrWhiteSpace(DestinationPath))
        {
            MessageBox.Show("Please specify both source and destination folder paths.",
                "Missing Paths", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AddOrUpdateRecentPair(SourcePath, DestinationPath);

        string directionText = direction switch
        {
            SyncDirection.SourceToDestination => "Copy from Source to Destination",
            SyncDirection.DestinationToSource => "Copy from Destination to Source",
            SyncDirection.Bidirectional => "Sync Bidirectionally",
            _ => "Sync"
        };

        var confirmResult = MessageBox.Show(
            $"This will {directionText}.\n\n" +
            (OverwriteMismatches ? "Files with different sizes/dates will be overwritten." : "Files with different sizes/dates will NOT be overwritten.") +
            "\n\nContinue?",
            "Confirm Sync", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirmResult != MessageBoxResult.Yes)
            return;

        IsSyncing = true;
        _cts = new CancellationTokenSource();

        var progress = new Progress<SyncProgress>(msg =>
        {
            StatusText = msg.Status;
        });

        try
        {
            var result = await _syncService.SyncAsync(
                SourcePath, DestinationPath, direction, OverwriteMismatches, progress, _cts.Token);

            StatusText = $"Sync complete. {result.Summary}";
            MessageBox.Show($"Sync completed successfully.\n\n{result.Summary}", "Sync Complete", 
                MessageBoxButton.OK, MessageBoxImage.Information);

            if (result.Errors.Count > 0)
            {
                string errorMsg = "Errors occurred:\n\n" + 
                    string.Join("\n", result.Errors.Take(5).Select(e => $"{e.FilePath}: {e.Message}"));
                if (result.Errors.Count > 5)
                    errorMsg += $"\n\n... and {result.Errors.Count - 5} more errors";
                
                MessageBox.Show(errorMsg, "Sync Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Re-run comparison to show updated results
            await RunComparisonAsync();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Sync cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = "Sync error occurred.";
            MessageBox.Show(ex.Message, "Sync Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsSyncing = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    private void ApplySelectedRecentPair()
    {
        if (SelectedRecentPair is null)
            return;

        SourcePath = SelectedRecentPair.SourcePath;
        DestinationPath = SelectedRecentPair.DestinationPath;
        StatusText = "Applied selected recent pair.";
    }

    private async Task RunRecentPairComparisonAsync()
    {
        ApplySelectedRecentPair();
        await RunComparisonAsync();
    }

    private async Task RunRecentPairSyncAsync()
    {
        ApplySelectedRecentPair();
        await RunSyncAsync(SyncDirection.Bidirectional);
    }

    private void AddOrUpdateRecentPair(string sourcePath, string destinationPath)
    {
        string normalizedSource = sourcePath.Trim();
        string normalizedDestination = destinationPath.Trim();

        if (string.IsNullOrWhiteSpace(normalizedSource) || string.IsNullOrWhiteSpace(normalizedDestination))
            return;

        var existing = RecentPairs.FirstOrDefault(pair =>
            string.Equals(pair.SourcePath, normalizedSource, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(pair.DestinationPath, normalizedDestination, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.LastUsedUtc = DateTime.UtcNow;
            RecentPairs.Remove(existing);
            RecentPairs.Insert(0, existing);
        }
        else
        {
            RecentPairs.Insert(0, new RecentPair
            {
                SourcePath = normalizedSource,
                DestinationPath = normalizedDestination,
                LastUsedUtc = DateTime.UtcNow
            });
        }

        while (RecentPairs.Count > MaxRecentPairs)
        {
            RecentPairs.RemoveAt(RecentPairs.Count - 1);
        }

        SaveRecentPairs();
    }

    private void LoadRecentPairs()
    {
        try
        {
            if (!File.Exists(RecentPairsFilePath))
                return;

            var json = File.ReadAllText(RecentPairsFilePath);
            var pairs = JsonSerializer.Deserialize<List<RecentPair>>(json) ?? [];

            foreach (var pair in pairs
                .Where(p => !string.IsNullOrWhiteSpace(p.SourcePath) && !string.IsNullOrWhiteSpace(p.DestinationPath))
                .OrderByDescending(p => p.LastUsedUtc)
                .Take(MaxRecentPairs))
            {
                RecentPairs.Add(pair);
            }
        }
        catch
        {
            // Ignore load errors to keep app usable.
        }
    }

    private void SaveRecentPairs()
    {
        try
        {
            var folder = Path.GetDirectoryName(RecentPairsFilePath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var pairs = RecentPairs.Take(MaxRecentPairs).ToList();
            var json = JsonSerializer.Serialize(pairs, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(RecentPairsFilePath, json);
        }
        catch
        {
            // Ignore save errors to keep app usable.
        }
    }
}

public class RecentPair
{
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public DateTime LastUsedUtc { get; set; }

    public string DisplayText => $"{SourcePath}  <->  {DestinationPath}";
}
