using System.Text;

namespace OpenPhotoSort.Tests.TestHelpers;

internal static class VideoTestHelper
{
    private static readonly DateTime Epoch1904 = new(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Writes a minimal valid MP4/QuickTime container (ftyp + moov/mvhd boxes only,
    /// no track/media data) with the given creation date encoded in the mvhd box.
    /// The same byte layout is valid for a ".mov" extension too, since format
    /// detection is signature-based, not extension-based.
    /// </summary>
    public static void WriteMinimalMp4(string filePath, DateTime creationUtc)
    {
        uint secondsSince1904 = (uint)(creationUtc.ToUniversalTime() - Epoch1904).TotalSeconds;

        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms))
        {
            // ftyp box: size(4) + 'ftyp' + major_brand(4) + minor_version(4) + compatible_brand(4)
            bw.Write(BigEndian(20));
            bw.Write(Encoding.ASCII.GetBytes("ftyp"));
            bw.Write(Encoding.ASCII.GetBytes("isom"));
            bw.Write(BigEndian(0x200));
            bw.Write(Encoding.ASCII.GetBytes("isom"));

            byte[] mvhdPayload = BuildMvhdPayload(secondsSince1904);

            // moov box wraps mvhd box
            bw.Write(BigEndian((uint)(8 + mvhdPayload.Length + 8)));
            bw.Write(Encoding.ASCII.GetBytes("moov"));
            bw.Write(BigEndian((uint)(8 + mvhdPayload.Length)));
            bw.Write(Encoding.ASCII.GetBytes("mvhd"));
            bw.Write(mvhdPayload);
        }

        File.WriteAllBytes(filePath, ms.ToArray());
    }

    /// <summary>Writes a short byte sequence that is not a recognizable video container.</summary>
    public static void WriteCorruptVideo(string filePath)
    {
        File.WriteAllBytes(filePath, new byte[] { 0x00, 0x01, 0x02, 0x03, 0xFF, 0xFE });
    }

    private static byte[] BuildMvhdPayload(uint secondsSince1904)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(new byte[4]);                    // version(1) + flags(3)
        bw.Write(BigEndian(secondsSince1904));     // creation time
        bw.Write(BigEndian(secondsSince1904));     // modification time
        bw.Write(BigEndian(1000u));                // time scale
        bw.Write(BigEndian(1000u));                // duration
        bw.Write(BigEndian(0x00010000u));          // preferred rate (1.0)
        bw.Write((byte)0x01); bw.Write((byte)0x00);// preferred volume (1.0)
        bw.Write(new byte[10]);                    // reserved
        bw.Write(new byte[36]);                    // matrix
        bw.Write(new byte[24]);                    // pre-defined
        bw.Write(BigEndian(2u));                   // next track id
        return ms.ToArray();
    }

    private static byte[] BigEndian(uint value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return bytes;
    }
}
