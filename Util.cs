// This file is part of MoonoMod and is licenced under the GNU GPL v3.0.
// See LICENSE file for full text.
// Copyright © 2024 Michael Ripley

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
