using System.Text.RegularExpressions;

namespace OpenPhotoSort.Core;

internal static class FilenameDateHelper
{
    private const int MinYear = 1990;

    private static readonly Regex[] Patterns =
    {
        // IMG_20240625_101500.jpg / VID_20240625_101500.mp4
        new(@"^(?:IMG|VID)_(?<y>\d{4})(?<mo>\d{2})(?<d>\d{2})_(?<h>\d{2})(?<mi>\d{2})(?<s>\d{2})$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // PXL_20240625_101500123.jpg / .mp4 (Pixel, trailing milliseconds)
        new(@"^PXL_(?<y>\d{4})(?<mo>\d{2})(?<d>\d{2})_(?<h>\d{2})(?<mi>\d{2})(?<s>\d{2})\d{3}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // 20240625_101500.jpg (bare)
        new(@"^(?<y>\d{4})(?<mo>\d{2})(?<d>\d{2})_(?<h>\d{2})(?<mi>\d{2})(?<s>\d{2})$",
            RegexOptions.Compiled),
        // Screenshot_20240625-101500.png
        new(@"^Screenshot_(?<y>\d{4})(?<mo>\d{2})(?<d>\d{2})-(?<h>\d{2})(?<mi>\d{2})(?<s>\d{2})$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // 2024-06-25 10.15.00.jpg
        new(@"^(?<y>\d{4})-(?<mo>\d{2})-(?<d>\d{2}) (?<h>\d{2})\.(?<mi>\d{2})\.(?<s>\d{2})$",
            RegexOptions.Compiled),
        // WhatsApp Image 2024-06-25 at 10.15.00.jpeg / WhatsApp Video ... .mp4
        new(@"^WhatsApp (?:Image|Video) (?<y>\d{4})-(?<mo>\d{2})-(?<d>\d{2}) at (?<h>\d{2})\.(?<mi>\d{2})\.(?<s>\d{2})$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
    };

    internal static bool TryParseDate(string filePath, out DateTime date)
    {
        date = default;
        string name = Path.GetFileNameWithoutExtension(filePath);

        foreach (var pattern in Patterns)
        {
            var match = pattern.Match(name);
            if (!match.Success) continue;

            int year = int.Parse(match.Groups["y"].Value);
            if (year < MinYear || year > DateTime.Now.Year + 1) continue;

            int month = int.Parse(match.Groups["mo"].Value);
            int day = int.Parse(match.Groups["d"].Value);
            int hour = int.Parse(match.Groups["h"].Value);
            int minute = int.Parse(match.Groups["mi"].Value);
            int second = int.Parse(match.Groups["s"].Value);

            try
            {
                date = new DateTime(year, month, day, hour, minute, second);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }
        }

        return false;
    }
}
