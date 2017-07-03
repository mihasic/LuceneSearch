namespace LuceneSearch
{
    using Lucene.Net.Index;
    using Lucene.Net.Search;
    using Lucene.Net.Util;

    internal static class QueryHelper
    {
        public static Query Wildcard(string name, string value) =>
            new WildcardQuery(new Term(name, new BytesRef(value)));

        public static Query Term(string name, string value) =>
            new TermQuery(new Term(name, new BytesRef(value)));

        public static Query BooleanAnd(params Query[] queries)
        {
            if (queries.Length == 1) return queries[0];
            var query = new BooleanQuery();
            var occur = Occur.MUST;
            foreach (var q in queries)
            {
                query.Add(q, occur);
            }
            return query;
        }

        public static Query BooleanOr(params Query[] queries)
        {
            if (queries.Length == 1) return queries[0];
            var query = new BooleanQuery();
            var occur = Occur.SHOULD;
            foreach (var q in queries)
            {
                query.Add(q, occur);
            }
            return query;
        }
    }
}