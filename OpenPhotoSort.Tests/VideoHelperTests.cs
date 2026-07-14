using OpenPhotoSort.Core;
using OpenPhotoSort.Tests.TestHelpers;

namespace OpenPhotoSort.Tests;

public class VideoHelperTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public VideoHelperTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void TryGetVideoDate_ValidMp4WithCreationDate_ReturnsExpectedDate()
    {
        string path = Path.Combine(_tempDir, "clip.mp4");
        var expected = new DateTime(2023, 5, 17, 9, 15, 0, DateTimeKind.Utc);
        VideoTestHelper.WriteMinimalMp4(path, expected);

        bool found = VideoHelper.TryGetVideoDate(path, out var date);

        Assert.True(found);
        Assert.Equal(expected, date, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void TryGetVideoDate_ValidMovWithCreationDate_ReturnsExpectedDate()
    {
        // Same box layout is valid under a .mov extension too — detection is signature-based.
        string path = Path.Combine(_tempDir, "clip.mov");
        var expected = new DateTime(2022, 11, 3, 14, 0, 0, DateTimeKind.Utc);
        VideoTestHelper.WriteMinimalMp4(path, expected);

        bool found = VideoHelper.TryGetVideoDate(path, out var date);

        Assert.True(found);
        Assert.Equal(expected, date, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void TryGetVideoDate_CorruptFile_Throws()
    {
        string path = Path.Combine(_tempDir, "corrupt.mp4");
        VideoTestHelper.WriteCorruptVideo(path);

        Assert.ThrowsAny<Exception>(() => VideoHelper.TryGetVideoDate(path, out _));
    }

    [Fact]
    public void TryGetDeviceModel_NoDeviceMetadataPresent_ReturnsFalse()
    {
        // Our minimal synthetic file has no QuickTime "meta" atom, so no device tag exists.
        string path = Path.Combine(_tempDir, "clip.mp4");
        VideoTestHelper.WriteMinimalMp4(path, DateTime.UtcNow);

        bool found = VideoHelper.TryGetDeviceModel(path, out var model);

        Assert.False(found);
    }
}
