namespace TestServer
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public static class TestData
    {
        public static IEnumerable<string> DocumentsXL() =>
            DocumentsFrom("ol_dump_authors_2017-04-30.txt");
        public static IEnumerable<string> DocumentsSmall() =>
            DocumentsFrom("small.txt");
        private static IEnumerable<string> DocumentsFrom(string fileName)
        {
            var file = Path.Combine(System.IO.Directory.GetCurrentDirectory(), fileName);
            using (var reader = File.OpenText(file))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line.Substring(line.IndexOf('{'));
                }
            }
        }
    }
}