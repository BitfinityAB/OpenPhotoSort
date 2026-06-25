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
}
