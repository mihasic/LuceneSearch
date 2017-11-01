namespace LuceneSearch
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    public class SearchResult : IEnumerable<IReadOnlyCollection<KeyValuePair<string, string>>>
    {
        public readonly long Total;
        public readonly TimeSpan Elapsed;
        public readonly IEnumerable<IReadOnlyCollection<KeyValuePair<string, string>>> Docs;

        public SearchResult(
            long total,
            TimeSpan elapsed,
            IEnumerable<IReadOnlyCollection<KeyValuePair<string, string>>> docs)
        {
            Total = total;
            Elapsed = elapsed;
            Docs = docs;
        }

        public IEnumerator<IReadOnlyCollection<KeyValuePair<string, string>>> GetEnumerator() =>
            Docs.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            Docs.GetEnumerator();

        public void Deconstruct(
            out long total,
            out TimeSpan elapsed,
            out IEnumerable<IReadOnlyCollection<KeyValuePair<string, string>>> docs)
        {
            total = Total;
            elapsed = Elapsed;
            docs = Docs;
        }
    }
}