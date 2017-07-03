namespace TestServer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using LuceneSearch;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using P = System.Collections.Generic.KeyValuePair<string, string>;

    public class Program
    {
        public static void Main(string[] args)
        {
            bool needLoad = false;
            var dataDirectory = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "data");
            if (!Directory.Exists(dataDirectory))
            {
                System.IO.Directory.CreateDirectory(dataDirectory);
                needLoad = true;
            }

            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .ConfigureLogging(loggerFactory => loggerFactory.AddConsole())
                .UseStartup((app, env) =>
                {
                    if (env.IsDevelopment())
                    {
                        app.UseDeveloperExceptionPage();
                    }
                    Console.WriteLine("Loading documents...");
                    app.Map("/api", a => a.Use(ConfigureMiddleware(needLoad, dataDirectory)));

                    app.UseDefaultFiles();
                    app.UseStaticFiles();

                    app.Run(async (context) =>
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        context.Response.ContentType = "text/plain";
                        await context.Response.WriteAsync($"Cannot find requested resource\r\n{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}");
                    });
                })
                .UseUrls("http://localhost:5080")
                .UseIISIntegration()
                .Build();

            host.Run();
        }

        public static Func<RequestDelegate, RequestDelegate> ConfigureMiddleware(bool needLoad, string dataDirectory)
        {
            Func<string, IEnumerable<KeyValuePair<string, string>>> transform = json =>
                from j in new[] {json}
                let doc = (dynamic)JsonConvert.DeserializeObject(json)
                from p in new[]
                {
                    new P("key", (string)doc.key),
                    new P("name", (string)doc.name),
                    new P("personal_name", (string)doc.personal_name),
                    new P("last_modified", DateTime.Parse((string)doc.last_modified.value, new CultureInfo("en-US")).ToString("s")),
                    new P("revision", (string)doc.revision),
                    new P("name_f", (string)doc.name),
                    new P("personal_name_f", (string)doc.personal_name),
                    new P("__src", j)
                }
                select p;

            var index = new Index(dataDirectory, new []
            {
                new Field("key"),
                new Field("name"),
                new Field("personal_name"),
                new Field("last_modified"),
                new Field("revision", numeric: true),
                new Field("name_f", fullText: true),
                new Field("personal_name_f", fullText: true),
                new Field("__src", resultOnly: true)
            });

            if (needLoad)
            {
                index.LoadDocuments(TestData.DocumentsSmall().Select(transform));
                //index.LoadDocuments(TestData.DocumentsXL().Select(transform));
            }

            var search = SearchMiddleware.Create(index);
            var document = DocumentMiddleware.Create(index, "key", transform);

            return next => search(document(next));
        }
    }
}
