# Photo Sorting Feature — Design Spec

**Date:** 2026-06-25  
**Status:** Approved

## Goal

Implement photo sorting (copy or move) into date-based folder structures derived from EXIF metadata, matching and exceeding PhotoMove 2.5 — all options available, no artificial free-tier limits.

---

## Core Layer (`OpenPhotoSort.Core`)

### New: `ScanResult` record

```csharp
public record ScanResult(
    IReadOnlyList<string> WithValidExifDate,
    IReadOnlyList<string> WithExifButNoDate,
    IReadOnlyList<string> NoExif
)
{
    public int TotalFiles => WithValidExifDate.Count + WithExifButNoDate.Count + NoExif.Count;
}
```

### New: `PhotoScanner`

Static class. Entry point:

```csharp
public static Task<ScanResult> ScanAsync(
    string sourceFolder,
    bool includeSubfolders,
    CancellationToken cancellationToken = default)
```

- Collects files matching extensions: `.jpg`, `.jpeg`, `.png`, `.bmp`, `.gif`, `.heic`
- For each file calls `ImageHelper.ReadExifData()` and categorises into one of three buckets:
  - `WithValidExifDate` — EXIF profile present, `DateTimeOriginal` or `DateTime` tag parseable
  - `WithExifButNoDate` — EXIF profile present but no readable date tag
  - `NoExif` — no EXIF profile

### New: `SortOptions` record

```csharp
public record SortOptions(
    string SourceFolder,
    string DestinationFolder,
    FolderPattern FolderPattern,
    ConflictBehavior ConflictBehavior,
    string DuplicatesFolderPath,        // used when ConflictBehavior == DuplicatesFolder
    bool UseFileDateForNoExif,          // use file's LastWriteTime when no EXIF date
    bool DumpNoExifToFolder,            // copy/move no-EXIF files to NoExifFolderPath
    string NoExifFolderPath,            // used when DumpNoExifToFolder == true
    bool IncludeSubfolders,
    SortOperation Operation
);
```

### New enums

```csharp
public enum FolderPattern
{
    // Matches PhotoMove's available options (free and pro combined)
    YearMonthDay,               // 2024/2024_06/2024_06_25/              (default)
    YearMonthDayCameraModel,    // 2024/2024_06/2024_06_25/Canon EOS R5/
    YearMonth,                  // 2024/2024_06/
    YearMonthCameraModel,       // 2024/2024_06/Canon EOS R5/
    CameraModelYearMonthDay,    // Canon EOS R5/2024/2024_06/2024_06_25/
    CameraModelYearMonth,       // Canon EOS R5/2024/2024_06/
    YearOnly,                   // 2024/
    MonthOnly,                  // 2024_06/
    DayOnly,                    // 2024_06_25/
    CameraModelYear             // Canon EOS R5/2024/
}

public enum ConflictBehavior
{
    DoNotCopyOrMove,    // skip the file entirely           (default, matches PhotoMove free)
    RenameCopy,         // add '-Copy001' suffix before ext
    Overwrite,          // replace existing file
    DuplicatesFolder    // move/copy to a separate duplicates folder
}

public enum SortOperation { Copy, Move }
```

**Camera model fallback:** if the EXIF `Model` tag is absent or empty, use `"UnknownCamera"` as the folder name for any `FolderPattern` that includes a camera model segment.

### New: `SortProgress` record

```csharp
public record SortProgress(int Processed, int Total, string CurrentFile);
```

### New: `SortSummary` record

```csharp
public record SortSummary(int Moved, int Copied, int Skipped, int Renamed, int Failed);
```

### New: `PhotoSorter`

```csharp
public static Task<SortSummary> SortAsync(
    SortOptions options,
    ScanResult scanResult,
    IProgress<SortProgress> progress,
    CancellationToken cancellationToken = default)
```

**Algorithm:**

1. Build the candidate file list and determine each file's destination date:
   - Files in `WithValidExifDate` → use EXIF `DateTimeOriginal` (preferred) or `DateTime`
   - Files in `WithExifButNoDate` + `NoExif`:
     - If `UseFileDateForNoExif` → use `File.GetLastWriteTime`, treat like dated files
     - If `DumpNoExifToFolder` → route to `NoExifFolderPath` (skip pattern logic)
     - If neither → skip the file (count as Skipped)
     - Both flags can be true simultaneously: `UseFileDateForNoExif` takes priority (file goes into structured folders using file date; `DumpNoExifToFolder` is ignored for that file)

2. For each candidate file, resolve the destination path:
   - Build subfolder string from `FolderPattern`, date, and (if needed) EXIF `Model` tag
   - Full destination = `DestinationFolder / <pattern-subfolders> / filename`
   - For dump files: full destination = `NoExifFolderPath / filename`

3. Apply `ConflictBehavior` if a file already exists at the destination:
   - `DoNotCopyOrMove` → count as Skipped, continue
   - `RenameCopy` → insert `-Copy001`, `-Copy002`, … before the extension until name is free
   - `Overwrite` → proceed; existing file is replaced
   - `DuplicatesFolder` → write to `DuplicatesFolderPath / filename` instead (apply same rename logic there if another conflict exists)

4. Execute the file operation:
   - `Copy` → `File.Copy`
   - `Move` → `File.Copy` then `File.Delete` (delete source only after destination write is confirmed)

5. Report progress after each file via `IProgress<SortProgress>`.

6. Accumulate counts into `SortSummary`.

**Error handling:**
- Per-file exceptions (locked, permissions) → increment `Failed`, continue
- Destination folder creation failure → throw, caller shows alert and aborts
- Source file disappears mid-operation → count as `Failed`, continue

---

## UI Layer (`OpenPhotoSort.UI`)

### File changes

| File | Action |
|---|---|
| `MainPage.xaml` | Replace entirely |
| `MainPage.xaml.cs` | Replace entirely |
| `ViewModels/MainViewModel.cs` | Create — holds all state and delegates to Core |

### `MainViewModel` properties

```csharp
// Source / destination
string SourceFolder
string DestinationFolder
bool IncludeSubfolders

// Options
FolderPattern FolderPattern               // default: YearMonthDay
ConflictBehavior ConflictBehavior         // default: DoNotCopyOrMove
string DuplicatesFolderPath               // default: ""
bool UseFileDateForNoExif                 // default: false
bool DumpNoExifToFolder                   // default: false
string NoExifFolderPath                   // default: ""

// Scan results (displayed after Find Photos)
int TotalFiles
int FilesWithValidDate
int FilesWithExifButNoDate
int FilesNoExif
bool IsScanComplete

// Operation state
bool IsScanning
bool IsSorting
bool IsBusy              // IsScanning || IsSorting
double SortProgress      // 0.0–1.0
string StatusText
bool ShowSummaryButton

// Commands
ICommand BrowseSourceCommand
ICommand BrowseDestinationCommand
ICommand BrowseDuplicatesFolderCommand
ICommand BrowseNoExifFolderCommand
ICommand FindPhotosCommand
ICommand CopyCommand
ICommand MoveCommand
ICommand CancelCommand
ICommand ShowSummaryCommand
```

### `MainPage.xaml` layout

Two-column layout matching PhotoMove's visual style. The page is a horizontal `Grid` with two equal columns. The window minimum size is 900×600.

**Left column — Numbered steps**

*Step 1: Choose folder with photos to process*
- Read-only `Entry` showing source folder path
- `Button` "Click Here to Choose Folder to Search"
- `CheckBox` "Include Sub Folders"

*Step 2: Set destination folder*
- Read-only `Entry` showing destination folder path
- `Button` "Click Here to Choose Output Folder"

*Step 3: Find photos*
- `Button` "Find Photos" (enabled when both folders are set; disabled while scanning or sorting)
- Four stat rows, each: small fixed-width bordered `Label` (count) + wider `Label` (description):
  - "Total Files Checked In All Folders"
  - "Have Valid Date Created in File's Internal Exif Data"
  - "Have Exif Data But No Valid Creation Date"
  - "No Exif Data"
- `ProgressBar` + status `Label` (visible during sort operation)

*Step 4: Copy or move*
- `Button` "COPY to Destination Folders" (enabled after scan, disabled during operation)
- `Button` "MOVE to Destination Folders" (enabled after scan, disabled during operation)
- `Button` "Cancel" (visible and enabled only while `IsSorting`; hidden otherwise)

*Bottom bar*
- `Button` "Show Summary Report" (enabled after operation completes)

**Right column — Options panel**

*Output Folder Structure*
- `Picker` with 10 options, default `Yr, Mo, Day`

*If file to be moved/copied exists in the destination folder*
- `Picker` — Do Not Move or Copy / Add '-Copy###' then Move or Copy / Overwrite Existing File / Move to Duplicates Folder
- Read-only `Entry` + `Button` ">" for duplicates folder path (enabled only when "Move to Duplicates Folder" selected)

*Option for files with no EXIF date created*
- `CheckBox` "Use File Date to Move or Copy to Structured Folders"
- `CheckBox` "Copy or Move to This Folder:"
- Read-only `Entry` + `Button` ">" for no-EXIF folder path (enabled only when second checkbox ticked)

### Settings persistence

All user-configurable values are saved to `Microsoft.Maui.Storage.Preferences` (Windows Registry / NSUserDefaults on macOS) and restored on next launch. No separate settings file needed.

Persisted keys (all prefixed `ops_`):

| Key | Type | Default |
|---|---|---|
| `ops_sourceFolder` | string | `""` |
| `ops_destFolder` | string | `""` |
| `ops_includeSubfolders` | bool | `false` |
| `ops_folderPattern` | int (enum) | `0` (YearMonthDay) |
| `ops_conflictBehavior` | int (enum) | `0` (DoNotCopyOrMove) |
| `ops_duplicatesFolderPath` | string | `""` |
| `ops_useFileDateForNoExif` | bool | `false` |
| `ops_dumpNoExifToFolder` | bool | `false` |
| `ops_noExifFolderPath` | string | `""` |

`MainViewModel` loads preferences in its constructor and saves each value in the setter of the corresponding `[ObservableProperty]` (via `partial void On<Prop>Changed`).

### MVVM wiring

`MainPage.xaml.cs` constructor sets `BindingContext = new MainViewModel()`. All UI state driven by bindings. `MainViewModel` uses `CommunityToolkit.Mvvm` source generators (`[ObservableProperty]`, `[RelayCommand]`).

---

## Data Flow

### Scan
1. `FindPhotosCommand` → `IsScanning = true`, `StatusText = "Scanning…"`
2. `PhotoScanner.ScanAsync()` on background thread
3. Result populates stat properties, `IsScanComplete = true`, `IsScanning = false`

### Sort
1. `CopyCommand` or `MoveCommand` → `IsSorting = true`, creates a new `CancellationTokenSource`, disables all controls except `Cancel`
2. `PhotoSorter.SortAsync()` on background thread with `IProgress<SortProgress>` and the token
3. Progress reports update `SortProgress` and `StatusText` on main thread
4. `CancelCommand` → calls `CancellationTokenSource.Cancel()`; `SortAsync` throws `OperationCanceledException` which the ViewModel catches
5. On completion (normal or cancelled): `IsSorting = false`, `ShowSummaryButton = true`, stores `SortSummary`; if cancelled, `StatusText = "Cancelled — N of M files processed"`
6. `ShowSummaryCommand` → `DisplayAlertAsync` with final counts (including partial results if cancelled)

---

## Out of Scope

- Duplicate detection by file content (hash comparison)
- Video files
- Custom free-text folder pattern strings
- Undo / rollback of move operations
