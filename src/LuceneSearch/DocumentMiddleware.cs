
namespace LuceneSearch
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Linq;
    using Microsoft.AspNetCore.Http;
    using Newtonsoft.Json;

    public static class DocumentMiddleware
    {
        public static Func<RequestDelegate, RequestDelegate> Create(
            Index index,
            string identityField,
            Func<string, IEnumerable<KeyValuePair<string, string>>> transform)
        {
            return next => async ctx =>
            {
                if (ctx.Request.Path.HasValue)
                {
                    var id = ctx.Request.Path.Value.Substring(1);
                    if ("GET".Equals(ctx.Request.Method, StringComparison.OrdinalIgnoreCase))
                    {
                        var obj = index.GetByTerm(identityField, id);
                        var ct = ctx.RequestAborted;

                        ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                        ctx.Response.ContentType = "application/json";

                        using (var writer = new JsonTextWriter(new StreamWriter(ctx.Response.Body)))
                        {
                            await writer.WriteStartObjectAsync(ct).ConfigureAwait(false);
                            foreach (var p in obj.GroupBy(x => x.Key))
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
                    }
                    else if ("PUT".Equals(ctx.Request.Method, StringComparison.OrdinalIgnoreCase))
                    {
                        var stream = new StreamReader(ctx.Request.Body);
                        var json = await stream.ReadToEndAsync().ConfigureAwait(false);
                        index.UpdateByTerm(identityField, id, transform(json));
                        ctx.Response.StatusCode = (int) HttpStatusCode.OK;
                    }
                    else if ("POST".Equals(ctx.Request.Method, StringComparison.OrdinalIgnoreCase))
                    {
                        if (index.GetByTerm(identityField, id) != null)
                        {
                            ctx.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                            return;
                        }
                        var stream = new StreamReader(ctx.Request.Body);
                        var json = await stream.ReadToEndAsync().ConfigureAwait(false);
                        index.LoadDocuments(new[] { transform(json) });
                        ctx.Response.StatusCode = (int) HttpStatusCode.Created;
                    }
                    else if ("DELETE".Equals(ctx.Request.Method, StringComparison.OrdinalIgnoreCase))
                    {
                        index.DeleteByTerm(identityField, id);
                    }
                    else
                    {
                        ctx.Response.StatusCode = (int) HttpStatusCode.MethodNotAllowed;
                        ctx.Response.ContentType = "application/json";
                        ctx.Response.Headers["Allow"] = "GET,PUT,POST,DELETE";

                        await ctx.Response.WriteAsync("\"Method Not Allowed\"", ctx.RequestAborted).ConfigureAwait(false);
                        return;
                    }
                }
                else
                {
                    await next(ctx).ConfigureAwait(false);
                }
            };
        }
    }
}