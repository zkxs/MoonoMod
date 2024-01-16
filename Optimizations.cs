// This file is part of MoonoMod and is licenced under the GNU GPL v3.0.
// See LICENSE file for full text.
// Copyright © 2024 Michael Ripley

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace MoonoMod
{
    // This class contains performance optimization patches to Lunacid that do not affect the game in any way (aside from making it run better).
    internal class Optimizations
    {
        // matches numbers in a string
        private readonly static Regex NUMERIC = new Regex(@"[0-9]+", RegexOptions.Compiled);

        // used to cache the number-removed versions of strings, by string reference
        private readonly static ConditionalWeakTable<string, string> removeNumbersCache = new();

        // REMOVE_NUMS existing is problematic enough, but without a total refactor of how items work all I can do is optimize it.
        // First, we can optimize it with a compiled regex replace. Might as well use the right tool for the right job.
        // Second, we can cache the results. The game calls this function on the same strings over and over again, so the caching will have a big payoff.
        // Note that this cache will automatically drop entries for keys that have been garbage collected, so it shouldn't memory leak.
        [HarmonyPatch]
        private static class HarmonyPatches
        {
            [HarmonyPrefix]
            [HarmonyPatch(typeof(StaticFuncs), nameof(StaticFuncs.REMOVE_NUMS))]
            private static bool RemoveNumbers(ref string __result, string check)
            {
                __result = removeNumbersCache.GetValue(check, check => NUMERIC.Replace(check, ""));
                return false; // skip original method
            }

        }

    }

}
