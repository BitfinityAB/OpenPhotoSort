using ImageMagick;
using OpenPhotoSort.Core;
using OpenPhotoSort.Tests.TestHelpers;

namespace OpenPhotoSort.Tests;

public class MediaMetadataHelperTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public MediaMetadataHelperTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Theory]
    [InlineData(".mp4")]
    [InlineData(".mov")]
    [InlineData(".avi")]
    [InlineData(".mkv")]
    [InlineData(".wmv")]
    [InlineData(".m4v")]
    [InlineData(".3gp")]
    public void IsVideoFile_VideoExtensions_ReturnsTrue(string extension)
    {
        Assert.True(MediaMetadataHelper.IsVideoFile($"clip{extension}"));
    }

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".png")]
    [InlineData(".heic")]
    public void IsVideoFile_ImageExtensions_ReturnsFalse(string extension)
    {
        Assert.False(MediaMetadataHelper.IsVideoFile($"photo{extension}"));
    }

    [Fact]
    public void TryGetDate_VideoFileWithDate_DispatchesToVideoHelper()
    {
        string path = Path.Combine(_tempDir, "clip.mp4");
        var expected = new DateTime(2023, 5, 17, 9, 15, 0, DateTimeKind.Utc);
        VideoTestHelper.WriteMinimalMp4(path, expected);

        bool found = MediaMetadataHelper.TryGetDate(path, out var date);

        Assert.True(found);
        Assert.Equal(expected, date, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void TryGetDate_CorruptVideoFile_ReturnsFalseWithoutThrowing()
    {
        string path = Path.Combine(_tempDir, "corrupt.mp4");
        VideoTestHelper.WriteCorruptVideo(path);

        bool found = MediaMetadataHelper.TryGetDate(path, out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGetDate_ImageFileWithExifDate_DispatchesToImageHelper()
    {
        string path = Path.Combine(_tempDir, "photo.jpg");
        var image = new MagickImage(MagickColors.Red, 1, 1);
        var profile = new ExifProfile();
        profile.SetValue(ExifTag.DateTimeOriginal, "2024:06:25 10:30:00");
        image.SetProfile(profile);
        image.Write(path, MagickFormat.Jpeg);

        bool found = MediaMetadataHelper.TryGetDate(path, out var date);

        Assert.True(found);
        Assert.Equal(new DateTime(2024, 6, 25, 10, 30, 0), date);
    }

    [Fact]
    public void GetCameraModel_VideoFileWithNoDeviceTag_ReturnsUnknownCamera()
    {
        string path = Path.Combine(_tempDir, "clip.mp4");
        VideoTestHelper.WriteMinimalMp4(path, DateTime.UtcNow);

        string model = MediaMetadataHelper.GetCameraModel(path);

        Assert.Equal("UnknownCamera", model);
    }

    [Fact]
    public void GetCameraModel_ImageFileWithModelTag_ReturnsModel()
    {
        string path = Path.Combine(_tempDir, "photo.jpg");
        var image = new MagickImage(MagickColors.Red, 1, 1);
        var profile = new ExifProfile();
        profile.SetValue(ExifTag.Model, "TestCam");
        image.SetProfile(profile);
        image.Write(path, MagickFormat.Jpeg);

        string model = MediaMetadataHelper.GetCameraModel(path);

        Assert.Equal("TestCam", model);
    }

    [Fact]
    public void GetCameraModel_CorruptVideoFile_ReturnsUnknownCameraWithoutThrowing()
    {
        string path = Path.Combine(_tempDir, "corrupt.mp4");
        VideoTestHelper.WriteCorruptVideo(path);

        string model = MediaMetadataHelper.GetCameraModel(path);

        Assert.Equal("UnknownCamera", model);
    }
}
