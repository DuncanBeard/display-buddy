using System.Buffers.Binary;

namespace TaskBard.Tests;

/// <summary>
/// Reads pixel sizes straight from PNG and ICO headers, so the asset checks
/// need no imaging library and can't be fooled by a file's name.
/// </summary>
internal static class ImageHeaders
{
    private static readonly byte[] PngSignature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    public static (int Width, int Height) PngSize(string path) => PngSize(File.ReadAllBytes(path), path);

    /// <summary>
    /// Sizes of every frame in an ICO file. Each size comes from the frame's
    /// own PNG or BMP header, after checking that it agrees with the
    /// directory entry.
    /// </summary>
    public static IReadOnlyList<(int Width, int Height)> IcoFrameSizes(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var data = bytes.AsSpan();
        if (data.Length < 6 || BinaryPrimitives.ReadUInt16LittleEndian(data) != 0
            || BinaryPrimitives.ReadUInt16LittleEndian(data[2..]) != 1)
            throw new InvalidDataException($"{path} is not an ICO file");

        int count = BinaryPrimitives.ReadUInt16LittleEndian(data[4..]);
        var sizes = new List<(int, int)>(count);
        for (int i = 0; i < count; i++)
        {
            var entry = data.Slice(6 + i * 16, 16);
            int dirWidth = entry[0] == 0 ? 256 : entry[0];
            int dirHeight = entry[1] == 0 ? 256 : entry[1];
            int length = BinaryPrimitives.ReadInt32LittleEndian(entry[8..]);
            int offset = BinaryPrimitives.ReadInt32LittleEndian(entry[12..]);
            var image = bytes.AsSpan(offset, length);

            (int Width, int Height) actual = image.StartsWith(PngSignature)
                ? PngSize(image.ToArray(), path)
                // BITMAPINFOHEADER: the height covers the XOR and AND masks, so it is doubled.
                : (BinaryPrimitives.ReadInt32LittleEndian(image[4..]), BinaryPrimitives.ReadInt32LittleEndian(image[8..]) / 2);

            if (actual != (dirWidth, dirHeight))
                throw new InvalidDataException(
                    $"{path} frame {i}: directory says {dirWidth}x{dirHeight}, image is {actual.Width}x{actual.Height}");
            sizes.Add(actual);
        }
        return sizes;
    }

    private static (int Width, int Height) PngSize(byte[] bytes, string path)
    {
        // Signature (8) + IHDR length (4) + "IHDR" (4), then big-endian width and height.
        if (bytes.Length < 24 || !bytes.AsSpan().StartsWith(PngSignature)
            || bytes[12] != 'I' || bytes[13] != 'H' || bytes[14] != 'D' || bytes[15] != 'R')
            throw new InvalidDataException($"{path} is not a PNG file");

        return (BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20)));
    }
}
