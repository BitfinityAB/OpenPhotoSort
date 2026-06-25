using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using OpenPhotoSort.Core;
using OpenPhotoSort.Helpers;

namespace OpenPhotoSort.ViewModels;

public partial class MainViewModel : ObservableObject
{
    // --- Static picker data ---

    public static IReadOnlyList<string> FolderPatternOptions { get; } = new[]
    {
        "Yr, Mo, Day",
        "Yr, Mo, Day, Camera Model",
        "Yr, Mo",
        "Yr, Mo, Camera Model",
        "Camera Model, Yr, Mo, Day",
        "Camera Model, Yr, Mo",
        "Year Only",
        "Month Only",
        "Day Only",
        "Camera Model, Yr"
    };

    public static IReadOnlyList<string> ConflictBehaviorOptions { get; } = new[]
    {
        "Do Not Move or Copy",
        "Add '-Copy###', then Move or Copy",
        "Overwrite the Existing File",
        "Move to Duplicates Folder"
    };

    // --- Observable properties (partial property syntax — AOT compatible) ---

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DuplicatesFolderEnabled))]
    [NotifyCanExecuteChangedFor(nameof(FindPhotosCommand))]
    public partial string SourceFolder { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindPhotosCommand))]
    public partial string DestinationFolder { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IncludeSubfolders { get; set; }

    [ObservableProperty]
    public partial int SelectedFolderPatternIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DuplicatesFolderEnabled))]
    public partial int SelectedConflictBehaviorIndex { get; set; }

    [ObservableProperty]
    public partial string DuplicatesFolderPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool UseFileDateForNoExif { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoExifFolderEnabled))]
    public partial bool DumpNoExifToFolder { get; set; }

    [ObservableProperty]
    public partial string NoExifFolderPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int TotalFiles { get; set; }

    [ObservableProperty]
    public partial int FilesWithValidDate { get; set; }

    [ObservableProperty]
    public partial int FilesWithExifButNoDate { get; set; }

    [ObservableProperty]
    public partial int FilesNoExif { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindPhotosCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveCommand))]
    [NotifyPropertyChangedFor(nameof(CanOperate))]
    public partial bool IsScanComplete { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindPhotosCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveCommand))]
    [NotifyPropertyChangedFor(nameof(CanOperate))]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    public partial bool IsScanning { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindPhotosCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveCommand))]
    [NotifyPropertyChangedFor(nameof(CanOperate))]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    public partial bool IsSorting { get; set; }

    [ObservableProperty]
    public partial double OperationProgress { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusVisible))]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowSummaryButton { get; set; }

    // --- Computed properties ---

    public bool IsBusy => IsScanning || IsSorting;
    public bool CanOperate => IsScanComplete && !IsBusy;
    public bool IsStatusVisible => !string.IsNullOrEmpty(StatusText);
    public bool DuplicatesFolderEnabled =>
        SelectedConflictBehaviorIndex == (int)ConflictBehavior.DuplicatesFolder;
    public bool NoExifFolderEnabled => DumpNoExifToFolder;

    // --- State ---

    private ScanResult? _lastScan;
    private SortSummary? _lastSummary;
    private CancellationTokenSource? _cts;

    // --- Constructor: load preferences ---

    public MainViewModel()
    {
        SourceFolder             = Preferences.Get("ops_sourceFolder", string.Empty);
        DestinationFolder        = Preferences.Get("ops_destFolder", string.Empty);
        IncludeSubfolders        = Preferences.Get("ops_includeSubfolders", false);
        SelectedFolderPatternIndex    = Preferences.Get("ops_folderPattern", 0);
        SelectedConflictBehaviorIndex = Preferences.Get("ops_conflictBehavior", 0);
        DuplicatesFolderPath     = Preferences.Get("ops_duplicatesFolderPath", string.Empty);
        UseFileDateForNoExif     = Preferences.Get("ops_useFileDateForNoExif", false);
        DumpNoExifToFolder       = Preferences.Get("ops_dumpNoExifToFolder", false);
        NoExifFolderPath         = Preferences.Get("ops_noExifFolderPath", string.Empty);
    }

    // --- Preferences save via On{Prop}Changed ---

    partial void OnSourceFolderChanged(string value) =>
        Preferences.Set("ops_sourceFolder", value);
    partial void OnDestinationFolderChanged(string value) =>
        Preferences.Set("ops_destFolder", value);
    partial void OnIncludeSubfoldersChanged(bool value) =>
        Preferences.Set("ops_includeSubfolders", value);
    partial void OnSelectedFolderPatternIndexChanged(int value) =>
        Preferences.Set("ops_folderPattern", value);
    partial void OnSelectedConflictBehaviorIndexChanged(int value) =>
        Preferences.Set("ops_conflictBehavior", value);
    partial void OnDuplicatesFolderPathChanged(string value) =>
        Preferences.Set("ops_duplicatesFolderPath", value);
    partial void OnUseFileDateForNoExifChanged(bool value) =>
        Preferences.Set("ops_useFileDateForNoExif", value);
    partial void OnDumpNoExifToFolderChanged(bool value) =>
        Preferences.Set("ops_dumpNoExifToFolder", value);
    partial void OnNoExifFolderPathChanged(string value) =>
        Preferences.Set("ops_noExifFolderPath", value);

    // --- Commands ---

    [RelayCommand]
    private async Task BrowseSourceAsync()
    {
        var path = await new FolderPickerX().PickFolderAsync(CancellationToken.None);
        if (!string.IsNullOrEmpty(path)) SourceFolder = path;
    }

    [RelayCommand]
    private async Task BrowseDestinationAsync()
    {
        var path = await new FolderPickerX().PickFolderAsync(CancellationToken.None);
        if (!string.IsNullOrEmpty(path)) DestinationFolder = path;
    }

    [RelayCommand]
    private async Task BrowseDuplicatesFolderAsync()
    {
        var path = await new FolderPickerX().PickFolderAsync(CancellationToken.None);
        if (!string.IsNullOrEmpty(path)) DuplicatesFolderPath = path;
    }

    [RelayCommand]
    private async Task BrowseNoExifFolderAsync()
    {
        var path = await new FolderPickerX().PickFolderAsync(CancellationToken.None);
        if (!string.IsNullOrEmpty(path)) NoExifFolderPath = path;
    }

    [RelayCommand(CanExecute = nameof(CanFindPhotos))]
    private async Task FindPhotosAsync()
    {
        IsScanning = true;
        IsScanComplete = false;
        ShowSummaryButton = false;
        StatusText = "Scanning…";
        TotalFiles = FilesWithValidDate = FilesWithExifButNoDate = FilesNoExif = 0;

        try
        {
            _lastScan = await PhotoScanner.ScanAsync(SourceFolder, IncludeSubfolders);
            TotalFiles = _lastScan.TotalFiles;
            FilesWithValidDate = _lastScan.WithValidExifDate.Count;
            FilesWithExifButNoDate = _lastScan.WithExifButNoDate.Count;
            FilesNoExif = _lastScan.NoExif.Count;
            IsScanComplete = true;
            StatusText = $"Found {TotalFiles} file(s).";
        }
        catch (Exception ex)
        {
            StatusText = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private bool CanFindPhotos =>
        !string.IsNullOrEmpty(SourceFolder) &&
        !string.IsNullOrEmpty(DestinationFolder) &&
        !IsBusy;

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private Task CopyAsync() => RunSortAsync(SortOperation.Copy);

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private Task MoveAsync() => RunSortAsync(SortOperation.Move);

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private async Task ShowSummaryAsync()
    {
        if (_lastSummary is null) return;
        var s = _lastSummary;
        string msg = $"Copied:  {s.Copied}\n" +
                     $"Moved:   {s.Moved}\n" +
                     $"Skipped: {s.Skipped}\n" +
                     $"Renamed: {s.Renamed}\n" +
                     $"Failed:  {s.Failed}";
        await Shell.Current.DisplayAlertAsync("Summary", msg, "OK");
    }

    private async Task RunSortAsync(SortOperation operation)
    {
        if (_lastScan is null) return;

        _cts = new CancellationTokenSource();
        _lastSummary = null;
        IsSorting = true;
        OperationProgress = 0;
        ShowSummaryButton = false;
        StatusText = operation == SortOperation.Copy ? "Copying…" : "Moving…";

        var options = new SortOptions(
            SourceFolder, DestinationFolder,
            (FolderPattern)SelectedFolderPatternIndex,
            (ConflictBehavior)SelectedConflictBehaviorIndex,
            DuplicatesFolderPath,
            UseFileDateForNoExif, DumpNoExifToFolder, NoExifFolderPath,
            IncludeSubfolders, operation);

        var progress = new Progress<SortProgress>(p =>
        {
            OperationProgress = p.Total > 0 ? (double)p.Processed / p.Total : 0;
            StatusText = $"{p.Processed} / {p.Total}  {p.CurrentFile}";
        });

        bool cancelled = false;
        try
        {
            _lastSummary = await PhotoSorter.SortAsync(options, _lastScan, progress, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            _lastSummary = new SortSummary(0, 0, 0, 0, 0);
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsSorting = false;
            _cts.Dispose();
            _cts = null;
        }

        ShowSummaryButton = _lastSummary is not null;

        if (cancelled)
            StatusText = "Cancelled.";
        else if (_lastSummary is not null)
            StatusText = $"Done. {_lastSummary.Copied + _lastSummary.Moved} file(s) processed.";
    }
}
