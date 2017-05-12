namespace LuceneSearch
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Newtonsoft.Json;

    public static class SearchMiddleware
    {
        private static readonly HashSet<string> s_skipped = new HashSet<string>(new[] { "take", "skip" },
            StringComparer.OrdinalIgnoreCase);

        public static Func<RequestDelegate, RequestDelegate> Create(Index index)
        {
            var searchPath = new PathString("/search");
            var termPath = new PathString("/term");
            return next => async ctx =>
            {
                PathString remaining;
                if (ctx.Request.Path.StartsWithSegments(searchPath, out remaining))
                {
                    if (!"GET".Equals(ctx.Request.Method))
                    {
                        ctx.Response.StatusCode = (int) HttpStatusCode.MethodNotAllowed;
                        ctx.Response.ContentType = "application/json";
                        ctx.Response.Headers["Allow"] = "GET";

                        await ctx.Response.WriteAsync("\"Method Not Allowed\"", ctx.RequestAborted).ConfigureAwait(false);
                        return;
                    }
                    PathString term;
                    if (!remaining.HasValue || remaining.Value == "/")
                    {
                        var q = ctx.Request.Query["q"].FirstOrDefault();
                        var result = string.IsNullOrEmpty(q)
                            ? SearchByFilter(index, ctx)
                            : SearchByQuery(index, ctx, Uri.UnescapeDataString(q));
                        await ReturnSearchResult(result, ctx);
                        return;
                    }

                    if (remaining.StartsWithSegments(termPath, out term))
                    {
                        await ReturnSearchTerm(index, term.Value.TrimStart('/'), ctx).ConfigureAwait(false);
                        return;
                    }
                }
                await next(ctx).ConfigureAwait(false);
            };
        }

        private static async Task ReturnSearchResult(Tuple<int, IEnumerable<IReadOnlyCollection<KeyValuePair<string, string>>>> result, HttpContext ctx)
        {
            var ct = ctx.RequestAborted;

            ctx.Response.StatusCode = (int)HttpStatusCode.OK;
            ctx.Response.ContentType = "application/json";

            using (var writer = new JsonTextWriter(new StreamWriter(ctx.Response.Body)))
            {
                await writer.WriteStartObjectAsync(ct).ConfigureAwait(false);

                await writer.WritePropertyNameAsync("totalCount", ct).ConfigureAwait(false);
                await writer.WriteValueAsync(result.Item1, ct).ConfigureAwait(false);

                await writer.WritePropertyNameAsync("results", ct).ConfigureAwait(false);
                await writer.WriteStartArrayAsync(ct).ConfigureAwait(false);
                foreach (var document in result.Item2)
                {
                    if (ct.IsCancellationRequested) return;
                    var obj = document.GroupBy(x => x.Key);
                    await writer.WriteStartObjectAsync(ct).ConfigureAwait(false);
                    foreach (var p in obj)
                    {
                        await writer.WritePropertyNameAsync(p.Key, ct).ConfigureAwait(false);
                        var values = p.ToArray();
                        if (values.Length == 0)
                        {
                            await writer.WriteNullAsync(ct).ConfigureAwait(false);
                        }
                        else if (values.Length == 1)
                        {
                            await writer.WriteValueAsync(values[0].Value, ct).ConfigureAwait(false);
                        }
                        else
                        {
                            await writer.WriteStartArrayAsync(ct).ConfigureAwait(false);
                            foreach (var v in values)
                            {
                                await writer.WriteValueAsync(v.Value, ct).ConfigureAwait(false);
                            }
                            await writer.WriteEndArrayAsync(ct).ConfigureAwait(false);
                        }
                    }
                    await writer.WriteEndObjectAsync(ct).ConfigureAwait(false);
                }
                await writer.WriteEndArrayAsync(ct).ConfigureAwait(false);

                await writer.WriteEndObjectAsync(ct).ConfigureAwait(false);
            }
        }

        private static Tuple<int, IEnumerable<IReadOnlyCollection<KeyValuePair<string, string>>>> SearchByFilter(
            Index index, HttpContext ctx)
        {
            var filters = ctx.Request.Query.Where(x => !s_skipped.Contains(x.Key))
                .ToDictionary(x => x.Key, x => (IReadOnlyCollection<string>)x.Value.Select(Uri.UnescapeDataString).ToArray());
            var skip = ctx.Request.Query["skip"].FirstOrDefault().ParseInt() ?? 0;
            var take = ctx.Request.Query["take"].FirstOrDefault().ParseInt() ?? 25;

            var result = index.Search(filters, take, skip);
            return result;
        }

        private static Tuple<int, IEnumerable<IReadOnlyCollection<KeyValuePair<string, string>>>> SearchByQuery(
            Index index, HttpContext ctx, string query)
        {
            var skip = ctx.Request.Query["skip"].FirstOrDefault().ParseInt() ?? 0;
            var take = ctx.Request.Query["take"].FirstOrDefault().ParseInt() ?? 25;

            var result = index.Search(query, take, skip);
            return result;
        }

        private static async Task ReturnSearchTerm(Index index, string term, HttpContext ctx)
        {
            var result = index.GetTerms(
                term,
                ctx.Request.Query["from"].FirstOrDefault(),
                ctx.Request.Query["take"].FirstOrDefault().ParseInt() ?? 100,
                cancellationToken: ctx.RequestAborted);
            ctx.Response.StatusCode = (int) HttpStatusCode.OK;
            ctx.Response.ContentType = "application/json";

            await ctx.Response.WriteAsync(JsonConvert.SerializeObject(result),
                ctx.RequestAborted).ConfigureAwait(false);
        }
    }
}