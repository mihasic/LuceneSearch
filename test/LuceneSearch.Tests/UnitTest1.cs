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

    public class UnitTest1
    {
        [Fact]
        public async Task NotFound()
        {
            var dataDirectory = System.IO.Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dataDirectory);
            using (var client = new HttpClient(new AspNetCoreHttpMessageHandler(
                TestServer.Program.ConfigureMiddleware(true, dataDirectory)))
                {
                    BaseAddress = new Uri("http://localhost/")
                })
            {
                var x = await client.GetAsync("/anything");
                x.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            }
        }
    }
}
