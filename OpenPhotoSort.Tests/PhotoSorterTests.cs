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
}
