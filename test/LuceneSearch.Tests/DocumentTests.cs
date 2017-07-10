namespace LuceneSearch.Tests
{
    using Shouldly;
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder.Internal;
    using Xunit;

    public class DocumentTests
    {
        [Fact]
        public async Task NotFound()
        {
            using (var client = new HttpClient(new AspNetCoreHttpMessageHandler(
                TestServer.Program.ConfigureMiddleware(true, "~TEMP")))
                {
                    BaseAddress = new Uri("http://localhost/")
                })
            {
                var x = await client.GetAsync("/anything");
                x.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            }
        }
        [Fact]
        public async Task Found()
        {
            using (var client = new HttpClient(new AspNetCoreHttpMessageHandler(
                TestServer.Program.ConfigureMiddleware(true, "~TEMP")))
                {
                    BaseAddress = new Uri("http://localhost/")
                })
            {
                var x = await client.GetAsync("/" + Uri.EscapeDataString("/authors/OL100656A"));
                x.StatusCode.ShouldBe(HttpStatusCode.OK);
            }
        }
    }
}
