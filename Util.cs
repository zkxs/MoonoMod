using System.Text.RegularExpressions;

namespace MoonoMod
{
    internal class Util
    {
        private readonly static Regex NON_NUMERIC_SUFFIX = new Regex(@"^[^0-9]*", RegexOptions.Compiled);

        // trim trailing numeric characters from a string
        internal static string TrimTrailingNumbers(string str)
        {
            return NON_NUMERIC_SUFFIX.Match(str).Value;
        }
    }
}
