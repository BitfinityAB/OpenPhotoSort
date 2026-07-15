using ImageMagick;
using OpenPhotoSort.Core;
using OpenPhotoSort.Tests.TestHelpers;

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

        Assert.Single(result.NoExif);
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

    [Fact]
    public async Task ScanAsync_VideoFileWithEmbeddedDate_InWithValidExifDate()
    {
        VideoTestHelper.WriteMinimalMp4(
            Path.Combine(_tempDir, "clip.mp4"),
            new DateTime(2023, 5, 17, 9, 15, 0, DateTimeKind.Utc));

        var result = await PhotoScanner.ScanAsync(_tempDir, includeSubfolders: false);

        Assert.Single(result.WithValidExifDate);
        Assert.Empty(result.WithExifButNoDate);
        Assert.Empty(result.NoExif);
        Assert.EndsWith("clip.mp4", result.WithValidExifDate[0]);
    }

    [Fact]
    public async Task ScanAsync_CorruptVideoFile_TreatedAsNoExif()
    {
        VideoTestHelper.WriteCorruptVideo(Path.Combine(_tempDir, "corrupt.mp4"));

        var result = await PhotoScanner.ScanAsync(_tempDir, includeSubfolders: false);

        Assert.Single(result.NoExif);
    }

    [Fact]
    public async Task ScanAsync_MixedPhotosAndVideos_BucketsBothCorrectly()
    {
        WriteJpeg("dated.jpg", exifDate: "2024:06:25 10:30:00");
        VideoTestHelper.WriteMinimalMp4(
            Path.Combine(_tempDir, "clip.mp4"),
            new DateTime(2023, 5, 17, 9, 15, 0, DateTimeKind.Utc));

        var result = await PhotoScanner.ScanAsync(_tempDir, includeSubfolders: false);

        Assert.Equal(2, result.TotalFiles);
        Assert.Equal(2, result.WithValidExifDate.Count);
    }

    [Fact]
    public async Task ScanAsync_NoExifFileWithParseableFilename_RecordedInFilenameDates()
    {
        WriteJpeg("2024-06-25 10.15.00.jpg"); // no EXIF profile at all

        var result = await PhotoScanner.ScanAsync(_tempDir, includeSubfolders: false);

        Assert.Single(result.NoExif);
        Assert.True(result.FilenameDates.TryGetValue(result.NoExif[0], out var date));
        Assert.Equal(new DateTime(2024, 6, 25, 10, 15, 0), date);
    }

    [Fact]
    public async Task ScanAsync_ExifButNoDateFileWithParseableFilename_RecordedInFilenameDates()
    {
        WriteJpeg("IMG_20240625_101500.jpg", cameraModel: "TestCam"); // has EXIF, no date tag

        var result = await PhotoScanner.ScanAsync(_tempDir, includeSubfolders: false);

        Assert.Single(result.WithExifButNoDate);
        Assert.True(result.FilenameDates.TryGetValue(result.WithExifButNoDate[0], out var date));
        Assert.Equal(new DateTime(2024, 6, 25, 10, 15, 0), date);
    }

    [Fact]
    public async Task ScanAsync_VideoFilenameEncodesDate_RecordedInFilenameDates()
    {
        VideoTestHelper.WriteCorruptVideo(Path.Combine(_tempDir, "VID_20240625_101500.mp4"));

        var result = await PhotoScanner.ScanAsync(_tempDir, includeSubfolders: false);

        Assert.Single(result.NoExif);
        Assert.True(result.FilenameDates.TryGetValue(result.NoExif[0], out var date));
        Assert.Equal(new DateTime(2024, 6, 25, 10, 15, 0), date);
    }

    [Fact]
    public async Task ScanAsync_FilenameDoesNotEncodeDate_NotRecordedInFilenameDates()
    {
        WriteJpeg("noexif.jpg");

        var result = await PhotoScanner.ScanAsync(_tempDir, includeSubfolders: false);

        Assert.Empty(result.FilenameDates);
    }
}
