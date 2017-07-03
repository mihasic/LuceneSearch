namespace LuceneSearch
{
    internal static class Extensions
    {
        public static int? ParseInt(this string val)
        {
            int res;
            return val != null && int.TryParse(val, out res) ? res : (int?) null;
        }

        public static bool? ParseBool(this string val)
        {
            bool res;
            return val != null && bool.TryParse(val, out res) ? res : (bool?) null;
        }
    }
}