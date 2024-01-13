// This file is part of MoonoMod and is licenced under the GNU GPL v3.0.
// See LICENSE file for full text.
// Copyright © 2024 Michael Ripley

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoonoMod
{
    [BepInPlugin(GUID, MOD_NAME, VERSION)]
    public class MoonoMod : BaseUnityPlugin
    {
        internal const string GUID = "dev.zkxs.moonomod";
        internal const string MOD_NAME = "MoonoMod";
        internal const string VERSION = "1.1.0";

        // Spells that should not count towards total spell count.
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
        private static int TOTAL_SPELL_COUNT = 0; // if remains on 0 and isn't updated, we'll just not do the relevant patches
        private readonly static int EXPECTED_TOTAL_SPELL_COUNT = 36;
        private readonly static string EXPECTED_VERSION = "1.1.2";
        private readonly static int SCALING_TYPE_MOON = 1;

        // this is the same as the vanilla list, except the circular upgrade weapons "SHINING BLADE" and "SHADOW BLADE" are removed, as they have numbers appended if they have nonzero weapon XP which breaks Kira's check.
        private readonly static HashSet<string> SPECIAL_WEAPONS = new(new string[]
        {
            "JOTUNN SLAYER",
            "DARK GREATSWORD",
            "DOUBLE CROSSBOW",
            "ELFEN LONGSWORD",
            "FIRE SWORD",
            "HERITAGE SWORD",
            "IRON TORCH",
            "LYRIAN GREATSWORD",
            "MARAUDER BLACK FLAIL",
            "POISON CLAW",
            "SAINT ISHII",
            "SILVER RAPIER",
            "STEEL CLUB",
            "STEEL LANCE",
        });
        private static int TOTAL_WEAPON_COUNT = 0; // if remains on 0 and isn't updated, we'll just not do the relevant patches
        private readonly static int EXPECTED_TOTAL_WEAPONS = 48; // fun fact: I count 50 obtainable weapons. Maybe 51 if you can get both an Obsidian Cursebrand and Obsidian Poisonguard to drop. So I have no idea where Kira got 48 from.
        private readonly static int EXPECTED_SPECIAL_WEAPONS = SPECIAL_WEAPONS.Count;

        private static new ManualLogSource? Logger;

        private static ConfigEntry<bool>? fullMoon;
        private static ConfigEntry<bool>? skipWaits;
        private static ConfigEntry<bool>? christmas;
        private static ConfigEntry<bool>? summer;
        private static ConfigEntry<bool>? fixAllSpellCheck;
        private static ConfigEntry<bool>? fixAllWeaponCheck;
        private static ConfigEntry<bool>? debugLogs;

        private void Awake()
        {
            try
            {
                Logger = base.Logger; // this lets us access the logger from static contexts later: namely our patches.

                fullMoon = Config.Bind("General", "Force Full Moon", true, "Force full moon exclusive objects to appear on level load, and maximize the moon multiplier.");
                skipWaits = Config.Bind("General", "Skip Waits", false, "Force all checks to see if the player has waited some duration of time (sometimes minutes, somtimes months) to pass.");
                christmas = Config.Bind("General", "Force Christmas", false, "Force Christmas exclusive objects to appear on level load, and allow the Jingle Bells spell to be cast.");
                summer = Config.Bind("General", "Force Summer", false, "Force Summer exclusive objects to appear on level load.");
                fixAllSpellCheck = Config.Bind("Bugfixes", "Fix All-Spell Check", true, "Fix the all-spell check to not include normally unobtainable spells in your total.");
                fixAllWeaponCheck = Config.Bind("Bugfixes", "Fix All-Weapon Check", true, "Fix the all-weapon check to not not break if your Shadow/Shining blade has nonzero weapon XP.");
                debugLogs = Config.Bind("Developer", "Enable Debug Logs", false, "Emit BepInEx logs when certain time checks are detected. Useful for figuring out which levels contain which checks.");

                if (Application.version != EXPECTED_VERSION)
                {
                    Logger.LogWarning($"Lunacid is on version {Application.version}, but {MOD_NAME} was built for Lunacid {EXPECTED_VERSION}. While {MOD_NAME} was designed to be as future-proof as reasonably possible, it may behave in unintended ways as I can't forsee every change Kira might make.");
                }

                Harmony harmony = new Harmony(GUID);

                try
                {
                    TOTAL_SPELL_COUNT = ComputeTotalSpellCount();
                }
                catch (TranspilerException e)
                {
                    Logger.LogWarning($"Disabling total spell count fix due to error:\n{e}");
                }
                if (TOTAL_SPELL_COUNT != EXPECTED_TOTAL_SPELL_COUNT)
                {
                    Logger.LogWarning($"Found evidence of {TOTAL_SPELL_COUNT} spells, but expected {EXPECTED_TOTAL_SPELL_COUNT}. Kira may have added new spells or fixed the spell count bug this mod fixes.");
                }

                try
                {
                    TOTAL_WEAPON_COUNT = ComputeTotalWeaponCount();
                }
                catch (TranspilerException e)
                {
                    Logger.LogWarning($"Disabling total weapon count fix due to error:\n{e}");
                }
                if (TOTAL_WEAPON_COUNT != EXPECTED_TOTAL_WEAPONS)
                {
                    Logger.LogWarning($"Found evidence of {TOTAL_WEAPON_COUNT} weapons, but expected {EXPECTED_TOTAL_WEAPONS}. Kira may have added new weapons or fixed the weapon count bug this mod fixes.");
                }

                harmony.PatchAll();
                Logger.LogInfo("You're about to hack time, are you sure?"); // kung fury quote
            }
            catch (Exception e)
            {
                base.Logger.LogError($"Something has gone terribly wrong:\n{e}");
                throw e;
            }
        }

        // the date passed to the Christmas spell's time check.
        public static DateTime ChristmasDate()
        {
            // this is intentionally not logged, as it'd get spammed every time you try casting the Christmas spell.

            if (christmas!.Value)
            {
                // This date counts as Christmas, obviously
                return new(2023, 12, 25);
            }
            else
            {
                return DateTime.Now;
            }
        }

        // the date passed to SimpleMoon's time check
        public static DateTime FullMoonDate()
        {
            // this is intentionally not logged, as the game makes a LOT of SimpleMoon components.

            if (fullMoon!.Value)
            {
                // This date ALSO counts as a full moon, somewhat less obviously.
                return new(2023, 12, 25);
            }
            else
            {
                return DateTime.Now;
            }
        }

        // get total spell count in a dynamic way. This is non-trivial, because the spells are unloaded in an asset bundle and Unity 2020.3.4f1 has no way to enumerate them.
        // instead we'll just read how many spells Kira thinks there are, because he's got a hardcoded count lying around.
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

        // get total weapon count in a dynamic way. This is non-trivial, because the weapons are unloaded in an asset bundle and Unity 2020.3.4f1 has no way to enumerate them.
        // instead we'll just read how many weapons Kira thinks there are, because he's got a hardcoded count lying around.
        private static int ComputeTotalWeaponCount()
        {
            MethodInfo hasAllWeapons = AccessTools.DeclaredMethod(typeof(CONTROL), nameof(CONTROL.CheckForAllWeps));
            List<CodeInstruction> codes = PatchProcessor.GetOriginalInstructions(hasAllWeapons);

            // sliding window search for ldc.i4.s, blt.s
            for (int index = codes.Count - 1; index > 0; index -= 1)
            {
                if ((codes[index - 1].opcode == OpCodes.Ldc_I4 || codes[index - 1].opcode == OpCodes.Ldc_I4_S) && (codes[index].opcode == OpCodes.Blt || codes[index].opcode == OpCodes.Blt_S))
                {
                    object totalWeaponCount = codes[index - 1].operand;

                    try
                    {
                        // handle normal LDC
                        return (int)totalWeaponCount;
                    }
                    catch
                    {
                    }

                    try
                    {
                        // handle short-form LDC
                        return (sbyte)totalWeaponCount;
                    }
                    catch
                    {
                    }

                    throw new TranspilerException($"Could not extract int from {totalWeaponCount.GetType()} when trying to read total weapon count");
                }
            }

            throw new TranspilerException("Could not read total weapon count");
        }

        private static bool ShouldPatchSpellCount()
        {
            return fixAllSpellCheck!.Value && TOTAL_SPELL_COUNT != 0;
        }

        private static bool ShouldPatchWeaponCount()
        {
            return fixAllWeaponCheck!.Value && TOTAL_WEAPON_COUNT != 0;
        }

        private static bool HasAllSpells(CONTROL control)
        {
            string[] spells = control.CURRENT_PL_DATA.SPELLS;
            int spell_count = 0;
            for (int index = 0; index < spells.Length && spells[index] != null && spells[index] != ""; index += 1)
            {
                if (!SPELL_BLACKLIST.Contains(spells[index]))
                {
                    spell_count += 1;
                }
            }

            if (debugLogs!.Value)
            {
                Logger!.LogInfo($"You have {spell_count} / {TOTAL_SPELL_COUNT} spells");
            }

            return spell_count >= TOTAL_SPELL_COUNT;
        }

        private static bool HasAllWeapons(CONTROL control)
        {
            string[] weapons = control.CURRENT_PL_DATA.WEPS;

            int weapon_count = -1; // presumably Kira's way of dealing with EMPTY
            int special_weapon_count = 0;
            for (int index = 0; index < weapons.Length && weapons[index] != null && weapons[index] != ""; index += 1)
            {
                weapon_count += 1;
                if (SPECIAL_WEAPONS.Contains(weapons[index]))
                {
                    special_weapon_count += 1;
                }
            }

            if (debugLogs!.Value)
            {
                Logger!.LogInfo($"You have {weapon_count} / {TOTAL_WEAPON_COUNT} weapons, and {special_weapon_count} / {EXPECTED_SPECIAL_WEAPONS} special weapons.");
            }

            return weapon_count >= TOTAL_WEAPON_COUNT && special_weapon_count >= EXPECTED_SPECIAL_WEAPONS;
        }

        [HarmonyPatch]
        private static class HarmonyPatches
        {
            // Enable the March-August timed content by skipping the check.
            // This is used to put some flowers on Patchouli's branch-horn-things.
            [HarmonyPrefix]
            [HarmonyPatch(typeof(Season_Con), "OnEnable")]
            private static bool SeasonCon(Season_Con __instance)
            {
                if (debugLogs!.Value)
                {
                    Logger!.LogInfo($"Level {SceneManager.GetActiveScene().name} contains a Summer check.");
                }

                if (!summer!.Value)
                {
                    return true; // run original method
                }

                __instance.transform.GetChild(0).gameObject.SetActive(true);
                return false; // skip original method
            }

            // Skip the wait-a-month skeleton egg check. Normally it requires the month to change.
            // Also note that the vanilla code isn't making you wait a month... it's making you wait for the current month to CHANGE.
            [HarmonyPrefix]
            [HarmonyPatch(typeof(WaitAMonth), "Start")]
            private static bool WaitAMonth(WaitAMonth __instance)
            {
                if (__instance.Setter)
                {
                    return true; // run original method it it's a Setter call, because why not?
                }
                else
                {
                    if (debugLogs!.Value)
                    {
                        Logger!.LogInfo($"Level {SceneManager.GetActiveScene().name} contains a wait-a-month check.");
                    }

                    if (!skipWaits!.Value)
                    {
                        return true; // run original method
                    }

                    // if it's a Getter call just skip the check and run the SetActive code.
                    __instance.transform.GetChild(0).gameObject.SetActive(true);
                    return false; // skip original method
                }
            }

            // Skip the real time duration that certain things require you to wait for.
            // Note that Kira's code is buggy and doesn't track time correctly. There are not 600 minutes in a day, Kira.
            [HarmonyPrefix]
            [HarmonyPatch(typeof(Real_Timer), "OnEnable")]
            private static bool RealTimer(Real_Timer __instance)
            {
                if (__instance.Begin)
                {
                    return true; // run original method it it's a Begin call, because why not?
                }
                else
                {
                    if (debugLogs!.Value)
                    {
                        Logger!.LogInfo($"Level {SceneManager.GetActiveScene().name} contains a {__instance.length} minutes wait check.");
                    }

                    if (!skipWaits!.Value)
                    {
                        return true; // run original method
                    }

                    __instance.ACT?.SetActive(true);
                    return false; // skip original method
                }
            }

            // Always enable objects that are Christmas-exclusive. This is mostly decorations, but also the Christmas spell.
            [HarmonyPrefix]
            [HarmonyPatch(typeof(CRIMPUS), "OnEnable")]
            private static bool Christmas(WaitAMonth __instance)
            {
                if (debugLogs!.Value)
                {
                    Logger!.LogInfo($"Level {SceneManager.GetActiveScene().name} contains a Christmas check.");
                }

                if (!christmas!.Value)
                {
                    return true; // run original method
                }

                __instance.transform.GetChild(0).gameObject.SetActive(true);
                return false; // skip original method
            }

            // Make the Christmas spell always work. This is achieved by hijacking one of the DateTime.Now calls Magic_scr.Cast() makes.
            [HarmonyTranspiler]
            [HarmonyPatch(typeof(Magic_scr), "Cast")]
            private static IEnumerable<CodeInstruction> ChristmasCast(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                var nowMethod = AccessTools.DeclaredPropertyGetter(typeof(DateTime), nameof(DateTime.Now));
                var fakeMethod = AccessTools.DeclaredMethod(typeof(MoonoMod), nameof(ChristmasDate));

                bool santaCast = false;

                for (var index = 0; index < codes.Count; index += 1)
                {
                    if (santaCast)
                    {
                        // we've found the santa string, so now search for the next DateTime.Now call
                        if (codes[index].Calls(nowMethod))
                        {
                            // replace with a call to our faked DateTime.Now()
                            codes[index] = new CodeInstruction(OpCodes.Call, fakeMethod);
                            return codes;
                        }
                    }
                    else
                    {
                        // search for first santa cast
                        if (codes[index].opcode == OpCodes.Ldstr && (string)codes[index].operand == "SANTA_CAST")
                        {
                            santaCast = true;
                        }
                    }
                }

                throw new TranspilerException("could not find DateTime.Now call after LDSTR \"SANTA_CAST\" to patch");
            }

            // Make it always a full moon. This is achieved by hijacking all DateTime.Now calls SimpleMoon makes.
            [HarmonyTranspiler]
            [HarmonyPatch(typeof(SimpleMoon), "Start")]
            private static IEnumerable<CodeInstruction> FullMoon(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                var nowMethod = AccessTools.DeclaredPropertyGetter(typeof(DateTime), nameof(DateTime.Now));
                var fakeMethod = AccessTools.DeclaredMethod(typeof(MoonoMod), nameof(FullMoonDate));

                bool replacedAny = false;

                for (int index = 0; index < codes.Count; index += 1)
                {
                    if (codes[index].Calls(nowMethod))
                    {
                        // replace with a call to our faked DateTime.Now()
                        codes[index] = new CodeInstruction(OpCodes.Call, fakeMethod);
                        replacedAny = true;
                    }
                }

                if (replacedAny)
                {
                    return codes;
                }
                else
                {
                    throw new TranspilerException("could not find any DateTime.Now calls to patch");
                }
            }

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

            // Log total weapon progress
            [HarmonyPrefix]
            [HarmonyPatch(typeof(CONTROL), nameof(CONTROL.CheckForAllWeps))]
            private static bool CheckForAllWeps(CONTROL __instance, GameObject ___ACHY)
            {
                if (!ShouldPatchWeaponCount())
                {
                    return true; // run original method
                }

                ___ACHY?.transform.GetChild(0).gameObject.SetActive(HasAllWeapons(__instance));
                return false; // skip original method
            }

            // Fix the ending E check against if the player has all spells to not count normally unobtainable spells
            [HarmonyPrefix]
            [HarmonyPatch(typeof(Ending_Switch), "Check")]
            private static bool EndingCheck(Ending_Switch __instance)
            {
                if (debugLogs!.Value)
                {
                    Logger!.LogInfo($"Level {SceneManager.GetActiveScene().name} contains an ending check.");
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

            // log levels that contain full moon exclusive objects
            [HarmonyPrefix]
            [HarmonyPatch(typeof(Spawn_if_moon), "OnEnable")]
            private static void LogMoonCheck(Spawn_if_moon __instance)
            {
                if (debugLogs!.Value)
                {
                    bool passed = __instance.MOON.MOON_MULT > 9.0;
                    Logger!.LogInfo($"Level {SceneManager.GetActiveScene().name} contains a full moon check. MOON_MULT = {__instance.MOON.MOON_MULT}. Pass = {passed}.");
                }
            }

            // log levels that contain materials or lights affected by MOON_MULT
            [HarmonyPrefix]
            [HarmonyPatch(typeof(Moon_Light), "OnEnable")]
            private static void LogMoonLight(Moon_Light __instance)
            {
                if (debugLogs!.Value)
                {
                    Logger!.LogInfo($"Level {SceneManager.GetActiveScene().name} contains {__instance.Mats.Length} materials and {__instance.Lights.Length} lights affected by MOON_MULT ({__instance.MOON.MOON_MULT}).");
                }
            }

            // log levels that enemies with moon-based health scaling
            [HarmonyPrefix]
            [HarmonyPatch(typeof(NPC_Scaling), "Scale_NPC")]
            private static void LogMoonHealth(NPC_Scaling __instance)
            {
                if (debugLogs!.Value && __instance.Scaling_Type == SCALING_TYPE_MOON)
                {
                    var scale_factor = Mathf.Lerp(1f, __instance.scale_str, __instance.MOON.MOON_MULT / 8f);
                    Logger!.LogInfo($"Level {SceneManager.GetActiveScene().name} contains NPC {__instance.AI.gameObject.name} with health scaled by {scale_factor} to {__instance.AI.health_max} by MOON_MULT ({__instance.MOON.MOON_MULT}).");
                }
            }

        }

        private class TranspilerException : Exception
        {
            public TranspilerException(string message) : base(message) { }
        }

    }

}
