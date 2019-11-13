using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using VLC;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace TorchPlayer
{
    public class FTPServerAddress
    {
        public string Server { get; set; }
        public int Port { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string Address => $"ftp://{Server.Trim()}:{Port}";
        public string CredentialAddress => $"ftp://{User}:{Password}@{Server.Trim()}:{Port}";
        public Uri Uri => new Uri(Address);
        public NetworkCredential Credential => new NetworkCredential(User, Password);

        public string GetAddress(string sub)
        {
            return Address + "/" + sub.Replace('\\', '/').TrimStart('/');
        }

        public string GetAddressWithCredential(string sub)
        {
            return CredentialAddress + "/" + sub.Replace('\\', '/').TrimStart('/');
        }
    }

    public class FTPRandomAccessStream : IRandomAccessStream
    {
        public IInputStream GetInputStreamAt(ulong position)
        {
            var prePosition = Position;
            parent.Seek((long)position, SeekOrigin.Begin);
            var input = parent.Clone().AsInputStream();
            parent.Seek((long)prePosition, SeekOrigin.Begin);
            return input;
        }

        public IOutputStream GetOutputStreamAt(ulong position)
        {
            throw new NotImplementedException();
        }

        public void Seek(ulong position)
        {
            parent.Seek((long)position, SeekOrigin.Begin);
        }

        public IRandomAccessStream CloneStream()
        {
            return new FTPRandomAccessStream(parent.Clone());
        }

        public bool CanRead => parent.CanRead;

        public bool CanWrite => parent.CanWrite;

        public ulong Position => (ulong)parent.Position;

        public ulong Size { get => (ulong)parent.Length; set => parent.SetLength((long)value); }

        FTPStream parent;
        IRandomAccessStream randomAccessStream;
        public FTPRandomAccessStream(FTPStream parent)
        {
            this.parent = parent;
            randomAccessStream = parent.AsRandomAccessStream();
        }

        public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options)
        {
            return randomAccessStream.ReadAsync(buffer, count, options);
        }

        public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer)
        {
            return randomAccessStream.WriteAsync(buffer);
        }

        public IAsyncOperation<bool> FlushAsync()
        {
            return randomAccessStream.FlushAsync();
        }

        public void Dispose()
        {
            parent?.Dispose();
            parent = null;

            randomAccessStream?.Dispose();
            randomAccessStream = null;
        }
    }

    /// <summary>
    /// Seekable FTP File Stream is a Stream wrapper around a WebRequest creating a seekable network stream.
    /// This is performed by reconnecting to the FTP when a negative position change is requested.
    /// </summary>
    public class FTPStream : Stream, IDisposable
    {
        public NetworkCredential Credential { get; set; }
        public override bool CanRead => (Position < Length);
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => position; set => position = value; }

        /// <summary>
        /// Request URI saved to create new requests
        /// </summary>
        readonly Uri requestUri;
        /// <summary>
        /// Length of file requested, used for boundary when seeking
        /// </summary>
        readonly long length;
        /// <summary>
        /// Current position, used to move cursor to the right position
        /// </summary>
        long position = 0;
        /// <summary>
        /// Pointing to the current reading position on the stream
        /// </summary>
        long cursor = 0;

        FtpWebResponse response = null;
        Stream stream;

        public FTPStream(Uri requestUri, NetworkCredential credential = null)
        {
            Credential = credential;
            this.requestUri = requestUri;

            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(requestUri);
            request.Credentials = credential;
            request.Method = WebRequestMethods.Ftp.GetFileSize;
            using (var response = (FtpWebResponse)request.GetResponse())
                length = response.ContentLength;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset + count > buffer.Length)
                throw new Exception();

            if (stream == null)
                OpenConnection(position);

            int bytesRead = stream.Read(buffer, offset, count);

            cursor += bytesRead;
            position = cursor;

            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    position = offset;
                    break;
                case SeekOrigin.End:
                    position = length + offset;
                    break;
                default:
                    position = position + offset;
                    break;
            }

            if (position < 0 || position > length)
                throw new Exception();

            if (cursor != position)
                OpenConnection(position);

            return position;
        }

        public FTPStream Clone()
        {
            var clone = new FTPStream(requestUri, Credential);
            clone.Seek(Position, SeekOrigin.Begin);
            return clone;
        }

        public new void Dispose() => CloseConnection();
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        void OpenConnection(long offset = 0)
        {
            CloseConnection();

            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(requestUri);
            if (Credential != null)
                request.Credentials = Credential;
            request.UsePassive = true;
            request.KeepAlive = true;
            request.UseBinary = true;
            request.Method = WebRequestMethods.Ftp.DownloadFile;
            request.ContentOffset = offset;

            response = (FtpWebResponse)request.GetResponse();
            if (response.StatusCode != FtpStatusCode.OpeningData)
                throw new FileNotFoundException("Unable to open data stream");
            stream = response.GetResponseStream();

            position = cursor = offset;
        }

        void CloseConnection()
        {
            response?.Dispose();
            response = null;

            stream?.Dispose();
            stream = null;

            cursor = position = -1;
        }
    }
}