using System.Globalization;

namespace OpenPhotoSort.Core;

public static class PhotoSorter
{
    // Called from Task 4's SortAsync
    private record WorkEntry(string FilePath, DateTime Date, string CameraModel, bool IsDump);

    internal static string BuildSubfolderPath(FolderPattern pattern, DateTime date, string cameraModel)
    {
        string y = date.ToString("yyyy");
        string ym = date.ToString("yyyy_MM");
        string ymd = date.ToString("yyyy_MM_dd");
        string cam = SanitizeFolderName(cameraModel);

        return pattern switch
        {
            FolderPattern.YearMonthDay            => Path.Combine(y, ym, ymd),
            FolderPattern.YearMonthDayCameraModel => Path.Combine(y, ym, ymd, cam),
            FolderPattern.YearMonth               => Path.Combine(y, ym),
            FolderPattern.YearMonthCameraModel    => Path.Combine(y, ym, cam),
            FolderPattern.CameraModelYearMonthDay => Path.Combine(cam, y, ym, ymd),
            FolderPattern.CameraModelYearMonth    => Path.Combine(cam, y, ym),
            FolderPattern.YearOnly                => y,
            FolderPattern.MonthOnly               => ym,
            FolderPattern.DayOnly                 => ymd,
            FolderPattern.CameraModelYear         => Path.Combine(cam, y),
            _ => throw new ArgumentOutOfRangeException(nameof(pattern))
        };
    }

    internal static string FindRenameDestination(string destPath)
    {
        if (!File.Exists(destPath)) return destPath;

        string dir = Path.GetDirectoryName(destPath)!;
        string name = Path.GetFileNameWithoutExtension(destPath);
        string ext = Path.GetExtension(destPath);
        int i = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{name}-Copy{i:D3}{ext}");
            i++;
        }
        while (File.Exists(candidate));
        return candidate;
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }

    private static string GetCameraModel(Dictionary<string, Tuple<string, string>> exif)
    {
        if (exif.TryGetValue("Model", out var val) && !string.IsNullOrWhiteSpace(val.Item2))
            return val.Item2.Trim();
        return "UnknownCamera";
    }

    private static List<WorkEntry> BuildWorkList(
        ScanResult scanResult, SortOptions options, CancellationToken ct)
    {
        var result = new List<WorkEntry>();
        var undatedWithExif = new HashSet<string>(
            scanResult.WithExifButNoDate, StringComparer.OrdinalIgnoreCase);

        foreach (string filePath in scanResult.WithValidExifDate)
        {
            ct.ThrowIfCancellationRequested();
            Dictionary<string, Tuple<string, string>>? exif = null;
            try { exif = ImageHelper.ReadExifData(filePath); } catch { }
            var date = (exif != null && PhotoScanner.TryGetExifDate(exif, out var d))
                ? d : File.GetLastWriteTime(filePath);
            string camera = exif != null ? GetCameraModel(exif) : "UnknownCamera";
            result.Add(new WorkEntry(filePath, date, camera, false));
        }

        foreach (string filePath in scanResult.WithExifButNoDate.Concat(scanResult.NoExif))
        {
            ct.ThrowIfCancellationRequested();
            if (options.UseFileDateForNoExif)
            {
                var date = File.GetLastWriteTime(filePath);
                string camera = "UnknownCamera";
                if (undatedWithExif.Contains(filePath))
                {
                    try
                    {
                        var exif = ImageHelper.ReadExifData(filePath);
                        if (exif != null) camera = GetCameraModel(exif);
                    }
                    catch { }
                }
                result.Add(new WorkEntry(filePath, date, camera, false));
            }
            else if (options.DumpNoExifToFolder)
            {
                result.Add(new WorkEntry(filePath, default, "UnknownCamera", true));
            }
            // else: omit → will be skipped in summary
        }

        return result;
    }

    // SortAsync added in Task 4
}
