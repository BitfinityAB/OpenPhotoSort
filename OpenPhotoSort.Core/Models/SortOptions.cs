namespace OpenPhotoSort.Core;

public enum FolderPattern
{
    YearMonthDay,               // 2024/2024_06/2024_06_25
    YearMonthDayCameraModel,    // 2024/2024_06/2024_06_25/Canon EOS R5
    YearMonth,                  // 2024/2024_06
    YearMonthCameraModel,       // 2024/2024_06/Canon EOS R5
    CameraModelYearMonthDay,    // Canon EOS R5/2024/2024_06/2024_06_25
    CameraModelYearMonth,       // Canon EOS R5/2024/2024_06
    YearOnly,                   // 2024
    MonthOnly,                  // 2024_06
    DayOnly,                    // 2024_06_25
    CameraModelYear             // Canon EOS R5/2024
}

public enum ConflictBehavior
{
    DoNotCopyOrMove,
    RenameCopy,
    Overwrite,
    DuplicatesFolder
}

public enum SortOperation { Copy, Move }

public record SortOptions(
    string SourceFolder,
    string DestinationFolder,
    FolderPattern FolderPattern,
    ConflictBehavior ConflictBehavior,
    string DuplicatesFolderPath,
    bool UseFileDateForNoExif,
    bool DumpNoExifToFolder,
    string NoExifFolderPath,
    bool IncludeSubfolders,
    SortOperation Operation);
