namespace LuceneSearch
{
    using System.Linq;
    using Lucene.Net.Search;

    public class IndexQuery
    {
        internal readonly Query Query;

        protected internal IndexQuery(Query query) =>
            Query = query;

        public override string ToString() =>
            Query.ToString();

        public static IndexQuery All =>
            new IndexQuery(new MatchAllDocsQuery());
        public static IndexQuery Term(string name, string value) =>
            new IndexQuery(QueryHelper.Term(name, value));

        public static IndexQuery Wildcard(string name, string value) =>
            new IndexQuery(QueryHelper.Wildcard(name, value));

        public static IndexQuery Regexp(string name, string value) =>
            new IndexQuery(QueryHelper.Regexp(name, value));

        public static IndexQuery Prefix(string name, string value) =>
            new IndexQuery(QueryHelper.Prefix(name, value));

        public static IndexQuery Range(
            string name,
            string lowerTerm,
            bool includeLower = true,
            string upperTerm = null,
            bool includeUpper = true) =>
            new IndexQuery(QueryHelper.Range(name, lowerTerm, includeLower, upperTerm, includeUpper));

        public static IndexQuery And(params IndexQuery[] queries) =>
            new IndexQuery(QueryHelper.BooleanAnd(queries.Select(x => x.Query).ToArray()));

        public static IndexQuery Or(params IndexQuery[] queries) =>
            new IndexQuery(QueryHelper.BooleanOr(queries.Select(x => x.Query).ToArray()));
    }
}