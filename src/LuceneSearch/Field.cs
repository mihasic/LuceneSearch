namespace LuceneSearch
{
    public class Field
    {
        public readonly string Name;
        public readonly bool FullText;
        public readonly bool ResultOnly;
        public readonly string SpecificAnalyzer;

        public Field(string name, bool fullText = false, string specificAnalyzer = null, bool resultOnly = false)
        {
            Name = name;
            FullText = fullText;
            SpecificAnalyzer = specificAnalyzer;
            ResultOnly = resultOnly;
        }
    }
}