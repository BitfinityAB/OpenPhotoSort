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
        cancellationToken.ThrowIfCancellationRequested();
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
