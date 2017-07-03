namespace LuceneSearch
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Lucene.Net.Documents;
    using Lucene.Net.Index;

    internal static class FieldMapper
    {
        public static Dictionary<string, Func<string, IIndexableField>> Create(IEnumerable<LuceneSearch.Field> mapping) =>
            mapping.ToDictionary(x => x.Name, x => CreateFactory(x), StringComparer.OrdinalIgnoreCase);

        private static Func<string, IIndexableField> CreateFactory(LuceneSearch.Field field) =>
            (field.Numeric)
                ? (Func<string, IIndexableField>)(val => new NumericDocValuesField(field.Name, long.Parse(val)))
                : (field.FullText)
                    ? (Func<string, IIndexableField>)(val => new TextField(field.Name, val ?? "", Lucene.Net.Documents.Field.Store.YES))
                    : (field.ResultOnly)
                        ? (Func<string, IIndexableField>)(val => new Lucene.Net.Documents.Field(field.Name, val ?? "", new FieldType
                          {
                              IsStored = true,
                              IndexOptions = IndexOptions.NONE
                          }))
                        : (Func<string, IIndexableField>)(val => new StringField(field.Name, val ?? "", Lucene.Net.Documents.Field.Store.YES));
    }
}