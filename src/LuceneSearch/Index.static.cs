namespace LuceneSearch
{
    using Lucene.Net.Codecs;
    using Lucene.Net.Codecs.Lucene41;
    using Lucene.Net.Codecs.Lucene45;
    using Lucene.Net.Codecs.Lucene46;

    public partial class Index
    {
        private class CustomPostingsFormatFactory : DefaultPostingsFormatFactory
        {
            public CustomPostingsFormatFactory()
            {
                PutPostingsFormatType(typeof(Lucene41PostingsFormat));
            }
        }

        private class CustomCodecFactory : DefaultCodecFactory
        {
            public CustomCodecFactory()
            {
                PutCodecType(typeof(Lucene46Codec));
            }
        }

        private class CustomDocValuesFormatFactory : DefaultDocValuesFormatFactory
        {
            public CustomDocValuesFormatFactory()
            {
                PutDocValuesFormatType(typeof(Lucene45DocValuesFormat));
            }
        }

        public static void InitializeEmbeddable()
        {
            // try avoid reflection
            PostingsFormat.SetPostingsFormatFactory(new CustomPostingsFormatFactory());
            Codec.SetCodecFactory(new CustomCodecFactory());
            DocValuesFormat.SetDocValuesFormatFactory(new CustomDocValuesFormatFactory());
            //Codec.Default = new Lucene46Codec();
        }

    }
}