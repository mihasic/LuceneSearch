namespace LuceneSearch
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using Lucene.Net.Analysis;
    using Lucene.Net.Documents;
    using Lucene.Net.Index;
    using Lucene.Net.QueryParsers.Classic;
    using Lucene.Net.Search;
    using Lucene.Net.Store;
    using Lucene.Net.Util;

    public class Index : IDisposable
    {
        private readonly Directory _dir;
        private readonly Lazy<SearcherManager> _sm;
        private readonly Analyzer _analyzer;
        private readonly IDisposable _disposable;
        private readonly Dictionary<string, Func<string, IIndexableField>> _mapping;

        private IndexWriter _writer;

        private IndexWriter Writer =>
            _writer = _writer ?? new IndexWriter(_dir, new IndexWriterConfig(LuceneVersion.LUCENE_48, _analyzer));

        public Index(string directory, IEnumerable<Field> mapping)
        {
            _mapping = FieldMapper.Create(mapping);
            var analyzerMap = mapping
                .Where(x => !string.IsNullOrEmpty(x.SpecificAnalyzer))
                .ToDictionary(x => x.Name, x => x.SpecificAnalyzer);
            var t = PerFieldAnalyzer.Create("StandardAnalyzer", analyzerMap);
            _analyzer = t.Item1;
            _dir = DirectoryProvider.Create(directory);
            _sm = new Lazy<SearcherManager>(() => new SearcherManager(_dir, null), true);

            _disposable = new DelegateDisposable(() =>
            {
                t.Item2.Dispose();
                _writer?.Dispose();
                _writer = null;
                if (_sm.IsValueCreated) _sm.Value.Dispose();
                _dir.Dispose();
            });
        }

        public long LoadDocuments(IEnumerable<IEnumerable<KeyValuePair<string, string>>> docs)
        {
            long count = 0;
            var ixw = Writer;
            {
                foreach(var doc in docs)
                {
                    var luceneDoc = new Document();
                    foreach (var f in doc.Where(f => _mapping.ContainsKey(f.Key)))
                    {
                        luceneDoc.Add(_mapping[f.Key](f.Value));
                    }
                    ixw.AddDocument(luceneDoc);
                    count++;
                }
            }
            ixw.Commit();
            if (_sm.IsValueCreated) _sm.Value.MaybeRefreshBlocking();
            return count;
        }

        public Tuple<int, TimeSpan, IEnumerable<IReadOnlyCollection<KeyValuePair<string, string>>>> Search(
            IDictionary<string, IReadOnlyCollection<string>> filters,
            int take = 128,
            int skip = 0,
            string sort = null,
            ISet<string> fieldsToLoad = null)
        {
            var queries =
                from filter in filters
                let valueQueries = filter.Value.Select(v => QueryHelper.Wildcard(filter.Key, v)).ToArray()
                select QueryHelper.BooleanOr(valueQueries);
            var mainQuery = QueryHelper.BooleanAnd(queries.ToArray());
            return QuerySearch(mainQuery, take, skip, sort, fieldsToLoad);
        }

        public IReadOnlyCollection<KeyValuePair<string, string>> GetByTerm(string name, string value)
        {
            var searcher = _sm.Value.Acquire();
            try
            {
                var scores = searcher.Search(new TermQuery(new Term(name, new BytesRef(value))), 1).ScoreDocs;
                if (scores.Length > 0)
                {
                    var doc = searcher.Doc(scores[0].Doc);
                    return doc.Select(x => new KeyValuePair<string, string>(x.Name, x.GetStringValue())).ToArray();
                }
                return null;
            }
            finally
            {
                _sm.Value.Release(searcher);
            }
        }
        public void DeleteByTerm(string name, string value, bool commit = true)
        {
            var ixw = Writer;
            ixw.DeleteDocuments(new Term(name, new BytesRef(value)));
            if (commit) ixw.Commit();
        }
        public void UpdateByTerm(string name, string value, IEnumerable<KeyValuePair<string, string>> doc, bool commit = true)
        {
            var ixw = Writer;
            var luceneDoc = new Document();
            foreach (var f in doc.Where(f => _mapping.ContainsKey(f.Key)))
            {
                luceneDoc.Add(_mapping[f.Key](f.Value));
            }
            ixw.UpdateDocument(new Term(name, new BytesRef(value)), luceneDoc);
            if (commit) ixw.Commit();
        }

        public void Commit() => Writer.Commit();

        public Tuple<int, TimeSpan, IEnumerable<IReadOnlyCollection<KeyValuePair<string, string>>>> Search(
            string query,
            int take = 128,
            int skip = 0,
            string sort = null,
            ISet<string> fieldsToLoad = null)
        {
            var parser = new QueryParser(LuceneVersion.LUCENE_48, _mapping.Keys.First(), _analyzer);
            parser.AllowLeadingWildcard = true;
            parser.LowercaseExpandedTerms = false;
            var mainQuery = parser.Parse(query);
            return QuerySearch(mainQuery, take, skip, sort, fieldsToLoad);
        }

        private Tuple<int, TimeSpan, IEnumerable<IReadOnlyCollection<KeyValuePair<string, string>>>> QuerySearch(Query mainQuery, int take, int skip, string sort, ISet<string> fieldsToLoad)
        {
            var sw = Stopwatch.StartNew();
            var searcher = _sm.Value.Acquire();
            try
            {
                var res = sort == null ? searcher.Search(mainQuery, take + skip) : searcher.Search(mainQuery, take + skip, new Sort(new SortField(sort, SortFieldType.STRING)));
                var docs = new List<IReadOnlyCollection<KeyValuePair<string, string>>>(take);
                var scores = res.ScoreDocs;
                for (int i = skip; i < scores.Length; i++)
                {
                    var doc = searcher.Doc(scores[i].Doc, fieldsToLoad);
                    docs.Add(doc.Select(x => new KeyValuePair<string, string>(x.Name, x.GetStringValue())).ToArray());
                }
                return Tuple.Create<int, TimeSpan, IEnumerable<IReadOnlyCollection<KeyValuePair<string, string>>>>(res.TotalHits, sw.Elapsed, docs);
            }
            finally
            {
                _sm.Value.Release(searcher);
            }
        }

        public IEnumerable<string> GetTerms(
            string termName,
            string from = null,
            int? take = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var searcher = _sm.Value.Acquire();
            try
            {
                var ir = searcher.IndexReader;
                var terms = MultiFields.GetTerms(ir, termName);
                if (terms == null) yield break;
                var termsEnum = terms.GetIterator(null);
                BytesRef term;
                if (from != null) {
                    termsEnum = new PrefixTermsEnum(termsEnum, new BytesRef(from));
                }
                bool skip = from != null;
                int count = 0;
                while ((term = termsEnum.Next()) != null)
                {
                    var stringTerm = term.Utf8ToString();
                    if (skip)
                    {
                        skip = string.CompareOrdinal(from, stringTerm) > 0;
                        continue;
                    }
                    yield return stringTerm;
                    if (take.HasValue && ++count == take.Value) yield break;
                }
            }
            finally
            {
                _sm.Value.Release(searcher);
            }
        }

        public void Dispose() => _disposable?.Dispose();
    }
}