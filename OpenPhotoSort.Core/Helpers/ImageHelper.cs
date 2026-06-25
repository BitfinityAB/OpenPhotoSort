using ImageMagick;

namespace OpenPhotoSort.Core
{
    public static class ImageHelper
    {
        public static Dictionary<string, Tuple<string, string>>? ReadExifData(string fileName)
        {
            // Read image from file
            using var image = new MagickImage(fileName);

            // Retrieve the exif information
            var profile = image.GetExifProfile();

            // Check if image contains an exif profile
            if (profile is null)
            {
                Console.WriteLine("Image does not contain exif information.");
                return null;
            }
            else
            {
                var exifData = new Dictionary<string, Tuple<string, string>>();
                // Write all values to the console
                foreach (var value in profile.Values)
                {
                    Console.WriteLine("{0}({1}): {2}", value.Tag, value.DataType, value.ToString());
                    exifData[value.Tag.ToString()] = new Tuple<string, string>(value.DataType.ToString(), value.ToString() ?? string.Empty);
                }
                return exifData;
            }
        }
    }
}
