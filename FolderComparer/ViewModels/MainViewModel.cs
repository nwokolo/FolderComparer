using System.Collections.ObjectModel;
using System.Windows;
using FolderComparer.Models;
using FolderComparer.Services;
using Microsoft.Win32;

namespace FolderComparer.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly FolderComparisonService _comparisonService = new();
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

    private List<FileComparisonResult> _allResults = [];
    public ObservableCollection<FileComparisonResult> Results { get; } = [];

    public RelayCommand BrowseSourceCommand { get; }
    public RelayCommand BrowseDestinationCommand { get; }
    public RelayCommand CompareCommand { get; }
    public RelayCommand CancelCommand { get; }

    public MainViewModel()
    {
        BrowseSourceCommand = new RelayCommand(_ => BrowseFolder(path => SourcePath = path));
        BrowseDestinationCommand = new RelayCommand(_ => BrowseFolder(path => DestinationPath = path));
        CompareCommand = new RelayCommand(async _ => await RunComparisonAsync(), _ => IsNotComparing);
        CancelCommand = new RelayCommand(_ => _cts?.Cancel(), _ => IsComparing);
    }

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
}
