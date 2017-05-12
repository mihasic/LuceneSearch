namespace LuceneSearch
{
    using System;
    using System.Collections.Generic;
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
        private readonly Analyzer _analyzer;
        private readonly IDisposable _disposable;
        private readonly Dictionary<string, Func<string, IIndexableField>> _mapping;

        public Index(string directory, IEnumerable<Field> mapping)
        {
            _mapping = FieldMapper.Create(mapping);
            var analyzerMap = mapping.Where(x => !string.IsNullOrEmpty(x.SpecificAnalyzer)).ToDictionary(x => x.Name, x => x.SpecificAnalyzer);
            var t = PerFieldAnalyzer.Create("StandardAnalyzer", analyzerMap);
            _analyzer = t.Item1;
            _dir = DirectoryProvider.Create(directory);

            _disposable = new DelegateDisposable(() =>
            {
                t.Item2.Dispose();
                _dir.Dispose();
            });
        }

        public long LoadDocuments(IEnumerable<string> jsonDocuments, Func<string, IEnumerable<KeyValuePair<string, string>>> transform)
        {
            long count = 0;
            //using (var analyzer = new StopAnalyzer(LuceneVersion.LUCENE_48))
            //using (var analyzer = new Lucene.Net.Analysis.Core.KeywordAnalyzer())
            using (var ixw = new IndexWriter(_dir, new IndexWriterConfig(LuceneVersion.LUCENE_48, _analyzer)))
            {
                foreach(var json in jsonDocuments)
                {
                    var doc = transform(json);
                    var luceneDoc = new Document();
                    foreach (var f in doc.Where(f => _mapping.ContainsKey(f.Key)))
                    {
                        luceneDoc.Add(_mapping[f.Key](f.Value));
                    }
                    ixw.AddDocument(luceneDoc);
                    count++;
                }
            }
            return count;
        }

        public Tuple<int, IEnumerable<IReadOnlyCollection<KeyValuePair<string, string>>>> Search(
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
            using (var ir = DirectoryReader.Open(_dir))
            {
                return QuerySearch(ir, mainQuery, take, skip, sort, fieldsToLoad);
            }
        }

        public Tuple<int, IEnumerable<IReadOnlyCollection<KeyValuePair<string, string>>>> Search(
            string query,
            int take = 128,
            int skip = 0,
            string sort = null,
            ISet<string> fieldsToLoad = null)
        {
            using (var ir = DirectoryReader.Open(_dir))
            {
                var parser = new QueryParser(LuceneVersion.LUCENE_48, _mapping.Keys.First(), _analyzer);
                parser.AllowLeadingWildcard = true;
                parser.LowercaseExpandedTerms = false;
                var mainQuery = parser.Parse(query);
                return QuerySearch(ir, mainQuery, take, skip, sort, fieldsToLoad);
            }
        }

        private static Tuple<int, IEnumerable<IReadOnlyCollection<KeyValuePair<string, string>>>> QuerySearch(DirectoryReader ir, Query mainQuery, int take, int skip, string sort, ISet<string> fieldsToLoad)
        {
            var searcher = new IndexSearcher(ir);
            var res = sort == null ? searcher.Search(mainQuery, take + skip) : searcher.Search(mainQuery, take + skip, new Sort(new SortField(sort, SortFieldType.STRING)));
            var docs = new List<IReadOnlyCollection<KeyValuePair<string, string>>>(take);
            var scores = res.ScoreDocs;
            for (int i = skip; i < scores.Length; i++)
            {
                var doc = searcher.Doc(scores[i].Doc, fieldsToLoad);
                docs.Add(doc.Select(x => new KeyValuePair<string, string>(x.Name, x.GetStringValue())).ToArray());

            }
            return Tuple.Create<int, IEnumerable<IReadOnlyCollection<KeyValuePair<string, string>>>>(res.TotalHits, docs);
        }

        public IEnumerable<string> GetTerms(
            string termName,
            string from = null,
            int? take = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var ir = DirectoryReader.Open(_dir))
            {
                var terms = MultiFields.GetTerms(ir, termName);
                if (terms == null) yield break;
                var termsEnum = terms.GetIterator(null);
                BytesRef term;
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
        }

        public void Dispose() => _disposable?.Dispose();
    }
}