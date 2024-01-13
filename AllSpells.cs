using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoonoMod
{
    internal class AllSpells
    {
        // Spells that should NOT count towards total spell count.
        private readonly static HashSet<string> SPELL_BLACKLIST = new(new string[]
        {
            "EMPTY", // I don't give a fuck if Kira thinks "EMPTY" is a spell. It's not.
            "SOARING SWIM",
            "DEV FORWARD",
            "DEV XP",
            "DEV RESET",
            "DEV GODMODE",
            "JINGLE BELLS", // I can't believe that getting Jingle Bells can get you ending E when you're still missing a spell
        });

        // the 36 Lunacid 1.1.2 spells
        private readonly static HashSet<string> SPELL_WHITELIST = new(new string[]
        {
            "EMPTY",
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

        // if remains on 0 and isn't updated, we'll just not do the relevant patches
        private static int TOTAL_SPELL_COUNT = 0;

        // how many spells Kira thinks are in the game, as of Lunacid 1.1.2
        private readonly static int EXPECTED_TOTAL_SPELL_COUNT = 36;

        internal static void Init()
        {
            try
            {
                TOTAL_SPELL_COUNT = ComputeTotalSpellCount();
            }
            catch (TranspilerException e)
            {
                MoonoMod.Logger!.LogWarning($"Disabling total spell count fix due to error:\n{e}");
            }
            if (TOTAL_SPELL_COUNT != EXPECTED_TOTAL_SPELL_COUNT)
            {
                MoonoMod.Logger!.LogWarning($"Found evidence of {TOTAL_SPELL_COUNT} spells, but expected {EXPECTED_TOTAL_SPELL_COUNT}. Kira may have added new spells or fixed the spell count bug this mod fixes.");
            }
        }

        // get total spell count in a dynamic way. This is non-trivial, because the spells are unloaded in an asset bundle and Unity 2020.3.4f1 has no way to enumerate them.
        // instead we'll just read how many spells Kira thinks there are, because they've got a hardcoded count lying around.
        private static int ComputeTotalSpellCount()
        {
            MethodInfo hasAllSpells = AccessTools.DeclaredMethod(typeof(CONTROL), nameof(CONTROL.CheckForAllSpells));
            List<CodeInstruction> codes = PatchProcessor.GetOriginalInstructions(hasAllSpells);

            // sliding window search for ldc.i4.s, blt.s
            for (int index = codes.Count - 1; index > 0; index -= 1)
            {
                if ((codes[index - 1].opcode == OpCodes.Ldc_I4 || codes[index - 1].opcode == OpCodes.Ldc_I4_S) && (codes[index].opcode == OpCodes.Blt || codes[index].opcode == OpCodes.Blt_S))
                {
                    object totalSpellCount = codes[index - 1].operand;

                    try
                    {
                        // handle normal LDC
                        return (int)totalSpellCount;
                    }
                    catch
                    {
                    }

                    try
                    {
                        // handle short-form LDC
                        return (sbyte)totalSpellCount;
                    }
                    catch
                    {
                    }

                    throw new TranspilerException($"Could not extract int from {totalSpellCount.GetType()} when trying to read total spell count");
                }
            }

            throw new TranspilerException("Could not read total spell count");
        }

        private static bool ShouldPatchSpellCount()
        {
            return MoonoMod.fixAllSpellCheck!.Value && TOTAL_SPELL_COUNT != 0;
        }

        private static bool HasAllSpells(CONTROL control)
        {
            string[] spells = control.CURRENT_PL_DATA.SPELLS;
            int spell_count = 0;
            for (int index = 0; index < spells.Length && spells[index] != null && spells[index] != ""; index += 1)
            {
                // log ALL spells
                if (MoonoMod.verboseLogs!.Value)
                {
                    MoonoMod.Logger!.LogMessage(spells[index]);
                }

                if (!SPELL_BLACKLIST.Contains(spells[index]))
                {
                    spell_count += 1;
                }
            }

            if (MoonoMod.debugLogs!.Value)
            {
                MoonoMod.Logger!.LogInfo($"You have {spell_count} / {TOTAL_SPELL_COUNT} spells");
            }

            return spell_count >= TOTAL_SPELL_COUNT;
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
                if (MoonoMod.debugLogs!.Value)
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
