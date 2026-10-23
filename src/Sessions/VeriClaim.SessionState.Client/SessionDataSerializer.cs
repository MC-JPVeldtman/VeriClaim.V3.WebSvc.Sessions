using System.IO;
using System.IO.Compression;
using System.Web;
using System.Web.SessionState;

namespace VeriClaim.SessionState.Client
{
    /// <summary>
    /// Same binary layout ASP.NET's own SQL Server provider uses. Anything stored in Session must be [Serializable];
    /// ASP.NET reports "Unable to serialize the session state" at the end of the request when it is not.
    /// </summary>
    internal static class SessionDataSerializer
    {
        private const byte FormatRaw = 0;
        private const byte FormatGZip = 1;

        public static byte[] Serialize(SessionStateStoreData item, bool compress)
        {
            using (var buffer = new MemoryStream())
            {
                // Write the format first so turning compression on or off never breaks sessions already stored.
                buffer.WriteByte(compress ? FormatGZip : FormatRaw);

                using (var payload = compress ? (Stream)new GZipStream(buffer, CompressionLevel.Fastest, true) : new NonClosingStream(buffer))
                using (var writer = new BinaryWriter(payload))
                {
                    var hasItems = item.Items != null && item.Items.Count > 0;
                    var hasStaticObjects = item.StaticObjects != null && !item.StaticObjects.NeverAccessed;

                    writer.Write(item.Timeout);
                    writer.Write(hasItems);
                    writer.Write(hasStaticObjects);
                    if (hasItems) ((SessionStateItemCollection)item.Items).Serialize(writer);
                    if (hasStaticObjects) item.StaticObjects.Serialize(writer);
                }

                return buffer.ToArray();
            }
        }

        public static SessionStateStoreData Deserialize(HttpContext context, byte[] data)
        {
            using (var buffer = new MemoryStream(data, 1, data.Length - 1))
            using (var payload = data[0] == FormatGZip ? (Stream)new GZipStream(buffer, CompressionMode.Decompress) : buffer)
            using (var reader = new BinaryReader(payload))
            {
                var timeout = reader.ReadInt32();
                var hasItems = reader.ReadBoolean();
                var hasStaticObjects = reader.ReadBoolean();

                var items = hasItems ? SessionStateItemCollection.Deserialize(reader) : new SessionStateItemCollection();
                var staticObjects = hasStaticObjects ? HttpStaticObjectsCollection.Deserialize(reader) : SessionStateUtility.GetSessionStaticObjects(context);

                return new SessionStateStoreData(items, staticObjects, timeout);
            }
        }

        /// <summary>Lets the BinaryWriter be disposed without closing the MemoryStream we still need to read.</summary>
        private sealed class NonClosingStream : Stream
        {
            private readonly Stream _inner;

            public NonClosingStream(Stream inner)
            {
                _inner = inner;
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => _inner.Position = value; }
            public override void Flush() => _inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => throw new System.NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new System.NotSupportedException();
            public override void SetLength(long value) => throw new System.NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        }
    }
}
