# Photo Sorting Feature — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add photo scanning and date-based sorting (copy or move) to OpenPhotoSort, with a two-column PhotoMove-style UI and full settings persistence.

**Architecture:** Extend `OpenPhotoSort.Core` with `PhotoScanner` and `PhotoSorter` static classes backed by records and enums, then completely replace `MainPage` with a two-column XAML layout driven by `MainViewModel` (CommunityToolkit.Mvvm source generators). Sort is two-phase: scan first to populate stats, then copy/move with live progress and cancel support.

**Tech Stack:** .NET 10, .NET MAUI, Magick.NET 14.14.0 (already installed), CommunityToolkit.Mvvm 8.4.2, CommunityToolkit.Maui 14.2.0, xUnit 2.x

## Global Constraints

- Core project target: `net10.0`; UI project Windows target: `net10.0-windows10.0.19041.0`
- Nullable reference types and implicit usings enabled everywhere
- Core namespace: `OpenPhotoSort.Core`; UI namespace: `OpenPhotoSort`; ViewModel namespace: `OpenPhotoSort.ViewModels`
- `ImageHelper.ReadExifData(string fileName)` — already in Core — returns `Dictionary<string, Tuple<string, string>>?` where key is `ExifTag.ToString()` (e.g. `"DateTimeOriginal"`, `"DateTime"`, `"Model"`) and value is `(DataType, ValueString)`
- EXIF date string format from Magick.NET: `"yyyy:MM:dd HH:mm:ss"` — parse with `DateTime.TryParseExact(..., CultureInfo.InvariantCulture, DateTimeStyles.None, ...)`
- EXIF date tag priority: `"DateTimeOriginal"` first, `"DateTime"` fallback
- Camera model tag key: `"Model"`; fallback when absent/empty: `"UnknownCamera"`
- Folder name date segments: year = `date.ToString("yyyy")`, year-month = `date.ToString("yyyy_MM")`, year-month-day = `date.ToString("yyyy_MM_dd")`
- Rename suffix format: `-Copy001`, `-Copy002`, … (zero-padded 3 digits, inserted before extension)
- Folder name sanitization: replace every char in `Path.GetInvalidFileNameChars()` with `_`
- Supported extensions (case-insensitive): `.jpg`, `.jpeg`, `.png`, `.bmp`, `.gif`, `.heic`
- Move operation = `File.Copy` then `File.Delete` (delete source only after copy confirmed)
- Preferences key prefix: `ops_`
- No git repo currently — commit steps are for when git is initialized

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `OpenPhotoSort.Tests/OpenPhotoSort.Tests.csproj` | Create | xUnit test runner config |
| `OpenPhotoSort.Tests/PhotoScannerTests.cs` | Create | Tests for PhotoScanner categorization |
| `OpenPhotoSort.Tests/PhotoSorterTests.cs` | Create | Tests for path building, conflict, sort loop |
| `OpenPhotoSort.Core/Models/ScanResult.cs` | Create | `ScanResult` record |
| `OpenPhotoSort.Core/Models/SortOptions.cs` | Create | `SortOptions` record + all enums (`FolderPattern`, `ConflictBehavior`, `SortOperation`) |
| `OpenPhotoSort.Core/Models/SortProgress.cs` | Create | `SortProgress` record |
| `OpenPhotoSort.Core/Models/SortSummary.cs` | Create | `SortSummary` record |
| `OpenPhotoSort.Core/PhotoScanner.cs` | Create | `PhotoScanner.ScanAsync` — enumerate files, read EXIF, bucket into three lists |
| `OpenPhotoSort.Core/PhotoSorter.cs` | Create | `PhotoSorter.SortAsync` — build work list, resolve paths, execute copy/move |
| `OpenPhotoSort.UI/ViewModels/MainViewModel.cs` | Create | All UI state, commands, preferences, `CancellationTokenSource` lifecycle |
| `OpenPhotoSort.UI/MainPage.xaml` | Replace | Two-column PhotoMove-style layout |
| `OpenPhotoSort.UI/MainPage.xaml.cs` | Replace | Sets `BindingContext = new MainViewModel()` only |
| `OpenPhotoSort.sln` | Modify | Add `OpenPhotoSort.Tests` project |

---

### Task 1: Test project + Core models

**Files:**
- Create: `OpenPhotoSort.Tests/OpenPhotoSort.Tests.csproj`
- Create: `OpenPhotoSort.Core/Models/ScanResult.cs`
- Create: `OpenPhotoSort.Core/Models/SortOptions.cs`
- Create: `OpenPhotoSort.Core/Models/SortProgress.cs`
- Create: `OpenPhotoSort.Core/Models/SortSummary.cs`
- Modify: `OpenPhotoSort.sln`

**Interfaces:**
- Produces: `ScanResult`, `SortOptions`, `FolderPattern`, `ConflictBehavior`, `SortOperation`, `SortProgress`, `SortSummary` — consumed by Tasks 2, 3, 4, 5

- [ ] **Step 1: Scaffold test project and add to solution**

Run from `C:\Project\OpenPhotoSort`:
```powershell
dotnet new xunit -n OpenPhotoSort.Tests --framework net10.0 -o OpenPhotoSort.Tests
dotnet sln OpenPhotoSort.sln add OpenPhotoSort.Tests/OpenPhotoSort.Tests.csproj
dotnet add OpenPhotoSort.Tests/OpenPhotoSort.Tests.csproj reference OpenPhotoSort.Core/OpenPhotoSort.Core.csproj
```

Expected: three commands succeed with no errors.

- [ ] **Step 2: Verify test project builds and runs**

```powershell
dotnet test OpenPhotoSort.Tests/OpenPhotoSort.Tests.csproj
```

Expected output contains:
```
Test run for OpenPhotoSort.Tests.dll (.NETCoreApp,Version=v10.0)
Passed! - Failed: 0, Passed: 1, Skipped: 0
```
(The `dotnet new xunit` scaffold includes one passing `UnitTest1` test.)

- [ ] **Step 3: Delete the scaffold test file**

Delete `OpenPhotoSort.Tests/UnitTest1.cs`. We will add real test files in Tasks 2 and 3.

- [ ] **Step 4: Create `ScanResult.cs`**

Create `OpenPhotoSort.Core/Models/ScanResult.cs`:
```csharp
namespace OpenPhotoSort.Core;

public record ScanResult(
    IReadOnlyList<string> WithValidExifDate,
    IReadOnlyList<string> WithExifButNoDate,
    IReadOnlyList<string> NoExif)
{
    public int TotalFiles => WithValidExifDate.Count + WithExifButNoDate.Count + NoExif.Count;
}
```

- [ ] **Step 5: Create `SortOptions.cs` with enums**

Create `OpenPhotoSort.Core/Models/SortOptions.cs`:
```csharp
namespace OpenPhotoSort.Core;

public enum FolderPattern
{
    YearMonthDay,               // 2024/2024_06/2024_06_25
    YearMonthDayCameraModel,    // 2024/2024_06/2024_06_25/Canon EOS R5
    YearMonth,                  // 2024/2024_06
    YearMonthCameraModel,       // 2024/2024_06/Canon EOS R5
    CameraModelYearMonthDay,    // Canon EOS R5/2024/2024_06/2024_06_25
    CameraModelYearMonth,       // Canon EOS R5/2024/2024_06
    YearOnly,                   // 2024
    MonthOnly,                  // 2024_06
    DayOnly,                    // 2024_06_25
    CameraModelYear             // Canon EOS R5/2024
}

public enum ConflictBehavior
{
    DoNotCopyOrMove,
    RenameCopy,
    Overwrite,
    DuplicatesFolder
}

public enum SortOperation { Copy, Move }

public record SortOptions(
    string SourceFolder,
    string DestinationFolder,
    FolderPattern FolderPattern,
    ConflictBehavior ConflictBehavior,
    string DuplicatesFolderPath,
    bool UseFileDateForNoExif,
    bool DumpNoExifToFolder,
    string NoExifFolderPath,
    bool IncludeSubfolders,
    SortOperation Operation);
```

- [ ] **Step 6: Create `SortProgress.cs`**

Create `OpenPhotoSort.Core/Models/SortProgress.cs`:
```csharp
namespace OpenPhotoSort.Core;

public record SortProgress(int Processed, int Total, string CurrentFile);
```

- [ ] **Step 7: Create `SortSummary.cs`**

Create `OpenPhotoSort.Core/Models/SortSummary.cs`:
```csharp
namespace OpenPhotoSort.Core;

public record SortSummary(int Moved, int Copied, int Skipped, int Renamed, int Failed);
```

- [ ] **Step 8: Verify Core builds with models**

```powershell
dotnet build OpenPhotoSort.Core/OpenPhotoSort.Core.csproj
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 9: Commit**

```bash
git add OpenPhotoSort.Tests/ OpenPhotoSort.Core/Models/ OpenPhotoSort.sln
git commit -m "feat: add xUnit test project and Core model types"
```

---

### Task 2: PhotoScanner

**Files:**
- Create: `OpenPhotoSort.Core/PhotoScanner.cs`
- Create: `OpenPhotoSort.Tests/PhotoScannerTests.cs`

**Interfaces:**
- Consumes: `ScanResult` (from Task 1), `ImageHelper.ReadExifData(string) → Dictionary<string, Tuple<string, string>>?` (existing in `OpenPhotoSort.Core`)
- Produces:
  ```csharp
  // In OpenPhotoSort.Core namespace:
  public static class PhotoScanner
  {
      public static Task<ScanResult> ScanAsync(
          string sourceFolder,
          bool includeSubfolders,
          CancellationToken cancellationToken = default)
  }
  ```

- [ ] **Step 1: Write the failing tests**

Create `OpenPhotoSort.Tests/PhotoScannerTests.cs`:
```csharp
using ImageMagick;
using OpenPhotoSort.Core;

namespace OpenPhotoSort.Tests;

public class PhotoScannerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public PhotoScannerTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private void WriteJpeg(string fileName, string? exifDate = null, string? cameraModel = null)
    {
        var image = new MagickImage(MagickColors.Red, 1, 1);
        if (exifDate != null || cameraModel != null)
        {
            var profile = new ExifProfile();
            if (exifDate != null)
                profile.SetValue(ExifTag.DateTimeOriginal, exifDate);
            if (cameraModel != null)
                profile.SetValue(ExifTag.Model, cameraModel);
            image.SetProfile(profile);
        }
        image.Write(Path.Combine(_tempDir, fileName), MagickFormat.Jpeg);
    }

    [Fact]
    public async Task ScanAsync_FileWithExifDate_InWithValidExifDate()
    {
        WriteJpeg("dated.jpg", exifDate: "2024:06:25 10:30:00");

        var result = await PhotoScanner.ScanAsync(_tempDir, includeSubfolders: false);

        Assert.Single(result.WithValidExifDate);
        Assert.Empty(result.WithExifButNoDate);
        Assert.Empty(result.NoExif);
        Assert.EndsWith("dated.jpg", result.WithValidExifDate[0]);
    }

    [Fact]
    public async Task ScanAsync_FileWithExifButNoDate_InWithExifButNoDate()
    {
        WriteJpeg("nodateexif.jpg", cameraModel: "TestCam"); // has EXIF, but no date tag

        var result = await PhotoScanner.ScanAsync(_tempDir, includeSubfolders: false);

        Assert.Empty(result.WithValidExifDate);
        Assert.Single(result.WithExifButNoDate);
        Assert.Empty(result.NoExif);
    }

    [Fact]
    public async Task ScanAsync_FileWithNoExif_InNoExif()
    {
        WriteJpeg("noexif.jpg"); // no EXIF profile at all

        var result = await PhotoScanner.ScanAsync(_tempDir, includeSubfolders: false);

        Assert.Empty(result.WithValidExifDate);
        Assert.Empty(result.WithExifButNoDate);
        Assert.Single(result.NoExif);
    }

    [Fact]
    public async Task ScanAsync_NonImageFile_NotCounted()
    {
        File.WriteAllText(Path.Combine(_tempDir, "readme.txt"), "hello");

        var result = await PhotoScanner.ScanAsync(_tempDir, includeSubfolders: false);

        Assert.Equal(0, result.TotalFiles);
    }

    [Fact]
    public async Task ScanAsync_SubfoldersIncluded_ScansRecursively()
    {
        string sub = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(sub);
        WriteJpeg("root.jpg", exifDate: "2024:06:25 10:00:00");
        var subImage = new MagickImage(MagickColors.Blue, 1, 1);
        var subProfile = new ExifProfile();
        subProfile.SetValue(ExifTag.DateTimeOriginal, "2023:01:01 00:00:00");
        subImage.SetProfile(subProfile);
        subImage.Write(Path.Combine(sub, "sub.jpg"), MagickFormat.Jpeg);

        var result = await PhotoScanner.ScanAsync(_tempDir, includeSubfolders: true);

        Assert.Equal(2, result.WithValidExifDate.Count);
    }

    [Fact]
    public async Task ScanAsync_SubfoldersExcluded_OnlyTopLevel()
    {
        string sub = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(sub);
        WriteJpeg("root.jpg", exifDate: "2024:06:25 10:00:00");
        var subImage = new MagickImage(MagickColors.Blue, 1, 1);
        subImage.Write(Path.Combine(sub, "sub.jpg"), MagickFormat.Jpeg);

        var result = await PhotoScanner.ScanAsync(_tempDir, includeSubfolders: false);

        Assert.Equal(1, result.TotalFiles);
    }

    [Fact]
    public async Task ScanAsync_CorruptFile_TreatedAsNoExif()
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "corrupt.jpg"), new byte[] { 0xFF, 0xD8, 0x00 });

        var result = await PhotoScanner.ScanAsync(_tempDir, includeSubfolders: false);

        Assert.Equal(1, result.NoExif.Count);
    }

    [Fact]
    public async Task ScanAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        WriteJpeg("a.jpg", exifDate: "2024:06:25 10:00:00");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            PhotoScanner.ScanAsync(_tempDir, includeSubfolders: false, cts.Token));
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```powershell
dotnet test OpenPhotoSort.Tests/OpenPhotoSort.Tests.csproj --filter "FullyQualifiedName~PhotoScannerTests"
```

Expected: All 7 tests FAIL with `The type or namespace name 'PhotoScanner' could not be found`.

- [ ] **Step 3: Implement `PhotoScanner`**

Create `OpenPhotoSort.Core/PhotoScanner.cs`:
```csharp
using System.Globalization;

namespace OpenPhotoSort.Core;

public static class PhotoScanner
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".heic"
    };

    public static Task<ScanResult> ScanAsync(
        string sourceFolder,
        bool includeSubfolders,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Scan(sourceFolder, includeSubfolders, cancellationToken), cancellationToken);
    }

    private static ScanResult Scan(string sourceFolder, bool includeSubfolders, CancellationToken ct)
    {
        var withDate = new List<string>();
        var withExifNoDate = new List<string>();
        var noExif = new List<string>();

        var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(sourceFolder, "*", searchOption)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)));

        foreach (string filePath in files)
        {
            ct.ThrowIfCancellationRequested();

            Dictionary<string, Tuple<string, string>>? exif = null;
            try { exif = ImageHelper.ReadExifData(filePath); }
            catch { }

            if (exif == null)
            {
                noExif.Add(filePath);
            }
            else if (TryGetExifDate(exif, out _))
            {
                withDate.Add(filePath);
            }
            else
            {
                withExifNoDate.Add(filePath);
            }
        }

        return new ScanResult(withDate, withExifNoDate, noExif);
    }

    internal static bool TryGetExifDate(Dictionary<string, Tuple<string, string>> exif, out DateTime date)
    {
        date = default;
        if (exif.TryGetValue("DateTimeOriginal", out var val) ||
            exif.TryGetValue("DateTime", out val))
        {
            return DateTime.TryParseExact(
                val.Item2, "yyyy:MM:dd HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }
        return false;
    }
}
```

- [ ] **Step 4: Run tests — all should pass**

```powershell
dotnet test OpenPhotoSort.Tests/OpenPhotoSort.Tests.csproj --filter "FullyQualifiedName~PhotoScannerTests"
```

Expected: `Passed! - Failed: 0, Passed: 7, Skipped: 0`

- [ ] **Step 5: Commit**

```bash
git add OpenPhotoSort.Core/PhotoScanner.cs OpenPhotoSort.Tests/PhotoScannerTests.cs
git commit -m "feat: add PhotoScanner with EXIF categorization"
```

---

### Task 3: PhotoSorter — helpers

**Files:**
- Create: `OpenPhotoSort.Core/PhotoSorter.cs` (partial — helpers only, `SortAsync` added in Task 4)
- Create: `OpenPhotoSort.Tests/PhotoSorterTests.cs`

**Interfaces:**
- Consumes: `ScanResult`, `SortOptions`, `FolderPattern`, `ConflictBehavior`, `SortOperation`, `SortProgress`, `SortSummary` (all from Task 1); `PhotoScanner.TryGetExifDate` (Task 2); `ImageHelper.ReadExifData` (existing)
- Produces (private helpers consumed by Task 4's `SortAsync`):
  ```csharp
  // all private static in PhotoSorter:
  internal static string BuildSubfolderPath(FolderPattern pattern, DateTime date, string cameraModel)
  private static string SanitizeFolderName(string name)
  private static string GetCameraModel(Dictionary<string, Tuple<string, string>> exif)
  private static string FindRenameDestination(string destPath)
  private record WorkEntry(string FilePath, DateTime Date, string CameraModel, bool IsDump)
  private static List<WorkEntry> BuildWorkList(ScanResult scanResult, SortOptions options, CancellationToken ct)
  ```

- [ ] **Step 1: Write the failing tests**

Create `OpenPhotoSort.Tests/PhotoSorterTests.cs`:
```csharp
using ImageMagick;
using OpenPhotoSort.Core;

namespace OpenPhotoSort.Tests;

public class PhotoSorterTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public PhotoSorterTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private static readonly DateTime TestDate = new(2024, 6, 25, 10, 30, 0);

    // --- BuildSubfolderPath tests ---

    [Theory]
    [InlineData(FolderPattern.YearMonthDay, "2024", "2024_06", "2024_06_25")]
    [InlineData(FolderPattern.YearMonth, "2024", "2024_06")]
    [InlineData(FolderPattern.YearOnly, "2024")]
    [InlineData(FolderPattern.MonthOnly, "2024_06")]
    [InlineData(FolderPattern.DayOnly, "2024_06_25")]
    public void BuildSubfolderPath_DateOnlyPatterns_ReturnsExpectedSegments(
        FolderPattern pattern, params string[] expectedSegments)
    {
        string result = PhotoSorter.BuildSubfolderPath(pattern, TestDate, "TestCam");
        string expected = Path.Combine(expectedSegments);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(FolderPattern.YearMonthDayCameraModel, "2024", "2024_06", "2024_06_25", "TestCam")]
    [InlineData(FolderPattern.YearMonthCameraModel, "2024", "2024_06", "TestCam")]
    [InlineData(FolderPattern.CameraModelYearMonthDay, "TestCam", "2024", "2024_06", "2024_06_25")]
    [InlineData(FolderPattern.CameraModelYearMonth, "TestCam", "2024", "2024_06")]
    [InlineData(FolderPattern.CameraModelYear, "TestCam", "2024")]
    public void BuildSubfolderPath_CameraModelPatterns_IncludesCameraSegment(
        FolderPattern pattern, params string[] expectedSegments)
    {
        string result = PhotoSorter.BuildSubfolderPath(pattern, TestDate, "TestCam");
        string expected = Path.Combine(expectedSegments);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildSubfolderPath_CameraModelContainsInvalidChars_Sanitized()
    {
        string result = PhotoSorter.BuildSubfolderPath(
            FolderPattern.CameraModelYear, TestDate, "Canon/EOS:R5");
        Assert.StartsWith("Canon_EOS_R5", result.Split(Path.DirectorySeparatorChar)[0]);
    }

    // --- FindRenameDestination tests (via SortAsync integration in Task 4) ---
    // Tested here by creating a temp file and checking the rename suffix format.

    [Fact]
    public void FindRenameDestination_NoConflict_ReturnsSamePath()
    {
        string path = Path.Combine(_tempDir, "photo.jpg");
        // file does not exist
        string result = PhotoSorter.FindRenameDestination(path);
        Assert.Equal(path, result);
    }

    [Fact]
    public void FindRenameDestination_OneConflict_AddsCopy001Suffix()
    {
        string path = Path.Combine(_tempDir, "photo.jpg");
        File.WriteAllText(path, "");

        string result = PhotoSorter.FindRenameDestination(path);
        Assert.Equal(Path.Combine(_tempDir, "photo-Copy001.jpg"), result);
    }

    [Fact]
    public void FindRenameDestination_TwoConflicts_AddsCopy002Suffix()
    {
        string path = Path.Combine(_tempDir, "photo.jpg");
        File.WriteAllText(path, "");
        File.WriteAllText(Path.Combine(_tempDir, "photo-Copy001.jpg"), "");

        string result = PhotoSorter.FindRenameDestination(path);
        Assert.Equal(Path.Combine(_tempDir, "photo-Copy002.jpg"), result);
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```powershell
dotnet test OpenPhotoSort.Tests/OpenPhotoSort.Tests.csproj --filter "FullyQualifiedName~PhotoSorterTests"
```

Expected: All tests FAIL with `PhotoSorter` not found.

- [ ] **Step 3: Implement `PhotoSorter` helpers**

Create `OpenPhotoSort.Core/PhotoSorter.cs`:
```csharp
using System.Globalization;

namespace OpenPhotoSort.Core;

public static class PhotoSorter
{
    // Called from Task 4's SortAsync
    private record WorkEntry(string FilePath, DateTime Date, string CameraModel, bool IsDump);

    internal static string BuildSubfolderPath(FolderPattern pattern, DateTime date, string cameraModel)
    {
        string y = date.ToString("yyyy");
        string ym = date.ToString("yyyy_MM");
        string ymd = date.ToString("yyyy_MM_dd");
        string cam = SanitizeFolderName(cameraModel);

        return pattern switch
        {
            FolderPattern.YearMonthDay            => Path.Combine(y, ym, ymd),
            FolderPattern.YearMonthDayCameraModel => Path.Combine(y, ym, ymd, cam),
            FolderPattern.YearMonth               => Path.Combine(y, ym),
            FolderPattern.YearMonthCameraModel    => Path.Combine(y, ym, cam),
            FolderPattern.CameraModelYearMonthDay => Path.Combine(cam, y, ym, ymd),
            FolderPattern.CameraModelYearMonth    => Path.Combine(cam, y, ym),
            FolderPattern.YearOnly                => y,
            FolderPattern.MonthOnly               => ym,
            FolderPattern.DayOnly                 => ymd,
            FolderPattern.CameraModelYear         => Path.Combine(cam, y),
            _ => throw new ArgumentOutOfRangeException(nameof(pattern))
        };
    }

    internal static string FindRenameDestination(string destPath)
    {
        if (!File.Exists(destPath)) return destPath;

        string dir = Path.GetDirectoryName(destPath)!;
        string name = Path.GetFileNameWithoutExtension(destPath);
        string ext = Path.GetExtension(destPath);
        int i = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{name}-Copy{i:D3}{ext}");
            i++;
        }
        while (File.Exists(candidate));
        return candidate;
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }

    private static string GetCameraModel(Dictionary<string, Tuple<string, string>> exif)
    {
        if (exif.TryGetValue("Model", out var val) && !string.IsNullOrWhiteSpace(val.Item2))
            return val.Item2.Trim();
        return "UnknownCamera";
    }

    private static List<WorkEntry> BuildWorkList(
        ScanResult scanResult, SortOptions options, CancellationToken ct)
    {
        var result = new List<WorkEntry>();
        var undatedWithExif = new HashSet<string>(
            scanResult.WithExifButNoDate, StringComparer.OrdinalIgnoreCase);

        foreach (string filePath in scanResult.WithValidExifDate)
        {
            ct.ThrowIfCancellationRequested();
            Dictionary<string, Tuple<string, string>>? exif = null;
            try { exif = ImageHelper.ReadExifData(filePath); } catch { }
            var date = (exif != null && PhotoScanner.TryGetExifDate(exif, out var d))
                ? d : File.GetLastWriteTime(filePath);
            string camera = exif != null ? GetCameraModel(exif) : "UnknownCamera";
            result.Add(new WorkEntry(filePath, date, camera, false));
        }

        foreach (string filePath in scanResult.WithExifButNoDate.Concat(scanResult.NoExif))
        {
            ct.ThrowIfCancellationRequested();
            if (options.UseFileDateForNoExif)
            {
                var date = File.GetLastWriteTime(filePath);
                string camera = "UnknownCamera";
                if (undatedWithExif.Contains(filePath))
                {
                    try
                    {
                        var exif = ImageHelper.ReadExifData(filePath);
                        if (exif != null) camera = GetCameraModel(exif);
                    }
                    catch { }
                }
                result.Add(new WorkEntry(filePath, date, camera, false));
            }
            else if (options.DumpNoExifToFolder)
            {
                result.Add(new WorkEntry(filePath, default, "UnknownCamera", true));
            }
            // else: omit → will be skipped in summary
        }

        return result;
    }

    // SortAsync added in Task 4
}
```

- [ ] **Step 4: Run tests — all should pass**

```powershell
dotnet test OpenPhotoSort.Tests/OpenPhotoSort.Tests.csproj --filter "FullyQualifiedName~PhotoSorterTests"
```

Expected: `Passed! - Failed: 0, Passed: 8, Skipped: 0`

- [ ] **Step 5: Commit**

```bash
git add OpenPhotoSort.Core/PhotoSorter.cs OpenPhotoSort.Tests/PhotoSorterTests.cs
git commit -m "feat: add PhotoSorter path building and conflict resolution helpers"
```

---

### Task 4: PhotoSorter.SortAsync

**Files:**
- Modify: `OpenPhotoSort.Core/PhotoSorter.cs` (add `SortAsync`)
- Modify: `OpenPhotoSort.Tests/PhotoSorterTests.cs` (add integration tests)

**Interfaces:**
- Consumes: all helpers from Task 3; `SortOptions`, `ScanResult`, `SortProgress`, `SortSummary` (Task 1)
- Produces:
  ```csharp
  public static Task<SortSummary> SortAsync(
      SortOptions options,
      ScanResult scanResult,
      IProgress<SortProgress> progress,
      CancellationToken cancellationToken = default)
  ```

- [ ] **Step 1: Add integration tests to `PhotoSorterTests.cs`**

Append to `OpenPhotoSort.Tests/PhotoSorterTests.cs` (inside the class, after existing tests):
```csharp
    // --- SortAsync integration tests ---

    private string MakeTempDir(string name)
    {
        string path = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private void WriteJpegWithDate(string dir, string fileName, string exifDate)
    {
        var image = new MagickImage(MagickColors.Red, 1, 1);
        var profile = new ExifProfile();
        profile.SetValue(ExifTag.DateTimeOriginal, exifDate);
        image.SetProfile(profile);
        image.Write(Path.Combine(dir, fileName), MagickFormat.Jpeg);
    }

    private SortOptions DefaultOptions(string src, string dest, SortOperation op = SortOperation.Copy) =>
        new(src, dest, FolderPattern.YearMonthDay, ConflictBehavior.DoNotCopyOrMove,
            "", false, false, "", false, op);

    [Fact]
    public async Task SortAsync_CopyDatedFile_CreatesInDateFolder()
    {
        string src = MakeTempDir("src");
        string dest = MakeTempDir("dest");
        WriteJpegWithDate(src, "photo.jpg", "2024:06:25 10:30:00");
        var scanResult = await PhotoScanner.ScanAsync(src, false);
        var options = DefaultOptions(src, dest);

        var summary = await PhotoSorter.SortAsync(options, scanResult, null!);

        string expected = Path.Combine(dest, "2024", "2024_06", "2024_06_25", "photo.jpg");
        Assert.True(File.Exists(expected));
        Assert.True(File.Exists(Path.Combine(src, "photo.jpg")), "Copy: source should remain");
        Assert.Equal(1, summary.Copied);
        Assert.Equal(0, summary.Failed);
    }

    [Fact]
    public async Task SortAsync_MoveFile_DeletesSource()
    {
        string src = MakeTempDir("src");
        string dest = MakeTempDir("dest");
        WriteJpegWithDate(src, "photo.jpg", "2024:06:25 10:30:00");
        var scanResult = await PhotoScanner.ScanAsync(src, false);
        var options = DefaultOptions(src, dest, SortOperation.Move);

        var summary = await PhotoSorter.SortAsync(options, scanResult, null!);

        string expected = Path.Combine(dest, "2024", "2024_06", "2024_06_25", "photo.jpg");
        Assert.True(File.Exists(expected));
        Assert.False(File.Exists(Path.Combine(src, "photo.jpg")), "Move: source should be deleted");
        Assert.Equal(1, summary.Moved);
    }

    [Fact]
    public async Task SortAsync_ConflictDoNotCopy_FileSkipped()
    {
        string src = MakeTempDir("src");
        string dest = MakeTempDir("dest");
        WriteJpegWithDate(src, "photo.jpg", "2024:06:25 10:30:00");
        var scanResult = await PhotoScanner.ScanAsync(src, false);
        string destPath = Path.Combine(dest, "2024", "2024_06", "2024_06_25", "photo.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        File.WriteAllText(destPath, "existing");
        var options = DefaultOptions(src, dest);

        var summary = await PhotoSorter.SortAsync(options, scanResult, null!);

        Assert.Equal(1, summary.Skipped);
        Assert.Equal("existing", File.ReadAllText(destPath));
    }

    [Fact]
    public async Task SortAsync_ConflictRename_AddsCopySuffix()
    {
        string src = MakeTempDir("src");
        string dest = MakeTempDir("dest");
        WriteJpegWithDate(src, "photo.jpg", "2024:06:25 10:30:00");
        var scanResult = await PhotoScanner.ScanAsync(src, false);
        string destPath = Path.Combine(dest, "2024", "2024_06", "2024_06_25", "photo.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        File.WriteAllText(destPath, "existing");
        var options = new SortOptions(src, dest, FolderPattern.YearMonthDay,
            ConflictBehavior.RenameCopy, "", false, false, "", false, SortOperation.Copy);

        var summary = await PhotoSorter.SortAsync(options, scanResult, null!);

        string renamed = Path.Combine(dest, "2024", "2024_06", "2024_06_25", "photo-Copy001.jpg");
        Assert.True(File.Exists(renamed));
        Assert.Equal(1, summary.Renamed);
        Assert.Equal(1, summary.Copied);
    }

    [Fact]
    public async Task SortAsync_ConflictOverwrite_ReplacesExisting()
    {
        string src = MakeTempDir("src");
        string dest = MakeTempDir("dest");
        WriteJpegWithDate(src, "photo.jpg", "2024:06:25 10:30:00");
        var scanResult = await PhotoScanner.ScanAsync(src, false);
        string destPath = Path.Combine(dest, "2024", "2024_06", "2024_06_25", "photo.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        File.WriteAllText(destPath, "old");
        var options = new SortOptions(src, dest, FolderPattern.YearMonthDay,
            ConflictBehavior.Overwrite, "", false, false, "", false, SortOperation.Copy);

        var summary = await PhotoSorter.SortAsync(options, scanResult, null!);

        Assert.True(new FileInfo(destPath).Length > 3, "File should be replaced by actual JPEG");
        Assert.Equal(1, summary.Copied);
    }

    [Fact]
    public async Task SortAsync_ConflictDuplicatesFolder_RoutesToDupFolder()
    {
        string src = MakeTempDir("src");
        string dest = MakeTempDir("dest");
        string dups = MakeTempDir("dups");
        WriteJpegWithDate(src, "photo.jpg", "2024:06:25 10:30:00");
        var scanResult = await PhotoScanner.ScanAsync(src, false);
        string destPath = Path.Combine(dest, "2024", "2024_06", "2024_06_25", "photo.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        File.WriteAllText(destPath, "existing");
        var options = new SortOptions(src, dest, FolderPattern.YearMonthDay,
            ConflictBehavior.DuplicatesFolder, dups, false, false, "", false, SortOperation.Copy);

        await PhotoSorter.SortAsync(options, scanResult, null!);

        Assert.True(File.Exists(Path.Combine(dups, "photo.jpg")));
    }

    [Fact]
    public async Task SortAsync_NoExifUseFileDate_SortsIntoDateFolder()
    {
        string src = MakeTempDir("src");
        string dest = MakeTempDir("dest");
        // Write a JPEG with no EXIF
        var image = new MagickImage(MagickColors.Blue, 1, 1);
        string filePath = Path.Combine(src, "noexif.jpg");
        image.Write(filePath, MagickFormat.Jpeg);
        File.SetLastWriteTime(filePath, new DateTime(2023, 3, 15));

        var scanResult = await PhotoScanner.ScanAsync(src, false);
        var options = new SortOptions(src, dest, FolderPattern.YearMonthDay,
            ConflictBehavior.DoNotCopyOrMove, "", false, false, "", false, SortOperation.Copy)
            with { UseFileDateForNoExif = true };

        var summary = await PhotoSorter.SortAsync(options, scanResult, null!);

        string expected = Path.Combine(dest, "2023", "2023_03", "2023_03_15", "noexif.jpg");
        Assert.True(File.Exists(expected));
        Assert.Equal(1, summary.Copied);
    }

    [Fact]
    public async Task SortAsync_NoExifDumpFolder_RoutesToDumpFolder()
    {
        string src = MakeTempDir("src");
        string dest = MakeTempDir("dest");
        string dump = MakeTempDir("dump");
        var image = new MagickImage(MagickColors.Green, 1, 1);
        image.Write(Path.Combine(src, "noexif.jpg"), MagickFormat.Jpeg);

        var scanResult = await PhotoScanner.ScanAsync(src, false);
        var options = new SortOptions(src, dest, FolderPattern.YearMonthDay,
            ConflictBehavior.DoNotCopyOrMove, "", false, true, dump, false, SortOperation.Copy);

        var summary = await PhotoSorter.SortAsync(options, scanResult, null!);

        Assert.True(File.Exists(Path.Combine(dump, "noexif.jpg")));
        Assert.Equal(1, summary.Copied);
    }

    [Fact]
    public async Task SortAsync_Cancelled_ThrowsOperationCanceled()
    {
        string src = MakeTempDir("src");
        string dest = MakeTempDir("dest");
        WriteJpegWithDate(src, "a.jpg", "2024:01:01 00:00:00");
        var scanResult = await PhotoScanner.ScanAsync(src, false);
        var options = DefaultOptions(src, dest);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            PhotoSorter.SortAsync(options, scanResult, null!, cts.Token));
    }

    [Fact]
    public async Task SortAsync_ReportsProgress()
    {
        string src = MakeTempDir("src");
        string dest = MakeTempDir("dest");
        WriteJpegWithDate(src, "a.jpg", "2024:01:01 00:00:00");
        WriteJpegWithDate(src, "b.jpg", "2024:01:02 00:00:00");
        var scanResult = await PhotoScanner.ScanAsync(src, false);
        var options = DefaultOptions(src, dest);
        var reports = new List<SortProgress>();
        var progress = new Progress<SortProgress>(p => reports.Add(p));

        await PhotoSorter.SortAsync(options, scanResult, progress);
        await Task.Delay(50); // let Progress callbacks fire

        Assert.Equal(2, reports.Count);
        Assert.Equal(1, reports[0].Processed);
        Assert.Equal(2, reports[1].Processed);
        Assert.Equal(2, reports[1].Total);
    }
```

- [ ] **Step 2: Run tests to confirm new tests fail**

```powershell
dotnet test OpenPhotoSort.Tests/OpenPhotoSort.Tests.csproj --filter "FullyQualifiedName~PhotoSorterTests"
```

Expected: 8 existing tests PASS, new integration tests FAIL with `PhotoSorter does not contain a definition for 'SortAsync'`.

- [ ] **Step 3: Add `SortAsync` to `PhotoSorter.cs`**

Append the following method to `OpenPhotoSort.Core/PhotoSorter.cs` (inside the `PhotoSorter` class, before the closing brace):
```csharp
    public static async Task<SortSummary> SortAsync(
        SortOptions options,
        ScanResult scanResult,
        IProgress<SortProgress> progress,
        CancellationToken cancellationToken = default)
    {
        var workList = await Task.Run(
            () => BuildWorkList(scanResult, options, cancellationToken), cancellationToken);

        int total = workList.Count;
        int processed = 0, moved = 0, copied = 0, skipped = 0, renamed = 0, failed = 0;

        foreach (var entry in workList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fileName = Path.GetFileName(entry.FilePath);
            bool skipFile = false;
            bool wasRenamed = false;

            try
            {
                string destPath;
                if (entry.IsDump)
                {
                    Directory.CreateDirectory(options.NoExifFolderPath);
                    destPath = Path.Combine(options.NoExifFolderPath, fileName);
                }
                else
                {
                    string sub = BuildSubfolderPath(options.FolderPattern, entry.Date, entry.CameraModel);
                    string destDir = Path.Combine(options.DestinationFolder, sub);
                    Directory.CreateDirectory(destDir);
                    destPath = Path.Combine(destDir, fileName);
                }

                if (File.Exists(destPath))
                {
                    switch (options.ConflictBehavior)
                    {
                        case ConflictBehavior.DoNotCopyOrMove:
                            skipFile = true;
                            skipped++;
                            break;
                        case ConflictBehavior.RenameCopy:
                            destPath = FindRenameDestination(destPath);
                            wasRenamed = true;
                            break;
                        case ConflictBehavior.Overwrite:
                            break;
                        case ConflictBehavior.DuplicatesFolder:
                            Directory.CreateDirectory(options.DuplicatesFolderPath);
                            string dupBase = Path.Combine(options.DuplicatesFolderPath, fileName);
                            string dupDest = FindRenameDestination(dupBase);
                            wasRenamed = dupDest != dupBase;
                            destPath = dupDest;
                            break;
                    }
                }

                if (!skipFile)
                {
                    bool overwrite = options.ConflictBehavior == ConflictBehavior.Overwrite;
                    File.Copy(entry.FilePath, destPath, overwrite);
                    if (options.Operation == SortOperation.Move)
                    {
                        File.Delete(entry.FilePath);
                        moved++;
                    }
                    else
                    {
                        copied++;
                    }
                    if (wasRenamed) renamed++;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { if (!skipFile) failed++; }
            finally
            {
                processed++;
                progress?.Report(new SortProgress(processed, total, fileName));
            }
        }

        return new SortSummary(moved, copied, skipped, renamed, failed);
    }
```

- [ ] **Step 4: Run all tests — all should pass**

```powershell
dotnet test OpenPhotoSort.Tests/OpenPhotoSort.Tests.csproj
```

Expected: `Passed! - Failed: 0, Passed: 18, Skipped: 0` (7 scanner + 8 helper + 10 sort + 1 progress… exact count may vary by one based on final test count).

- [ ] **Step 5: Commit**

```bash
git add OpenPhotoSort.Core/PhotoSorter.cs OpenPhotoSort.Tests/PhotoSorterTests.cs
git commit -m "feat: add PhotoSorter.SortAsync with full copy/move/cancel support"
```

---

### Task 5: MainViewModel

**Files:**
- Create: `OpenPhotoSort.UI/ViewModels/MainViewModel.cs`

**Interfaces:**
- Consumes: `PhotoScanner.ScanAsync`, `PhotoSorter.SortAsync`, `ScanResult`, `SortOptions`, `SortProgress`, `SortSummary`, all enums (Tasks 1–4); `FolderPickerX.PickFolderAsync` (existing in `OpenPhotoSort.Helpers`)
- Produces: `MainViewModel` class — consumed by Task 6's XAML bindings

  Key bindings consumed by the XAML (Task 6) listed here for cross-reference:
  ```
  // string properties (ObservableProperty)
  SourceFolder, DestinationFolder, DuplicatesFolderPath, NoExifFolderPath, StatusText

  // bool properties
  IncludeSubfolders, UseFileDateForNoExif, DumpNoExifToFolder
  IsScanning, IsSorting, IsBusy, IsScanComplete, ShowSummaryButton
  DuplicatesFolderEnabled, NoExifFolderEnabled

  // int properties (picker selected index)
  SelectedFolderPatternIndex, SelectedConflictBehaviorIndex

  // int stats
  TotalFiles, FilesWithValidDate, FilesWithExifButNoDate, FilesNoExif

  // double (0.0–1.0 for ProgressBar)
  OperationProgress

  // commands
  BrowseSourceCommand, BrowseDestinationCommand
  BrowseDuplicatesFolderCommand, BrowseNoExifFolderCommand
  FindPhotosCommand, CopyCommand, MoveCommand, CancelCommand, ShowSummaryCommand

  // static lists for Picker.ItemsSource
  public static IReadOnlyList<string> FolderPatternOptions
  public static IReadOnlyList<string> ConflictBehaviorOptions
  ```

**No automated tests for the ViewModel** — MAUI dependency injection and `Preferences` API require the MAUI runtime. Verify manually (Step 5).

- [ ] **Step 1: Create the `ViewModels` directory and `MainViewModel.cs`**

Create `OpenPhotoSort.UI/ViewModels/MainViewModel.cs`:
```csharp
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

    // --- Observable properties ---

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DuplicatesFolderEnabled))]
    [NotifyCanExecuteChangedFor(nameof(FindPhotosCommand))]
    private string _sourceFolder = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindPhotosCommand))]
    private string _destinationFolder = string.Empty;

    [ObservableProperty] private bool _includeSubfolders;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DuplicatesFolderEnabled))]
    private int _selectedFolderPatternIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DuplicatesFolderEnabled))]
    private int _selectedConflictBehaviorIndex;

    [ObservableProperty] private string _duplicatesFolderPath = string.Empty;
    [ObservableProperty] private bool _useFileDateForNoExif;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoExifFolderEnabled))]
    private bool _dumpNoExifToFolder;

    [ObservableProperty] private string _noExifFolderPath = string.Empty;

    [ObservableProperty] private int _totalFiles;
    [ObservableProperty] private int _filesWithValidDate;
    [ObservableProperty] private int _filesWithExifButNoDate;
    [ObservableProperty] private int _filesNoExif;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindPhotosCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveCommand))]
    [NotifyPropertyChangedFor(nameof(CanOperate))]
    private bool _isScanComplete;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindPhotosCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveCommand))]
    [NotifyPropertyChangedFor(nameof(CanOperate))]
    private bool _isScanning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindPhotosCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveCommand))]
    [NotifyPropertyChangedFor(nameof(CanOperate))]
    private bool _isSorting;

    [ObservableProperty] private double _operationProgress;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _showSummaryButton;

    // --- Computed properties ---

    public bool IsBusy => IsScanning || IsSorting;
    public bool CanOperate => IsScanComplete && !IsBusy;
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
        _sourceFolder           = Preferences.Get("ops_sourceFolder", string.Empty);
        _destinationFolder      = Preferences.Get("ops_destFolder", string.Empty);
        _includeSubfolders      = Preferences.Get("ops_includeSubfolders", false);
        _selectedFolderPatternIndex   = Preferences.Get("ops_folderPattern", 0);
        _selectedConflictBehaviorIndex = Preferences.Get("ops_conflictBehavior", 0);
        _duplicatesFolderPath   = Preferences.Get("ops_duplicatesFolderPath", string.Empty);
        _useFileDateForNoExif   = Preferences.Get("ops_useFileDateForNoExif", false);
        _dumpNoExifToFolder     = Preferences.Get("ops_dumpNoExifToFolder", false);
        _noExifFolderPath       = Preferences.Get("ops_noExifFolderPath", string.Empty);
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
        await Application.Current!.MainPage!.DisplayAlert("Summary", msg, "OK");
    }

    private async Task RunSortAsync(SortOperation operation)
    {
        if (_lastScan is null) return;

        _cts = new CancellationTokenSource();
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

        int lastTotal = 0;
        var progress = new Progress<SortProgress>(p =>
        {
            lastTotal = p.Total;
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
            _lastSummary = new SortSummary(0, 0, 0, 0, 0); // partial; real counts not available after cancel
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsSorting = false;
            ShowSummaryButton = true;
            _cts.Dispose();
            _cts = null;
        }

        if (cancelled)
            StatusText = $"Cancelled.";
        else if (_lastSummary is not null)
            StatusText = $"Done. {_lastSummary.Copied + _lastSummary.Moved} file(s) processed.";
    }
}
```

- [ ] **Step 2: Build the UI project**

```powershell
dotnet build OpenPhotoSort.UI/OpenPhotoSort.UI.csproj -f net10.0-windows10.0.19041.0
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

If there are source-generator warnings about nullable or missing partial methods, they are expected only on first build and will resolve once `MainPage.xaml.cs` is updated in Task 6.

- [ ] **Step 3: Commit**

```bash
git add OpenPhotoSort.UI/ViewModels/MainViewModel.cs
git commit -m "feat: add MainViewModel with scan/sort commands and settings persistence"
```

---

### Task 6: MainPage UI

**Files:**
- Replace: `OpenPhotoSort.UI/MainPage.xaml`
- Replace: `OpenPhotoSort.UI/MainPage.xaml.cs`

**Interfaces:**
- Consumes: `MainViewModel` (Task 5) — all bindings listed in Task 5's Produces block

- [ ] **Step 1: Replace `MainPage.xaml.cs`**

Replace the entire contents of `OpenPhotoSort.UI/MainPage.xaml.cs`:
```csharp
using OpenPhotoSort.ViewModels;

namespace OpenPhotoSort;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BindingContext = new MainViewModel();
    }
}
```

- [ ] **Step 2: Replace `MainPage.xaml`**

Replace the entire contents of `OpenPhotoSort.UI/MainPage.xaml`:
```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:OpenPhotoSort.ViewModels"
             x:Class="OpenPhotoSort.MainPage"
             Title="OpenPhotoSort">

    <ScrollView>
        <Grid ColumnDefinitions="*,1,*" Padding="16,12" ColumnSpacing="0">

            <!-- Vertical separator -->
            <BoxView Grid.Column="1"
                     BackgroundColor="{AppThemeBinding Light=#CCCCCC, Dark=#444444}"
                     Margin="12,0" VerticalOptions="Fill"/>

            <!-- ═══════════════════════════════════════════ LEFT COLUMN ═══ -->
            <VerticalStackLayout Grid.Column="0" Spacing="0">

                <!-- Step 1 -->
                <Label Text="Step 1: Choose Folder with Photos to Process:"
                       FontAttributes="Bold" TextColor="Gray" Margin="0,0,0,6"/>
                <Border StrokeThickness="1" Padding="6,4" Margin="0,0,0,6">
                    <Entry Text="{Binding SourceFolder}" IsReadOnly="True"
                           Placeholder="(no source folder selected)"/>
                </Border>
                <HorizontalStackLayout Spacing="16" Margin="0,0,0,8">
                    <Button Text="Click Here to Choose Folder to Search"
                            Command="{Binding BrowseSourceCommand}"/>
                    <HorizontalStackLayout Spacing="6" VerticalOptions="Center">
                        <CheckBox IsChecked="{Binding IncludeSubfolders}"/>
                        <Label Text="Include Sub Folders" VerticalOptions="Center"/>
                    </HorizontalStackLayout>
                </HorizontalStackLayout>

                <!-- Step 2 -->
                <Label Text="Step 2: Set Destination Folder Under Which Date Sorted Folders Will Be Made:"
                       FontAttributes="Bold" TextColor="Gray" Margin="0,8,0,6"/>
                <Border StrokeThickness="1" Padding="6,4" Margin="0,0,0,6">
                    <Entry Text="{Binding DestinationFolder}" IsReadOnly="True"
                           Placeholder="(no destination folder selected)"/>
                </Border>
                <Button Text="Click Here to Choose Output Folder"
                        Command="{Binding BrowseDestinationCommand}" Margin="0,0,0,8"
                        HorizontalOptions="Start"/>

                <!-- Step 3 -->
                <Label Text="Step 3: Click Find Photos to Find Files to Move or Copy:"
                       FontAttributes="Bold" TextColor="Gray" Margin="0,8,0,8"/>
                <Grid ColumnDefinitions="Auto,*" ColumnSpacing="8">
                    <Button Grid.Column="0" Text="Find Photos"
                            Command="{Binding FindPhotosCommand}"
                            VerticalOptions="Center" Padding="12,8"/>
                    <VerticalStackLayout Grid.Column="1" Spacing="4">
                        <!-- Stat row: Total -->
                        <Grid ColumnDefinitions="56,*">
                            <Border Grid.Column="0" StrokeThickness="1" Padding="4,2">
                                <Label Text="{Binding TotalFiles}"
                                       HorizontalTextAlignment="Center" FontSize="13"/>
                            </Border>
                            <Label Grid.Column="1" Text="Total Files Checked In All Folders"
                                   VerticalOptions="Center" Margin="8,0,0,0" FontSize="13"/>
                        </Grid>
                        <!-- Stat row: Valid date -->
                        <Grid ColumnDefinitions="56,*">
                            <Border Grid.Column="0" StrokeThickness="1" Padding="4,2">
                                <Label Text="{Binding FilesWithValidDate}"
                                       HorizontalTextAlignment="Center" FontSize="13"/>
                            </Border>
                            <Label Grid.Column="1"
                                   Text="Have Valid Date Created in File's Internal Exif Data"
                                   VerticalOptions="Center" Margin="8,0,0,0" FontSize="13"/>
                        </Grid>
                        <!-- Stat row: EXIF no date -->
                        <Grid ColumnDefinitions="56,*">
                            <Border Grid.Column="0" StrokeThickness="1" Padding="4,2">
                                <Label Text="{Binding FilesWithExifButNoDate}"
                                       HorizontalTextAlignment="Center" FontSize="13"/>
                            </Border>
                            <Label Grid.Column="1" Text="Have Exif Data But No Valid Creation Date"
                                   VerticalOptions="Center" Margin="8,0,0,0" FontSize="13"/>
                        </Grid>
                        <!-- Stat row: No EXIF -->
                        <Grid ColumnDefinitions="56,*">
                            <Border Grid.Column="0" StrokeThickness="1" Padding="4,2">
                                <Label Text="{Binding FilesNoExif}"
                                       HorizontalTextAlignment="Center" FontSize="13"/>
                            </Border>
                            <Label Grid.Column="1" Text="No Exif Data"
                                   VerticalOptions="Center" Margin="8,0,0,0" FontSize="13"/>
                        </Grid>
                    </VerticalStackLayout>
                </Grid>

                <!-- Progress (visible during sort) -->
                <ProgressBar Progress="{Binding OperationProgress}"
                             IsVisible="{Binding IsSorting}" Margin="0,12,0,4"/>
                <Label Text="{Binding StatusText}" IsVisible="{Binding IsBusy}"
                       FontSize="12" TextColor="Gray" Margin="0,0,0,4"/>

                <!-- Step 4 -->
                <Label Text="Step 4: Copy or Move Photos to Date Sorted Folders"
                       FontAttributes="Bold" TextColor="Gray" Margin="0,16,0,8"/>
                <HorizontalStackLayout Spacing="12">
                    <Button Text="COPY to Destination Folders"
                            Command="{Binding CopyCommand}"
                            IsEnabled="{Binding CanOperate}"/>
                    <Button Text="MOVE to Destination Folders"
                            Command="{Binding MoveCommand}"
                            IsEnabled="{Binding CanOperate}"/>
                    <Button Text="Cancel" Command="{Binding CancelCommand}"
                            IsVisible="{Binding IsSorting}"
                            BackgroundColor="#CC3333" TextColor="White"/>
                </HorizontalStackLayout>

                <!-- Bottom bar -->
                <BoxView HeightRequest="1"
                         BackgroundColor="{AppThemeBinding Light=#CCCCCC, Dark=#444444}"
                         Margin="0,16,0,12"/>
                <Button Text="Show Summary Report"
                        Command="{Binding ShowSummaryCommand}"
                        IsEnabled="{Binding ShowSummaryButton}"
                        HorizontalOptions="Start"/>
            </VerticalStackLayout>

            <!-- ══════════════════════════════════════════ RIGHT COLUMN ═══ -->
            <VerticalStackLayout Grid.Column="2" Spacing="8" Padding="16,0,0,0">

                <!-- Folder structure -->
                <Label Text="Output Folder Structure:" FontAttributes="Bold"/>
                <Picker ItemsSource="{x:Static vm:MainViewModel.FolderPatternOptions}"
                        SelectedIndex="{Binding SelectedFolderPatternIndex}"/>

                <!-- Conflict behavior -->
                <Label Text="If File to be Moved/Copied exists in the destination folder:"
                       FontAttributes="Bold" Margin="0,12,0,0"/>
                <Picker ItemsSource="{x:Static vm:MainViewModel.ConflictBehaviorOptions}"
                        SelectedIndex="{Binding SelectedConflictBehaviorIndex}"/>
                <HorizontalStackLayout Spacing="4">
                    <Entry Text="{Binding DuplicatesFolderPath}" IsReadOnly="True"
                           IsEnabled="{Binding DuplicatesFolderEnabled}"
                           Placeholder="Duplicates folder…"
                           HorizontalOptions="FillAndExpand"/>
                    <Button Text="&gt;" WidthRequest="44"
                            Command="{Binding BrowseDuplicatesFolderCommand}"
                            IsEnabled="{Binding DuplicatesFolderEnabled}"/>
                </HorizontalStackLayout>

                <!-- No-EXIF options -->
                <Label Text="Option for Files with No Exif Date Created:"
                       FontAttributes="Bold" Margin="0,12,0,0"/>
                <HorizontalStackLayout Spacing="8">
                    <CheckBox IsChecked="{Binding UseFileDateForNoExif}"/>
                    <Label Text="Use File Date to Move or Copy to Structured Folders"
                           VerticalOptions="Center" FontSize="13"/>
                </HorizontalStackLayout>
                <HorizontalStackLayout Spacing="8">
                    <CheckBox IsChecked="{Binding DumpNoExifToFolder}"/>
                    <Label Text="Copy or Move to This Folder:" VerticalOptions="Center" FontSize="13"/>
                </HorizontalStackLayout>
                <HorizontalStackLayout Spacing="4">
                    <Entry Text="{Binding NoExifFolderPath}" IsReadOnly="True"
                           IsEnabled="{Binding NoExifFolderEnabled}"
                           Placeholder="No-EXIF folder…"
                           HorizontalOptions="FillAndExpand"/>
                    <Button Text="&gt;" WidthRequest="44"
                            Command="{Binding BrowseNoExifFolderCommand}"
                            IsEnabled="{Binding NoExifFolderEnabled}"/>
                </HorizontalStackLayout>

            </VerticalStackLayout>

        </Grid>
    </ScrollView>
</ContentPage>
```

- [ ] **Step 3: Build the UI project**

```powershell
dotnet build OpenPhotoSort.UI/OpenPhotoSort.UI.csproj -f net10.0-windows10.0.19041.0
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 4: Run the app and do a manual smoke test**

```powershell
dotnet run --project OpenPhotoSort.UI/OpenPhotoSort.UI.csproj -f net10.0-windows10.0.19041.0
```

Verify:
1. Two-column layout loads; left side shows Steps 1–4; right side shows options
2. Source/Destination folder entries are empty on first run
3. "Find Photos" button is disabled (both folders empty)
4. Browse buttons open a folder picker dialog
5. After selecting both folders, "Find Photos" enables
6. Clicking "Find Photos" scans and populates the four stat rows
7. "COPY/MOVE to Destination Folders" buttons enable after scan
8. During a copy/move: progress bar appears, "Cancel" button appears, Copy/Move buttons disable
9. "Cancel" stops the operation; "Show Summary Report" appears
10. "Show Summary Report" opens an alert with counts
11. Close and re-open the app — folder paths and options should be restored from preferences

- [ ] **Step 5: Update README status section**

Open `README.md` and replace the `## Status` section:
```markdown
## Status

Photo scanning and sorting are implemented. Core features:
- Scans a folder (optionally recursive) for JPG/JPEG/PNG/BMP/GIF/HEIC files
- Reads EXIF to detect date and camera model
- Copies or moves files into configurable date-based folder structures (10 patterns)
- Handles file conflicts: skip / rename / overwrite / route to duplicates folder
- Routes no-EXIF files to a dump folder or uses file date as fallback
- Two-phase UX: scan first to see stats, then copy or move with live progress
- Cancel button stops an in-progress operation
- All settings persist across sessions
```

- [ ] **Step 6: Run all tests one final time**

```powershell
dotnet test OpenPhotoSort.Tests/OpenPhotoSort.Tests.csproj
```

Expected: All tests PASS with 0 failures.

- [ ] **Step 7: Commit**

```bash
git add OpenPhotoSort.UI/MainPage.xaml OpenPhotoSort.UI/MainPage.xaml.cs README.md
git commit -m "feat: implement PhotoMove-style two-column UI with full sort controls"
```

---

## Self-Review

**Spec coverage:**
- ✅ `ScanResult`, `PhotoScanner.ScanAsync` — Task 2
- ✅ `SortOptions` + all enums (`FolderPattern` 10 values, `ConflictBehavior` 4 values, `SortOperation`) — Task 1
- ✅ `SortProgress`, `SortSummary` — Task 1
- ✅ `PhotoSorter.SortAsync` + path building + conflict + no-EXIF routing — Tasks 3–4
- ✅ Cancel via `CancellationToken` + Cancel button — Tasks 4–5–6
- ✅ Camera model fallback `"UnknownCamera"` — Task 3 (`GetCameraModel`)
- ✅ Rename format `-Copy###` — Task 3 (`FindRenameDestination`)
- ✅ Folder name sanitization — Task 3 (`SanitizeFolderName`)
- ✅ `DuplicatesFolder` conflict routing — Tasks 3–4
- ✅ `UseFileDateForNoExif` takes priority over `DumpNoExifToFolder` — `BuildWorkList` (Task 3)
- ✅ All 9 `Preferences` keys — Task 5 constructor + `On{Prop}Changed` partial methods
- ✅ Two-column PhotoMove-style layout — Task 6
- ✅ Stats panel (four rows with count boxes) — Task 6
- ✅ Progress bar + status label — Task 6
- ✅ Settings persistence tested manually (smoke test step 11) — Task 6
- ✅ `SortAsync` with `IProgress<SortProgress>` — Tasks 4, tested in Task 4

**Placeholder scan:** None found.

**Type consistency:**
- `SortProgress` record used in `PhotoSorter.SortAsync` signature and `IProgress<SortProgress>` in ViewModel — consistent ✅
- `BuildSubfolderPath` signature: `(FolderPattern, DateTime, string) → string` — consistent between Task 3 definition and Task 4 usage ✅
- `FindRenameDestination(string) → string` — consistent ✅
- `TryGetExifDate` defined `internal` in `PhotoScanner`, used in `PhotoSorter.BuildWorkList` — consistent ✅
- All enum values match between `SortOptions.cs` and ViewModel cast `(FolderPattern)SelectedFolderPatternIndex` — enum order is the same as `FolderPatternOptions` list ✅
