using System.Collections.Generic;

namespace LuceneSearch.Tests
{
    using System;
    using System.Linq;
    using Shouldly;
    using Xunit;
    using P = KeyValuePair<string, string>;

    public class IndexTests : IDisposable
    {
        private readonly Index _index;

        public IndexTests() =>
            _index = new Index("~TEMP", new[]
            {
                new Field("key"),
                new Field("value", resultOnly: true)
            });

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

        public void Dispose() => _index.Dispose();
    }
}