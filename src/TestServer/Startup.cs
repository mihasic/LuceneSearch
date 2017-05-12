namespace TestServer
{
    using System;
    using System.Collections.Generic;
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

    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole();

            bool needLoad = false;
            var dataDirectory = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "data");
            if (!Directory.Exists(dataDirectory))
            {
                System.IO.Directory.CreateDirectory(dataDirectory);
                needLoad = true;
            }

            Func<string, IEnumerable<KeyValuePair<string, string>>> transform = json =>
                from j in new[] {json}
                let doc = (dynamic)JsonConvert.DeserializeObject(json)
                from p in new[]
                {
                    new P("key", (string)doc.key),
                    new P("name", (string)doc.name),
                    new P("personal_name", (string)doc.personal_name),
                    new P("last_modified", (string)doc.last_modified.value),
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
                new Field("revision"),
                new Field("name_f", fullText: true),
                new Field("personal_name_f", fullText: true),
                new Field("__src", resultOnly: true)
            });

            if (needLoad)
            {
                index.LoadDocuments(TestData.DocumentsXL().Select(transform));
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.Map(new PathString("/api"), a => a
                .UseSearchMiddleware(index)
                .UseDocumentMiddleware(index, "key", transform));

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.Run(async (context) =>
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync($"Cannot find requested resource\r\n{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}");
            });

        }
    }
}
