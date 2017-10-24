namespace LuceneSearch
{
    using System.Text.RegularExpressions;
    using Lucene.Net.Index;
    using Lucene.Net.QueryParsers.Classic;
    using Lucene.Net.Search;
    using Lucene.Net.Util;

    internal static class QueryHelper
    {
        public static Query Wildcard(string name, string value) =>
            new WildcardQuery(new Term(name, new BytesRef(value)));
        public static Query Regexp(string name, string value) =>
            new RegexpQuery(new Term(name, new BytesRef(value)));

        public static Query Prefix(string name, string value) =>
            new PrefixQuery(new Term(name, new BytesRef(value)));

        public static bool IsRange(string query)
        {
            query = query.Trim();
            return (query.StartsWith("{") || query.StartsWith("[")) &&
                (query.EndsWith("}") || query.EndsWith("]")) &&
                query.ToUpperInvariant().Contains(" TO ");
        }

        public static Query Range(
            string name,
            string lowerTerm,
            bool includeLower = true,
            string upperTerm = null,
            bool includeUpper = true) =>
            new TermRangeQuery(name, new BytesRef(lowerTerm), new BytesRef(upperTerm), includeLower, includeUpper);

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