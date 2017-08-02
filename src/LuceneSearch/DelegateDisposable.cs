namespace LuceneSearch
{
    using System;

    internal class DelegateDisposable : IDisposable
    {
        private Action _dispose;

        public DelegateDisposable(Action dispose) =>
            _dispose = dispose;

        public void Dispose() => _dispose?.Invoke();
    }
}