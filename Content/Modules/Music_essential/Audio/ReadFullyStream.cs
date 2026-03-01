using System;
using System.IO;

namespace MarySGameEngine.Modules.Music_essential.Audio
{
    /// <summary>
    /// Wraps a stream so that Read() does not return until the requested number of bytes
    /// have been read or the source stream reaches end. Prevents partial reads that break MP3 frame parsing.
    /// </summary>
    public sealed class ReadFullyStream : Stream
    {
        private readonly Stream _source;

        public ReadFullyStream(Stream source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public override bool CanRead => true;
        public override bool CanSeek => _source.CanSeek;
        public override bool CanWrite => false;
        public override long Length
        {
            get { try { return _source.CanSeek ? _source.Length : 0; } catch { return 0; } }
        }
        public override long Position { get => _source.Position; set => _source.Position = value; }

        public override void Flush() => _source.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int n = _source.Read(buffer, offset + totalRead, count - totalRead);
                if (n == 0) break;
                totalRead += n;
            }
            return totalRead;
        }

        public override long Seek(long offset, SeekOrigin origin) => _source.Seek(offset, origin);

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _source.Dispose();
            base.Dispose(disposing);
        }
    }
}
