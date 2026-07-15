namespace OpenPhotoSort.Core;

public record ScanResult(
    IReadOnlyList<string> WithValidExifDate,
    IReadOnlyList<string> WithExifButNoDate,
    IReadOnlyList<string> NoExif,
    IReadOnlyDictionary<string, DateTime> FilenameDates)
{
    public int TotalFiles => WithValidExifDate.Count + WithExifButNoDate.Count + NoExif.Count;
}
