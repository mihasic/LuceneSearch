namespace System.Net.Http
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    
    internal class ResponseStream : Stream
    {
        private bool _disposed;
        private bool _aborted;
        private Exception _abortException;
        private readonly ConcurrentQueue<byte[]> _bufferedData;
        private ArraySegment<byte> _topBuffer;
        private readonly SemaphoreSlim _readLock;
        private readonly SemaphoreSlim _writeLock;
        private TaskCompletionSource<object> _readWaitingForData;

        private readonly Action _onFirstWrite;
        private bool _firstWrite;

        internal ResponseStream(Action onFirstWrite)
        {
            _onFirstWrite = onFirstWrite;
            _firstWrite = true;

            _readLock = new SemaphoreSlim(1, 1);
            _writeLock = new SemaphoreSlim(1, 1);
            _bufferedData = new ConcurrentQueue<byte[]>();
            _readWaitingForData = new TaskCompletionSource<object>();
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        #region NotSupported

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        #endregion NotSupported

        public override void Flush()
        {
            CheckDisposed();

            _writeLock.Wait();
            try
            {
                FirstWrite();
            }
            finally
            {
                _writeLock.Release();
            }

            // TODO: Wait for data to drain?
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                var tcs = new TaskCompletionSource<object>();
                tcs.TrySetCanceled();
                return tcs.Task;
            }

            Flush();

            // TODO: Wait for data to drain?

            return Task.FromResult<object>(null);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            VerifyBuffer(buffer, offset, count, allowEmpty: false);
            _readLock.Wait();
            try
            {
                int totalRead = 0;
                do
                {
                    // Don't drain buffered data when signaling an abort.
                    CheckAborted();
                    if (_topBuffer.Count <= 0)
                    {
                        byte[] topBuffer;
                        while (!_bufferedData.TryDequeue(out topBuffer))
                        {
                            if (_disposed)
                            {
                                CheckAborted();
                                // Graceful close
                                return totalRead;
                            }
                            WaitForDataAsync().Wait();
                        }
                        _topBuffer = new ArraySegment<byte>(topBuffer);
                    }
                    int actualCount = Math.Min(count, _topBuffer.Count);
                    Buffer.BlockCopy(_topBuffer.Array, _topBuffer.Offset, buffer, offset, actualCount);
                    _topBuffer = new ArraySegment<byte>(_topBuffer.Array,
                        _topBuffer.Offset + actualCount,
                        _topBuffer.Count - actualCount);
                    totalRead += actualCount;
                    offset += actualCount;
                    count -= actualCount;
                }
                while (count > 0 && (_topBuffer.Count > 0 || _bufferedData.Count > 0));
                // Keep reading while there is more data available and we have more space to put it in.
                return totalRead;
            }
            finally
            {
                _readLock.Release();
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            VerifyBuffer(buffer, offset, count, allowEmpty: false);
            CancellationTokenRegistration registration = cancellationToken.Register(Abort);
            await _readLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                int totalRead = 0;
                do
                {
                    // Don't drained buffered data on abort.
                    CheckAborted();
                    if (_topBuffer.Count <= 0)
                    {
                        byte[] topBuffer;
                        while (!_bufferedData.TryDequeue(out topBuffer))
                        {
                            if (_disposed)
                            {
                                CheckAborted();
                                // Graceful close
                                return totalRead;
                            }
                            await WaitForDataAsync().ConfigureAwait(false);
                        }
                        _topBuffer = new ArraySegment<byte>(topBuffer);
                    }
                    int actualCount = Math.Min(count, _topBuffer.Count);
                    Buffer.BlockCopy(_topBuffer.Array, _topBuffer.Offset, buffer, offset, actualCount);
                    _topBuffer = new ArraySegment<byte>(_topBuffer.Array,
                        _topBuffer.Offset + actualCount,
                        _topBuffer.Count - actualCount);
                    totalRead += actualCount;
                    offset += actualCount;
                    count -= actualCount;
                }
                while (count > 0 && (_topBuffer.Count > 0 || _bufferedData.Count > 0));
                // Keep reading while there is more data available and we have more space to put it in.
                return totalRead;
            }
            finally
            {
                registration.Dispose();
                _readLock.Release();
            }
        }

        // Called under write-lock.
        private void FirstWrite()
        {
            if (_firstWrite)
            {
                _firstWrite = false;
                _onFirstWrite();
            }
        }

        // Write with count 0 will still trigger OnFirstWrite
        public override void Write(byte[] buffer, int offset, int count)
        {
            VerifyBuffer(buffer, offset, count, allowEmpty: true);
            CheckDisposed();

            _writeLock.Wait();
            try
            {
                FirstWrite();
                if (count == 0)
                {
                    return;
                }
                // Copies are necessary because we don't know what the caller is going to do with the buffer afterwards.
                var internalBuffer = new byte[count];
                Buffer.BlockCopy(buffer, offset, internalBuffer, 0, count);
                _bufferedData.Enqueue(internalBuffer);

                SignalDataAvailable();
            }
            finally
            {
                _writeLock.Release();
            }
        }

#if NET45
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            Write(buffer, offset, count);
            var tcs = new TaskCompletionSource<object>(state);
            tcs.TrySetResult(null);
            IAsyncResult result = tcs.Task;
            callback?.Invoke(result);
            return result;
        }

        public override void EndWrite(IAsyncResult asyncResult)
        { }
#endif

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            VerifyBuffer(buffer, offset, count, allowEmpty: true);
            if (cancellationToken.IsCancellationRequested)
            {
                var tcs = new TaskCompletionSource<object>();
                tcs.TrySetCanceled();
                return tcs.Task;
            }

            Write(buffer, offset, count);
            return Task.FromResult<object>(null);
        }

        private static void VerifyBuffer(byte[] buffer, int offset, int count, bool allowEmpty)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            if (offset < 0 || offset > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset, string.Empty);
            }
            if (count < 0 || count > buffer.Length - offset
                || (!allowEmpty && count == 0))
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, string.Empty);
            }
        }

        private void SignalDataAvailable()
        {
            // Dispatch, as TrySetResult will synchronously execute the waiters callback and block our Write.
            Task.Factory.StartNew(() => _readWaitingForData.TrySetResult(null));
        }

        private Task WaitForDataAsync()
        {
            _readWaitingForData = new TaskCompletionSource<object>();

            if (!_bufferedData.IsEmpty || _disposed)
            {
                // Race, data could have arrived before we created the TCS.
                _readWaitingForData.TrySetResult(null);
            }

            return _readWaitingForData.Task;
        }

        private void Abort()
        {
            Abort(new OperationCanceledException());
        }

        internal void Abort(Exception innerException)
        {
            _aborted = true;
            _abortException = innerException;
            Dispose();
        }

        private void CheckAborted()
        {
            if (_aborted)
            {
                throw new IOException(string.Empty, _abortException);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Throw for further writes, but not reads.  Allow reads to drain the buffered data and then return 0 for further reads.
                _disposed = true;
                _readWaitingForData.TrySetResult(null);
            }

            base.Dispose(disposing);
        }

        private void CheckDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }
    }
}