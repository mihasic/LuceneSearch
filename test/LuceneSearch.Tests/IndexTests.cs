namespace LuceneSearch.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Shouldly;
    using Xunit;
    using P = System.Collections.Generic.KeyValuePair<string, string>;

    public class IndexTests : IDisposable
    {
        private readonly Index _index;
        private readonly Field[] _mapping =
        {
            new Field("key"),
            new Field("value", resultOnly: true),
            new Field("multivalue")
        };

        public IndexTests() => _index = new Index("~TEMP", _mapping);

        [Fact]
        public void Can_get_count()
        {
            _index.UpdateByTerm("key", "1", new[] { new P("key", "1"), new P("value", "123")});
            _index.Count.ShouldBe(1);
            var doc = _index.GetByTerm("key", "1");
            doc.Single(x => x.Key == "key").Value.ShouldBe("1");
            doc.Single(x => x.Key == "value").Value.ShouldBe("123");
        }


        [Fact]
        public void Consequent_updates_also_update_reader()
        {
            _index.UpdateByTerm("key", "1", new[] { new P("key", "1"), new P("value", "123")});
            _index.Count.ShouldBe(1);
            _index.UpdateByTerm("key", "2", new[] { new P("key", "2"), new P("value", "234")});
            _index.Count.ShouldBe(2);
            var doc = _index.GetByTerm("key", "2");
            doc.Single(x => x.Key == "key").Value.ShouldBe("2");
            doc.Single(x => x.Key == "value").Value.ShouldBe("234");
        }

        [Fact]
        public void Can_build_query_using_dsl()
        {
            _index.UpdateByTerm("key", "1", new[] { new P("key", "1"), new P("multivalue", "123"), new P("multivalue", "234")});
            _index.UpdateByTerm("key", "2", new[] { new P("key", "2"), new P("multivalue", "234"), new P("multivalue", "345")});

            var query = new BooleanIndexQuery()
                .And(IndexQuery.Term("multivalue", "234"));

            var (total, _, docs) = _index.Search(query, fieldsToLoad: new HashSet<string>(){"key"});
            total.ShouldBe(2);

            query.AndNot(IndexQuery.Term("multivalue", "345"));

            (total, _, docs) = _index.Search(query, fieldsToLoad: new HashSet<string>(){"key"});
            total.ShouldBe(1);
            docs.SelectMany(x => x).Single(x => x.Key == "key").Value.ShouldBe("1");
        }

        [Fact]
        public void Can_backup_index_with_uncommitted_changes()
        {
            var destIndex = Path.Combine(Path.GetTempPath(), "lpbak-" + Guid.NewGuid().ToString("N"));

            _index.UpdateByTerm("key", "1", new[] { new P("key", "1")});
            _index.UpdateByTerm("key", "2", new[] { new P("key", "2")});
            _index.UpdateByTerm("key", "3", new[] { new P("key", "3")}, commit: false);

            _index.Copy(destIndex);

            _index.DeleteByTerm("key", "1", commit: true);

            var (total, _, docs) = _index.Search(IndexQuery.All, fieldsToLoad: new HashSet<string>(){"key"});
            total.ShouldBe(2);
            docs.SelectMany(x => x).Select(x => x.Value).ShouldBe(new [] { "2", "3" }, "Index has been updated");

            var backup = new Index(destIndex, _mapping);
            (total, _, docs) = backup.Search(IndexQuery.All, fieldsToLoad: new HashSet<string>(){"key"});
            total.ShouldBe(2);
            docs.SelectMany(x => x).Select(x => x.Value).ShouldBe(new [] { "1", "2" }, "Only committed ids persisted");
        }

        public void Dispose() => _index.Dispose();
    }
}