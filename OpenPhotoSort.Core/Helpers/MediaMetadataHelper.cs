namespace OpenPhotoSort.Core;

internal static class MediaMetadataHelper
{
    internal static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".m4v", ".3gp"
    };

    internal static bool IsVideoFile(string filePath) =>
        VideoExtensions.Contains(Path.GetExtension(filePath));

    internal static bool TryGetDate(string filePath, out DateTime date)
    {
        date = default;

        if (IsVideoFile(filePath))
        {
            try { return VideoHelper.TryGetVideoDate(filePath, out date); }
            catch { return false; }
        }

        Dictionary<string, Tuple<string, string>>? exif = null;
        try { exif = ImageHelper.ReadExifData(filePath); } catch { }
        return exif != null && PhotoScanner.TryGetExifDate(exif, out date);
    }

    internal static string GetCameraModel(string filePath)
    {
        if (IsVideoFile(filePath))
        {
            try
            {
                return VideoHelper.TryGetDeviceModel(filePath, out var model) ? model : "UnknownCamera";
            }
            catch { return "UnknownCamera"; }
        }

        try
        {
            var exif = ImageHelper.ReadExifData(filePath);
            return exif != null ? PhotoSorter.GetCameraModel(exif) : "UnknownCamera";
        }
        catch { return "UnknownCamera"; }
    }
}
