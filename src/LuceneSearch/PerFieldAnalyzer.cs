namespace LuceneSearch
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Lucene.Net.Analysis;
    using Lucene.Net.Analysis.Miscellaneous;
    using Lucene.Net.Util;

    internal static class PerFieldAnalyzer
    {
        private static Dictionary<string, Func<Analyzer>> s_analyzerMap = new Dictionary<string, Func<Analyzer>>
        {
            { "StandardAnalyzer", () => new Lucene.Net.Analysis.Standard.StandardAnalyzer(LuceneVersion.LUCENE_48) },
            { "StopAnalyzer", () => new Lucene.Net.Analysis.Core.StopAnalyzer(LuceneVersion.LUCENE_48) },
            { "KeywordAnalyzer", () => new Lucene.Net.Analysis.Core.KeywordAnalyzer() },
            { "SimpleAnalyzer", () => new Lucene.Net.Analysis.Core.SimpleAnalyzer(LuceneVersion.LUCENE_48) },
            { "WhitespaceAnalyzer", () => new Lucene.Net.Analysis.Core.WhitespaceAnalyzer(LuceneVersion.LUCENE_48) },
            { "ClassicAnalyzer", () => new Lucene.Net.Analysis.Standard.ClassicAnalyzer(LuceneVersion.LUCENE_48) },
        };

        public static (Analyzer analyzer, IDisposable disposable) Create(
            string defaultAnalyzer,
            IDictionary<string, string> fieldAnalyzers)
        {
            var analyzers = new ConcurrentDictionary<string, Analyzer>();
            Func<string, Analyzer> getAnalyzer = name => analyzers.GetOrAdd(name, n => s_analyzerMap[n]());
            var perFieldAnalyzers = fieldAnalyzers.ToDictionary(x => x.Key, x => getAnalyzer(x.Value));
            Analyzer analyzer = new PerFieldAnalyzerWrapper(
                getAnalyzer(defaultAnalyzer),
                perFieldAnalyzers);
            IDisposable disposable = new DelegateDisposable(() =>
            {
                analyzer.Dispose();
                foreach (var a in analyzers.Values)
                    a.Dispose();
            });
            return (analyzer, disposable);
        }
     }
}
