namespace LuceneSearch
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Owin;
    using Newtonsoft.Json;

    using AppFunc = System.Func<System.Collections.Generic.IDictionary<string, object>, System.Threading.Tasks.Task>;

    public static class SearchMiddleware
    {
        private static readonly HashSet<string> s_skipped = new HashSet<string>(new[] { "take", "skip", "q", "sort", "i" },
            StringComparer.OrdinalIgnoreCase);

        public static Func<AppFunc, AppFunc> Create(Index index)
        {
            var searchPath = new PathString("/search");
            var termPath = new PathString("/term");
            return next => async env =>
            {
                var ctx = new OwinContext(env);
                PathString remaining;
                if (ctx.Request.Path.StartsWithSegments(searchPath, out remaining))
                {
                    if (!"GET".Equals(ctx.Request.Method))
                    {
                        ctx.Response.StatusCode = (int) HttpStatusCode.MethodNotAllowed;
                        ctx.Response.ContentType = "application/json";
                        ctx.Response.Headers["Allow"] = "GET";

                        await ctx.Response.WriteAsync("\"Method Not Allowed\"", ctx.Request.CallCancelled).ConfigureAwait(false);
                        return;
                    }
                    PathString term;
                    if (!remaining.HasValue || remaining.Value == "/")
                    {
                        var q = ctx.Request.Query.GetValues("q").FirstOrDefault();
                        var result = string.IsNullOrEmpty(q)
                            ? SearchByQuery(index, ctx, null)
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
                await next(env).ConfigureAwait(false);
            };
        }

        private static async Task ReturnSearchResult((int total, TimeSpan elapsed, IEnumerable<IReadOnlyCollection<KeyValuePair<string, string>>> docs) result, OwinContext ctx)
        {
            var ct = ctx.Request.CallCancelled;

            ctx.Response.StatusCode = (int)HttpStatusCode.OK;
            ctx.Response.ContentType = "application/json";

            using (var writer = new JsonTextWriter(new StreamWriter(ctx.Response.Body)))
            {
                await writer.WriteStartObjectAsync(ct).ConfigureAwait(false);

                await writer.WritePropertyNameAsync("totalCount", ct).ConfigureAwait(false);
                await writer.WriteValueAsync(result.total, ct).ConfigureAwait(false);

                await writer.WritePropertyNameAsync("elapsed", ct).ConfigureAwait(false);
                await writer.WriteValueAsync(result.elapsed, ct).ConfigureAwait(false);

                await writer.WritePropertyNameAsync("results", ct).ConfigureAwait(false);
                await writer.WriteStartArrayAsync(ct).ConfigureAwait(false);
                foreach (var document in result.docs)
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

        private static (int total, TimeSpan elapsed, IEnumerable<IReadOnlyCollection<KeyValuePair<string, string>>> docs) SearchByQuery(
            Index index, OwinContext ctx, string query)
        {
            var filters = ctx.Request.Query.Where(x => !s_skipped.Contains(x.Key))
                .ToDictionary(x => x.Key, x => (IReadOnlyCollection<string>)x.Value.Select(Uri.UnescapeDataString).ToArray());
            var skip = ctx.Request.Query["skip"].ParseInt() ?? 0;
            var take = ctx.Request.Query["take"].ParseInt() ?? 25;
            var sort = ctx.Request.Query["sort"];
            var include = new HashSet<string>(ctx.Request.Query.GetValues("i")
                .SelectMany(x => x.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)));
            if (!include.Any()) include = null;
            var result = string.IsNullOrWhiteSpace(query)
                ? index.Search(filters, take, skip, sort, false, include)
                : index.Search(query, filters, take, skip, sort, false, include);
            return result;
        }

        private static async Task ReturnSearchTerm(Index index, string term, OwinContext ctx)
        {
            var result = index.GetTerms(
                term,
                ctx.Request.Query["p"].ParseBool() ?? false,
                ctx.Request.Query["from"],
                ctx.Request.Query["take"].ParseInt() ?? 100,
                cancellationToken: ctx.Request.CallCancelled);
            ctx.Response.StatusCode = (int) HttpStatusCode.OK;
            ctx.Response.ContentType = "application/json";

            await ctx.Response.WriteAsync(JsonConvert.SerializeObject(result),
                ctx.Request.CallCancelled).ConfigureAwait(false);
        }
    }
}