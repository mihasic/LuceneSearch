namespace LuceneSearch
{
    using Lucene.Net.Search;

    public class BooleanIndexQuery : IndexQuery
    {
        private readonly BooleanQuery _query;

        public BooleanIndexQuery() : base(new BooleanQuery()) =>
            _query = (BooleanQuery) Query;

        public BooleanIndexQuery Add(IndexQuery query, bool include = true)
        {
            if (!include && _query.Clauses.Count == 0)
                _query.Add(new MatchAllDocsQuery(), Occur.MUST);
            _query.Add(query.Query, include ? Occur.MUST : Occur.MUST_NOT);
            return this;
        }

        public BooleanIndexQuery And(IndexQuery query) =>
            Add(query, true);

        public BooleanIndexQuery AndNot(IndexQuery query) =>
            Add(query, false);
    }
}