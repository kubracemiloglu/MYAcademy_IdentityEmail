using StbImageSharp;
using System.Buffers.Binary;

namespace MYAcademy_IdentityEmail.Helpers
{
    public static class ProfilePhotoValidator
    {
        public static string? ValidExtension(byte[] bytes, string contentType)
        {
            var extension = contentType == "image/png" && bytes.Length >= 8
                && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? ".png"
                : contentType == "image/jpeg" && bytes.Length >= 4
                && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 ? ".jpg" : null;
            if (extension == null || bytes.Length > 2 * 1024 * 1024) return null;
            if (extension == ".png" && !HasBoundedPngChunks(bytes)) return null;
            try
            {
                using var stream = new BoundedImageStream(bytes);
                var info = ImageInfo.FromStream(stream);
                // Inspect dimensions before allocating the decoded pixel buffer.
                if (info is not { Width: > 0 and <= 4096, Height: > 0 and <= 4096 }) return null;
                stream.Position = 0;
                var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
                return image.Width == info.Value.Width && image.Height == info.Value.Height
                    && image.Data.Length == (long)image.Width * image.Height * 4 ? extension : null;
            }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException
                or IOException or OverflowException or IndexOutOfRangeException)
            {
                return null;
            }
        }

        private static bool HasBoundedPngChunks(byte[] bytes)
        {
            var position = 8;
            var hasPixels = false;
            while (position <= bytes.Length - 12)
            {
                var size = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position, 4));
                if (size > bytes.Length - position - 12) return false;
                var type = bytes.AsSpan(position + 4, 4);
                if (position == 8 && (!type.SequenceEqual("IHDR"u8) || size != 13)) return false;
                if (type.SequenceEqual("IDAT"u8)) hasPixels = true;
                position += (int)size + 12;
                if (type.SequenceEqual("IEND"u8)) return size == 0 && hasPixels && position == bytes.Length;
            }
            return false;
        }

        // Malformed JPEG headers may repeatedly request bytes after EOF. Reject
        // those reads and cap total parsing work, including decoder rewinds.
        private sealed class BoundedImageStream(byte[] bytes) : MemoryStream(bytes, writable: false)
        {
            private long _remainingReads = (long)bytes.Length * 32 + 1024 * 1024;
            public override int ReadByte()
            {
                if (--_remainingReads < 0 || Position >= Length) throw new EndOfStreamException();
                return base.ReadByte();
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                _remainingReads -= count;
                if (_remainingReads < 0 || count > Length - Position) throw new EndOfStreamException();
                return base.Read(buffer, offset, count);
            }
            public override long Seek(long offset, SeekOrigin origin)
            {
                var target = origin switch
                {
                    SeekOrigin.Begin => offset,
                    SeekOrigin.Current => Position + offset,
                    SeekOrigin.End => Length + offset,
                    _ => -1
                };
                if (target < 0 || target > Length) throw new EndOfStreamException();
                return base.Seek(offset, origin);
            }
        }
    }
}
