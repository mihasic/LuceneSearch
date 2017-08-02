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

    public class SearchTests
    {
        [Theory]
        [InlineData("name", "Beatrice")]
        [InlineData("name_f", "beatrice")]
        public async Task GetTerm(string termName, string from)
        {
            using (var client = new HttpClient(new AspNetCoreHttpMessageHandler(
                TestServer.Program.ConfigureMiddleware(true, "~TEMP")))
                {
                    BaseAddress = new Uri("http://localhost/")
                })
            {
                var x = await client.GetAsync($"/search/term/{termName}?from={from}&take=1");
                x.StatusCode.ShouldBe(HttpStatusCode.OK);
                var content = await x.Content.ReadAsStringAsync();
                content.Trim('[', ']', '"').ShouldStartWith(from);
            }
        }

        [Theory]
        [InlineData("last_modified", "[2008\\-08\\-18T22\\:37\\:00 TO 2008\\-08\\-18T22\\:37\\:00]", "2008-08-18T22:37:00")]
        [InlineData("last_modified", "{2008\\-08\\-18T22\\:36\\:59 TO 2008\\-08\\-18T22\\:37\\:01}", "2008-08-18T22:37:00")]
        [InlineData("last_modified", "2008\\-08\\-18T22\\:37\\:00", "2008-08-18T22:37:00")]
        [InlineData("name_f", "beatrice", "Beatrice")]
        public async Task SearchSingleTerm(string termName, string filter, string result)
        {
            using (var client = new HttpClient(new AspNetCoreHttpMessageHandler(
                TestServer.Program.ConfigureMiddleware(true, "~TEMP")))
                {
                    BaseAddress = new Uri("http://localhost/")
                })
            {
                var x = await client.GetAsync($"/search?{termName}={filter}&take=1&i={termName}");
                x.StatusCode.ShouldBe(HttpStatusCode.OK);
                var content = await x.Content.ReadAsStringAsync();
                content.ShouldContain(result);
            }
        }
    }
}
