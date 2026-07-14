using MetadataExtractor;
using MetadataExtractor.Formats.Avi;
using MetadataExtractor.Formats.QuickTime;

namespace OpenPhotoSort.Core;

public static class VideoHelper
{
    public static bool TryGetVideoDate(string filePath, out DateTime date)
    {
        date = default;
        var directories = ImageMetadataReader.ReadMetadata(filePath);

        var quickTimeHeader = directories.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();
        if (quickTimeHeader != null &&
            quickTimeHeader.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out date))
        {
            return true;
        }

        var aviDirectory = directories.OfType<AviDirectory>().FirstOrDefault();
        if (aviDirectory != null &&
            aviDirectory.TryGetDateTime(AviDirectory.TagDateTimeOriginal, out date))
        {
            return true;
        }

        return false;
    }

    public static bool TryGetDeviceModel(string filePath, out string model)
    {
        model = string.Empty;
        var directories = ImageMetadataReader.ReadMetadata(filePath);

        var metadataHeader = directories.OfType<QuickTimeMetadataHeaderDirectory>().FirstOrDefault();
        if (metadataHeader == null) return false;

        string? value = metadataHeader.GetString(QuickTimeMetadataHeaderDirectory.TagModel);
        if (string.IsNullOrWhiteSpace(value))
            value = metadataHeader.GetString(QuickTimeMetadataHeaderDirectory.TagAndroidModel);

        if (string.IsNullOrWhiteSpace(value)) return false;

        model = value.Trim();
        return true;
    }
}
