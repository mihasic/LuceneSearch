namespace LuceneSearch
{
    internal static class Extensions
    {
        public static int? ParseInt(this string val)
        {
            int res;
            return val != null && int.TryParse(val, out res) ? res : (int?) null;
        }
    }
}