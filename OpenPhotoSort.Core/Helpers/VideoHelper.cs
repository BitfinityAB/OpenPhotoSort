using MetadataExtractor;
using MetadataExtractor.Formats.Avi;
using MetadataExtractor.Formats.QuickTime;

namespace OpenPhotoSort.Core;

public static class VideoHelper
{
    public static bool TryGetVideoDate(string filePath, out DateTime date) =>
        TryGetDateAndDeviceModel(filePath, out date, out _);

    public static bool TryGetDeviceModel(string filePath, out string model)
    {
        TryGetDateAndDeviceModel(filePath, out _, out model);
        return !string.IsNullOrEmpty(model);
    }

    public static bool TryGetDateAndDeviceModel(string filePath, out DateTime date, out string model)
    {
        date = default;
        model = string.Empty;
        var directories = ImageMetadataReader.ReadMetadata(filePath);

        bool foundDate = false;
        var quickTimeHeader = directories.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();
        if (quickTimeHeader != null &&
            quickTimeHeader.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out date))
        {
            foundDate = true;
        }

        if (!foundDate)
        {
            var aviDirectory = directories.OfType<AviDirectory>().FirstOrDefault();
            if (aviDirectory != null &&
                aviDirectory.TryGetDateTime(AviDirectory.TagDateTimeOriginal, out date))
            {
                foundDate = true;
            }
        }

        var metadataHeader = directories.OfType<QuickTimeMetadataHeaderDirectory>().FirstOrDefault();
        if (metadataHeader != null)
        {
            string? value = metadataHeader.GetString(QuickTimeMetadataHeaderDirectory.TagModel);
            if (string.IsNullOrWhiteSpace(value))
                value = metadataHeader.GetString(QuickTimeMetadataHeaderDirectory.TagAndroidModel);
            if (!string.IsNullOrWhiteSpace(value))
                model = value.Trim();
        }

        return foundDate;
    }
}
