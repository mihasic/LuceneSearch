using Microsoft.AspNetCore.Builder;

namespace LuceneSearch
{
    public static class SearchMiddlewareExtensions
    {
        public static IApplicationBuilder UseSearchMiddleware(this IApplicationBuilder builder, Index luceneIndex) =>
            builder.Use(SearchMiddleware.Create(luceneIndex));
    }
}