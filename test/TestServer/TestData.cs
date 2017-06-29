namespace TestServer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    public static class TestData
    {
        public static IEnumerable<string> DocumentsXL() =>
            DocumentsFrom("ol_dump_authors_2017-04-30.txt");
        public static IEnumerable<string> DocumentsSmall() =>
            DocumentsFrom("small.txt");
        private static IEnumerable<string> DocumentsFrom(string fileName)
        {
            var file = Path.Combine(System.IO.Directory.GetCurrentDirectory(), fileName);
            if (File.Exists(fileName))
            {
                return ReadFromStream(File.OpenText(file));
            }
            else
            {
                var assembly = typeof(TestData).GetTypeInfo().Assembly;
                var resourceName = assembly.GetManifestResourceNames().First(x => x.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
                return ReadFromStream(new StreamReader(assembly.GetManifestResourceStream(resourceName)));
            }
        }

        private static IEnumerable<string> ReadFromStream(StreamReader reader)
        {
            using (reader)
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