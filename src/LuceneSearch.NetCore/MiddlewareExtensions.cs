using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;

namespace LuceneSearch
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseSearchMiddleware(this IApplicationBuilder builder, Index luceneIndex) =>
            builder.Use(SearchMiddleware.Create(luceneIndex));
        public static IApplicationBuilder UseDocumentMiddleware(this IApplicationBuilder builder,
            Index luceneIndex,
            string identityField,
            Func<string, IEnumerable<KeyValuePair<string, string>>> transform) =>
                builder.Use(DocumentMiddleware.Create(luceneIndex, identityField, transform));
    }
}