// This file is part of MoonoMod and is licenced under the GNU GPL v3.0.
// See LICENSE file for full text.
// Copyright © 2024 Michael Ripley

using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoonoMod
{
    internal class AllSpells
    {
        // the 36 Lunacid 1.1.2 spells
        private readonly static HashSet<string> ALL_SPELLS = new(new string[]
        {
            "EMPTY", // included for completeness
            "BARRIER",
            "BESTIAL COMMUNION",
            "BLOOD DRAIN",
            "BLOOD STRIKE",
            "BLUE FLAME ARC",
            "COFFIN",
            "CORPSE TRANSFORMATION",
            "DARK SKULL",
            "EARTH STRIKE",
            "EARTH THORN",
            "FIRE WORM",
            "FLAME FLARE",
            "FLAME SPEAR",
            "GHOST LIGHT",
            "HOLY WARMTH",
            "ICARIAN FLIGHT",
            "ICE SPEAR",
            "ICE TEAR",
            "IGNIS CALOR",
            "LAVA CHASM",
            "LIGHT REVEAL",
            "LIGHTNING",
            "LITHOMANCY",
            "MOON BEAM",
            "POISON MIST",
            "QUICK STRIDE",
            "ROCK BRIDGE",
            "SLIME ORB",
            "SPIRIT WARP",
            "SUMMON FAIRY",
            "SUMMON ICE SWORD",
            "SUMMON KODAMA",
            "SUMMON SNAIL",
            "TORNADO",
            "WIND DASH",
            "WIND SLICER",
        });

        private static bool ShouldPatchSpellCount()
        {
            return MoonoMod.fixAllSpellCheck!.Value;
        }

        private static bool HasAllSpells(CONTROL control)
        {
            string[] spells = control.CURRENT_PL_DATA.SPELLS;
            HashSet<string> spellSet = new();
            for (int index = 0; index < spells.Length && spells[index] != null && spells[index] != ""; index += 1)
            {
                spellSet.Add(spells[index]);

                // log ALL spells
                if (MoonoMod.debugInventory?.Value ?? false)
                {
                    MoonoMod.Logger!.LogMessage($"you have {spells[index]}");
                }
            }

            if (MoonoMod.debugInventory?.Value ?? false)
            {
                HashSet<string> missingSpells = new(ALL_SPELLS);
                missingSpells.ExceptWith(spellSet);
                foreach (string missingSpell in missingSpells)
                {
                    MoonoMod.Logger!.LogInfo($"missing {missingSpell}");
                }
                return missingSpells.Count != 0;
            }
            else
            {
                return spellSet.IsSupersetOf(ALL_SPELLS);
            }
        }

        [HarmonyPatch]
        private static class HarmonyPatches
        {
            // Fix the achievement check against if the player has all spells to not count normally unobtainable spells
            [HarmonyPrefix]
            [HarmonyPatch(typeof(CONTROL), nameof(CONTROL.CheckForAllSpells))]
            private static bool AllSpellsAchievement(CONTROL __instance, GameObject ___ACHY)
            {
                if (!ShouldPatchSpellCount())
                {
                    return true; // run original method
                }

                ___ACHY?.transform.GetChild(1).gameObject.SetActive(HasAllSpells(__instance));
                return false; // skip original method
            }

            // Fix the ending E check against if the player has all spells to not count normally unobtainable spells
            [HarmonyPrefix]
            [HarmonyPatch(typeof(Ending_Switch), "Check")]
            private static bool EndingCheck(Ending_Switch __instance)
            {
                if (MoonoMod.debugLogs?.Value ?? false)
                {
                    MoonoMod.Logger!.LogInfo($"Level {SceneManager.GetActiveScene().name} contains an ending check.");
                }

                if (!ShouldPatchSpellCount())
                {
                    return true; // run original method
                }

                if (HasAllSpells(__instance.CON))
                {
                    __instance.END_E.SetActive(true);
                    __instance.END_A.SetActive(false);
                }
                else
                {
                    __instance.END_E.SetActive(false);
                    __instance.END_A.SetActive(true);
                }
                return false; // skip original method
            }

        }

    }

}
