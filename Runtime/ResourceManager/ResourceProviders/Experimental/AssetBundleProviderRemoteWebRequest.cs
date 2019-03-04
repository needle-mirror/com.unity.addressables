using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;

#if !UNITY_METRO
namespace UnityEngine.ResourceManagement.ResourceProviders.Experimental
{
    /// <summary>
    /// Loads an asset bundle via the AssetBundle.LoadFromStreamAsync API.  The bundle is downloaded via WebRequest as a byte array and bypasses the normal asset bundle loading code.
    /// This is provided as an example to extend to support cases such as using the .NET HttpClient API or injecting a decryption layer into loading bundles.
    /// </summary>
    public class AssetBundleProviderRemoteWebRequest : ResourceProviderBase
    {
        internal class InternalOp<TObject> : AsyncOperationBase<TObject>, IDisposable
            where TObject : class
        {
            bool m_Complete;
            int m_StartFrame;
            ChunkedMemoryStream m_Data;
            byte[] m_Buffer = new byte[1024 * 1024];

            public InternalOp<TObject> Start(IResourceLocation location)
            {
                Validate();
                m_Result = null;
                Context = location;
                m_Complete = false;
                m_StartFrame = Time.frameCount;
                m_Data = new ChunkedMemoryStream();
                CompletionUpdater.UpdateUntilComplete("WebRequest" + location.InternalId, CompleteInMainThread);
                var req = WebRequest.Create(location.InternalId);
                req.BeginGetResponse(AsyncCallback, req);
                return this;
            }

            void AsyncCallback(IAsyncResult ar)
            {
                Validate();
                HttpWebRequest req = ar.AsyncState as HttpWebRequest;
                if (req == null)
                    return;
                var response = req.EndGetResponse(ar) as HttpWebResponse;
                if (response == null)
                    return;
                var stream = response.GetResponseStream();
                if (stream == null)
                    return;
                stream.BeginRead(m_Buffer, 0, m_Buffer.Length, OnRead, stream);
            }

            void OnRead(IAsyncResult ar)
            {
                Validate();
                var responseStream = ar.AsyncState as Stream;
                if (responseStream == null)
                    return;
                int read = responseStream.EndRead(ar);
                if (read > 0)
                {
                    m_Data.Write(m_Buffer, 0, read);
                    responseStream.BeginRead(m_Buffer, 0, m_Buffer.Length, OnRead, responseStream);
                }
                else
                {
                    m_Data.Position = 0;
                    m_Complete = true;
                    responseStream.Close();
                }
            }

            public bool CompleteInMainThread()
            {
                Validate();
                if (!m_Complete)
                    return false;
                AssetBundle.LoadFromStreamAsync(m_Data).completed += InternalOp_completed;
                return true;
            }

            void InternalOp_completed(AsyncOperation obj)
            {
                Validate();
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.LoadAsyncCompletion, Context, Time.frameCount - m_StartFrame);
                TObject result = null;
                if (obj is AssetBundleCreateRequest)
                    result = (obj as AssetBundleCreateRequest).assetBundle as TObject;
                SetResult(result);
                InvokeCompletionEvent();
                m_Data.Close();
                m_Data.Dispose();
                m_Data = null;
            }

            public void Dispose()
            {
                Validate();
                if (m_Data != null)
                {
                    m_Data.Close();
                    m_Data.Dispose();
                    m_Data = null;
                }
            }
        }

        /// <inheritdoc/>
        public override IAsyncOperation<TObject> Provide<TObject>(IResourceLocation location, IList<object> deps)
        {
            var operation = AsyncOperationCache.Instance.Acquire<InternalOp<TObject>>();
            return operation.Start(location);
        }

        /// <inheritdoc/>
        public override bool Release(IResourceLocation location, object asset)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            var assetBundle = asset as AssetBundle;
            if (assetBundle != null)
                assetBundle.Unload(true);
            return true;
        }
    }

    sealed class ChunkedMemoryStream : Stream
    {
        const int k_BufferSize = 65536;
        readonly List<byte[]> m_Chunks;
        long m_Length;
        long m_Position;

        public ChunkedMemoryStream()
        {
            m_Chunks = new List<byte[]> { new byte[k_BufferSize], new byte[k_BufferSize] };
            m_Position = 0;
            m_Length = 0;
        }

        public void Reset()
        {
            m_Position = 0;
            m_Length = 0;
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }
        public override bool CanWrite { get { return true; } }
        public override long Length { get { return m_Length; } }
        long Capacity { get { return m_Chunks.Count * k_BufferSize; } }
        byte[] CurrentChunk { get { return m_Chunks[Convert.ToInt32(m_Position / k_BufferSize)]; } }
        int PositionInChunk { get { return Convert.ToInt32(m_Position % k_BufferSize); } }
        int RemainingBytesInCurrentChunk { get { return CurrentChunk.Length - PositionInChunk; } }
        public override void Flush() { }

        public override long Position
        {
            get { return m_Position; }
            set
            {
                m_Position = value;
                if (m_Position > m_Length)
                    m_Position = m_Length - 1;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesToRead = count;
            if (m_Length - m_Position < bytesToRead)
                bytesToRead = Convert.ToInt32(m_Length - m_Position);

            int bytesreaded = 0;
            while (bytesToRead > 0)
            {
                int remainingBytesInCurrentChunk = RemainingBytesInCurrentChunk;
                if (remainingBytesInCurrentChunk > bytesToRead)
                    remainingBytesInCurrentChunk = bytesToRead;
                Buffer.BlockCopy(CurrentChunk, PositionInChunk, buffer, offset, remainingBytesInCurrentChunk);
                m_Position += remainingBytesInCurrentChunk;
                offset += remainingBytesInCurrentChunk;
                bytesToRead -= remainingBytesInCurrentChunk;
                bytesreaded += remainingBytesInCurrentChunk;
            }
            return bytesreaded;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position += offset;
                    break;
                case SeekOrigin.End:
                    Position = Length + offset;
                    break;
            }
            return Position;
        }

        public override void SetLength(long value)
        {
            if (value > m_Length)
            {
                while (value > Capacity)
                {
                    var item = new byte[k_BufferSize];
                    m_Chunks.Add(item);
                }
            }
            else if (value < m_Length)
            {
                var decimalValue = Convert.ToDecimal(value);
                var valueToBeCompared = decimalValue % k_BufferSize == 0 ? Capacity : Capacity - k_BufferSize;
                while (value < valueToBeCompared && m_Chunks.Count > 2)
                {
                    byte[] lastChunk = m_Chunks.Last();
                    m_Chunks.Remove(lastChunk);
                }
            }
            m_Length = value;
            if (m_Position > m_Length - 1)
                m_Position = m_Length == 0 ? 0 : m_Length - 1;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            int bytesToWrite = count;
            while (bytesToWrite > 0)
            {
                int remainingBytesInCurrentChunk = RemainingBytesInCurrentChunk;
                if (remainingBytesInCurrentChunk > bytesToWrite)
                    remainingBytesInCurrentChunk = bytesToWrite;

                if (remainingBytesInCurrentChunk > 0)
                {
                    Buffer.BlockCopy(buffer, offset, CurrentChunk, PositionInChunk, remainingBytesInCurrentChunk);
                    offset += remainingBytesInCurrentChunk;
                    bytesToWrite -= remainingBytesInCurrentChunk;
                    m_Length += remainingBytesInCurrentChunk;
                    m_Position += remainingBytesInCurrentChunk;
                }

                if (Capacity == m_Position)
                    m_Chunks.Add(new byte[k_BufferSize]);
            }
        }
    }
}
#endif