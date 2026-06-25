namespace OpenPhotoSort.Core;

public record SortSummary(int Moved, int Copied, int Skipped, int Renamed, int Failed);
