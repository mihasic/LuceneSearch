namespace LuceneSearch
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using Lucene.Net.Analysis;
    using Lucene.Net.Documents;
    using Lucene.Net.Index;
    using Lucene.Net.QueryParsers.Classic;
    using Lucene.Net.Search;
    using Lucene.Net.Store;
    using Lucene.Net.Util;

    public partial class Index : IDisposable
    {
        private readonly Directory _dir;
        private readonly Lazy<SearcherManager> _sm;
        private readonly Analyzer _analyzer;
        private readonly IDisposable _disposable;
        private readonly Dictionary<string, Func<string, IIndexableField>> _mapping;
        private readonly HashSet<string> _termFields;
        private IndexWriter _writer;

        private IndexWriter Writer =>
            _writer = _writer ?? new IndexWriter(_dir, new IndexWriterConfig(LuceneVersion.LUCENE_48, _analyzer)
            {
                IndexDeletionPolicy = new SnapshotDeletionPolicy(new KeepOnlyLastCommitDeletionPolicy())
            });

        public Index(string directory, IEnumerable<Field> mapping)
        {
            _mapping = FieldMapper.Create(mapping);
            _termFields = FieldMapper.GetTermFields(mapping);
            var analyzerMap = mapping
                .Where(x => !string.IsNullOrEmpty(x.SpecificAnalyzer))
                .ToDictionary(x => x.Name, x => x.SpecificAnalyzer);
            IDisposable d;
            (_analyzer, d) = PerFieldAnalyzer.Create("StandardAnalyzer", analyzerMap);
            _dir = DirectoryProvider.Create(directory);
            _sm = new Lazy<SearcherManager>(() => new SearcherManager(_dir, null), true);

            _disposable = new DelegateDisposable(() =>
            {
                d.Dispose();
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
            Commit();
            return count;
        }

        public int Count
        {
            get
            {
                var searcher = _sm.Value.Acquire();
                try
                {
                    return searcher.IndexReader.NumDocs;
                }
                finally
                {
                    _sm.Value.Release(searcher);
                }
            }
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
            if (commit) Commit();
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
            if (commit) Commit();
        }

        public void Commit()
        {
            Writer.Commit();
            if (_sm.IsValueCreated) _sm.Value.MaybeRefreshBlocking();
        }

        public SearchResult Search(
            IDictionary<string, IReadOnlyCollection<string>> filters,
            int take = 128,
            int skip = 0,
            string sort = null,
            bool reverseSort = false,
            ISet<string> fieldsToLoad = null) =>
            QuerySearch(QueryHelper.BooleanAnd(Parse(filters).ToArray()), take, skip, sort, reverseSort, fieldsToLoad);

        public SearchResult Search(
            IndexQuery query,
            int take = 128,
            int skip = 0,
            string sort = null,
            bool reverseSort = false,
            ISet<string> fieldsToLoad = null) =>
            QuerySearch(query.Query, take, skip, sort, reverseSort, fieldsToLoad);

        public SearchResult Search(
            string query,
            IDictionary<string, IReadOnlyCollection<string>> filters,
            int take = 128,
            int skip = 0,
            string sort = null,
            bool reverseSort = false,
            ISet<string> fieldsToLoad = null)
        {
            var queries = Parse(filters);
            var mainQuery = QueryHelper.BooleanAnd(queries.Concat(new[] { Parse(query, null) }).ToArray());
            return QuerySearch(mainQuery, take, skip, sort, reverseSort, fieldsToLoad);
        }

        public IndexQuery Parse(string query) =>
            new IndexQuery(Parse(query, null));

        private Query Parse(string query, string fieldName = null)
        {
            var analyzer = _analyzer;
            if (fieldName != null &&
                _mapping.ContainsKey(fieldName) &&
                _termFields.Contains(fieldName) &&
                !QueryHelper.IsRange(query))
            {
                return QueryHelper.Wildcard(fieldName, query);
            }
            var parser = new QueryParser(
                LuceneVersion.LUCENE_48,
                fieldName ?? _mapping.Keys.First(),
                _analyzer);
            parser.AllowLeadingWildcard = true;
            parser.LowercaseExpandedTerms = false;
            return parser.Parse(query);
        }

        private IEnumerable<Query> Parse(IDictionary<string, IReadOnlyCollection<string>> filters) =>
                from filter in filters
                let valueQueries = filter.Value.Select(v => Parse(v, fieldName: filter.Key)).ToArray()
                select QueryHelper.BooleanOr(valueQueries);

        public SearchResult Search(
            string query,
            int take = 128,
            int skip = 0,
            string sort = null,
            bool reverseSort = false,
            ISet<string> fieldsToLoad = null) =>
            QuerySearch(Parse(query, null), take, skip, sort, reverseSort, fieldsToLoad);

        private SearchResult QuerySearch(
            Query mainQuery,
            int take,
            int skip,
            string sort,
            bool reverseSort,
            ISet<string> fieldsToLoad)
        {
            var sw = Stopwatch.StartNew();
            var searcher = _sm.Value.Acquire();
            try
            {
                var res = sort == null
                    ? searcher.Search(mainQuery, take + skip)
                    : searcher.Search(mainQuery, take + skip, new Sort(new SortField(sort, SortFieldType.STRING, reverseSort)));
                var docs = new List<IReadOnlyCollection<KeyValuePair<string, string>>>(take);
                var scores = res.ScoreDocs;
                for (int i = skip; i < scores.Length; i++)
                {
                    var doc = searcher.Doc(scores[i].Doc, fieldsToLoad);
                    docs.Add(doc.Select(x => new KeyValuePair<string, string>(x.Name, x.GetStringValue())).ToArray());
                }
                return new SearchResult(res.TotalHits, sw.Elapsed, docs);
            }
            finally
            {
                _sm.Value.Release(searcher);
            }
        }

        public IEnumerable<string> GetTerms(
            string termName,
            bool prefix = false,
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
                    termsEnum = prefix
                        ? (TermsEnum) new PrefixTermsEnum(termsEnum, new BytesRef(from))
                        : new TermRangeTermsEnum(termsEnum, new BytesRef(from), null, true, false);
                }
                int count = 0;
                while ((term = termsEnum.Next()) != null)
                {
                    var stringTerm = term.Utf8ToString();
                    yield return stringTerm;
                    if (take.HasValue && ++count == take.Value) yield break;
                }
            }
            finally
            {
                _sm.Value.Release(searcher);
            }
        }

        public void Copy(string dest)
        {
            var destDir = DirectoryProvider.Create(dest);
            var policy = (SnapshotDeletionPolicy) Writer.Config.IndexDeletionPolicy;
            var commit = policy.Snapshot();
            try
            {
                foreach (var file in commit.FileNames)
                {
                    _dir.Copy(destDir, file, file, IOContext.READ_ONCE);
                }
            }
            finally
            {
                policy.Release(commit);
                Writer.DeleteUnusedFiles();
            }
        }

        public void Dispose() => _disposable?.Dispose();
    }
}