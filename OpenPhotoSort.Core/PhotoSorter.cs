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

    internal static string GetCameraModel(Dictionary<string, Tuple<string, string>> exif)
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
            var date = MediaMetadataHelper.TryGetDate(filePath, out var d) ? d : File.GetLastWriteTime(filePath);
            string camera = MediaMetadataHelper.GetCameraModel(filePath);
            result.Add(new WorkEntry(filePath, date, camera, false));
        }

        foreach (string filePath in scanResult.WithExifButNoDate.Concat(scanResult.NoExif))
        {
            ct.ThrowIfCancellationRequested();
            if (options.UseFileDateForNoExif)
            {
                var date = File.GetLastWriteTime(filePath);
                string camera = undatedWithExif.Contains(filePath)
                    ? MediaMetadataHelper.GetCameraModel(filePath)
                    : "UnknownCamera";
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

    public static async Task<SortSummary> SortAsync(
        SortOptions options,
        ScanResult scanResult,
        IProgress<SortProgress> progress,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var workList = await Task.Run(
            () => BuildWorkList(scanResult, options, cancellationToken), cancellationToken);

        int total = workList.Count;
        int processed = 0, moved = 0, copied = 0, skipped = 0, renamed = 0, failed = 0;

        await Task.Run(() =>
        {
            foreach (var entry in workList)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string fileName = Path.GetFileName(entry.FilePath);
                bool skipFile = false;
                bool wasRenamed = false;

                try
                {
                    string destPath;
                    if (entry.IsDump)
                    {
                        Directory.CreateDirectory(options.NoExifFolderPath);
                        destPath = Path.Combine(options.NoExifFolderPath, fileName);
                    }
                    else
                    {
                        string sub = BuildSubfolderPath(options.FolderPattern, entry.Date, entry.CameraModel);
                        string destDir = Path.Combine(options.DestinationFolder, sub);
                        Directory.CreateDirectory(destDir);
                        destPath = Path.Combine(destDir, fileName);
                    }

                    if (File.Exists(destPath))
                    {
                        switch (options.ConflictBehavior)
                        {
                            case ConflictBehavior.DoNotCopyOrMove:
                                skipFile = true;
                                skipped++;
                                break;
                            case ConflictBehavior.RenameCopy:
                                destPath = FindRenameDestination(destPath);
                                wasRenamed = true;
                                break;
                            case ConflictBehavior.Overwrite:
                                break;
                            case ConflictBehavior.DuplicatesFolder:
                                Directory.CreateDirectory(options.DuplicatesFolderPath);
                                string dupBase = Path.Combine(options.DuplicatesFolderPath, fileName);
                                string dupDest = FindRenameDestination(dupBase);
                                wasRenamed = dupDest != dupBase;
                                destPath = dupDest;
                                break;
                        }
                    }

                    if (!skipFile)
                    {
                        bool overwrite = options.ConflictBehavior == ConflictBehavior.Overwrite;
                        File.Copy(entry.FilePath, destPath, overwrite);
                        if (options.Operation == SortOperation.Move)
                        {
                            File.Delete(entry.FilePath);
                            moved++;
                        }
                        else
                        {
                            copied++;
                        }
                        if (wasRenamed) renamed++;
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch { if (!skipFile) failed++; }
                finally
                {
                    processed++;
                    progress?.Report(new SortProgress(processed, total, fileName));
                }
            }
        }, cancellationToken);

        return new SortSummary(moved, copied, skipped, renamed, failed);
    }
}
