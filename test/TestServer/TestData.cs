namespace TestServer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Reflection;

    public static class TestData
    {
        public static IEnumerable<string> DocumentsXL() =>
            DocumentsFrom("ol_dump_authors_2017-05-31.txt.gz");
        public static IEnumerable<string> DocumentsSmall() =>
            DocumentsFrom("small.txt");
        private static IEnumerable<string> DocumentsFrom(string fileName)
        {
            var file = Path.Combine(System.IO.Directory.GetCurrentDirectory(), fileName);
            if (File.Exists(fileName))
            {
                var streamReader = file.EndsWith(".gz")
                    ? new StreamReader(new GZipStream(File.OpenRead(file), CompressionMode.Decompress, true))
                    : File.OpenText(file);
                return ReadFromStream(streamReader);
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