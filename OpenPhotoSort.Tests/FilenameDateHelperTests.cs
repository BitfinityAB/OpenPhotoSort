using OpenPhotoSort.Core;

namespace OpenPhotoSort.Tests;

public class FilenameDateHelperTests
{
    [Theory]
    [InlineData("IMG_20240625_101500.jpg", 2024, 6, 25, 10, 15, 0)]
    [InlineData("VID_20240625_101500.mp4", 2024, 6, 25, 10, 15, 0)]
    [InlineData("PXL_20240625_101500123.jpg", 2024, 6, 25, 10, 15, 0)]
    [InlineData("PXL_20240625_101500123.mp4", 2024, 6, 25, 10, 15, 0)]
    [InlineData("20240625_101500.jpg", 2024, 6, 25, 10, 15, 0)]
    [InlineData("Screenshot_20240625-101500.png", 2024, 6, 25, 10, 15, 0)]
    [InlineData("2024-06-25 10.15.00.jpg", 2024, 6, 25, 10, 15, 0)]
    [InlineData("WhatsApp Image 2024-06-25 at 10.15.00.jpeg", 2024, 6, 25, 10, 15, 0)]
    [InlineData("WhatsApp Video 2024-06-25 at 10.15.00.mp4", 2024, 6, 25, 10, 15, 0)]
    public void TryParseDate_SupportedPatterns_ReturnsExpectedDate(
        string fileName, int y, int mo, int d, int h, int mi, int s)
    {
        bool found = FilenameDateHelper.TryParseDate(fileName, out var date);

        Assert.True(found);
        Assert.Equal(new DateTime(y, mo, d, h, mi, s), date);
    }

    [Theory]
    [InlineData("random_filename.jpg")]
    [InlineData("DSC00001.jpg")]
    [InlineData("IMG_20241325_101500.jpg")]   // month 13 (out of range)
    [InlineData("IMG_20240632_101500.jpg")]   // day 32 (out of range)
    [InlineData("IMG_18990101_101500.jpg")]   // year before 1990
    [InlineData("IMG_20990101_101500.jpg")]   // year too far in the future
    public void TryParseDate_UnsupportedOrInvalid_ReturnsFalse(string fileName)
    {
        bool found = FilenameDateHelper.TryParseDate(fileName, out _);

        Assert.False(found);
    }

    [Theory]
    [InlineData("img_20240625_101500.jpg", 2024, 6, 25, 10, 15, 0)]
    [InlineData("vid_20240625_101500.mp4", 2024, 6, 25, 10, 15, 0)]
    [InlineData("pxl_20240625_101500123.jpg", 2024, 6, 25, 10, 15, 0)]
    [InlineData("screenshot_20240625-101500.png", 2024, 6, 25, 10, 15, 0)]
    [InlineData("whatsapp image 2024-06-25 at 10.15.00.jpeg", 2024, 6, 25, 10, 15, 0)]
    public void TryParseDate_LowercasePrefix_ReturnsExpectedDate(
        string fileName, int y, int mo, int d, int h, int mi, int s)
    {
        bool found = FilenameDateHelper.TryParseDate(fileName, out var date);

        Assert.True(found);
        Assert.Equal(new DateTime(y, mo, d, h, mi, s), date);
    }

    [Theory]
    [InlineData("2026-07-09 10.51.02-1.jpg", 2026, 7, 9, 10, 51, 2)]
    [InlineData("2026-07-11 12.43.23-3.jpg", 2026, 7, 11, 12, 43, 23)]
    [InlineData("IMG_20240625_101500-2.jpg", 2024, 6, 25, 10, 15, 0)]
    [InlineData("PXL_20240625_101500123-1.jpg", 2024, 6, 25, 10, 15, 0)]
    [InlineData("WhatsApp Image 2024-06-25 at 10.15.00-1.jpeg", 2024, 6, 25, 10, 15, 0)]
    public void TryParseDate_TrailingDisambiguatorSuffix_ReturnsExpectedDate(
        string fileName, int y, int mo, int d, int h, int mi, int s)
    {
        bool found = FilenameDateHelper.TryParseDate(fileName, out var date);

        Assert.True(found);
        Assert.Equal(new DateTime(y, mo, d, h, mi, s), date);
    }

    [Fact]
    public void TryParseDate_FullPathWithDirectory_UsesFilenameOnly()
    {
        string path = System.IO.Path.Combine("C:", "Dropbox", "Camera Uploads", "2024-06-25 10.15.00.jpg");

        bool found = FilenameDateHelper.TryParseDate(path, out var date);

        Assert.True(found);
        Assert.Equal(new DateTime(2024, 6, 25, 10, 15, 0), date);
    }
}
