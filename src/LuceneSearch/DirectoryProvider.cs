
namespace LuceneSearch
{
    using System;
    using System.IO;
    using Lucene.Net.Index;
    using Lucene.Net.Store;

    internal static class DirectoryProvider
    {
        internal static Lucene.Net.Store.Directory Create(string name)
        {
            switch (name)
            {
                case "~RAM":
                    return new RAMDirectory();
                case "~TEMP":
                    name = Path.Combine(Path.GetTempPath(), "lp-" + Guid.NewGuid().ToString("N"));
                    break;
            }
            if (!System.IO.Directory.Exists(name)) System.IO.Directory.CreateDirectory(name);
            var dir = FSDirectory.Open(name);
            if (IndexWriter.IsLocked(dir)) IndexWriter.Unlock(dir);
            var lockFilePath = Path.Combine(name, "write.lock");
            if (File.Exists(lockFilePath)) File.Delete(lockFilePath);
            return dir;
        }
    }
}