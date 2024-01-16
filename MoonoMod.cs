// This file is part of MoonoMod and is licenced under the GNU GPL v3.0.
// See LICENSE file for full text.
// Copyright © 2024 Michael Ripley

// Uncomment the following define to enable my debugging features. These include certain things I do not want in the base mod, as they're either logspam, inefficient, or just plain cheating.
#define DEBUG

using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoonoMod
{
    [BepInPlugin(GUID, MOD_NAME, MOD_VERSION)]
    public class MoonoMod : BaseUnityPlugin
    {
        internal const string GUID = "dev.zkxs.moonomod";
        internal const string MOD_NAME = "MoonoMod";
        internal const string MOD_VERSION = "1.2.0";

        private readonly static string EXPECTED_LUNACID_VERSION = "1.1.2";

        // the scaling type value that means an NPC scales based on MOON_MULT
        private readonly static int SCALING_TYPE_MOON = 1;

        // the so-called "unlimited" FPS setting's value
        private readonly static int FPS_SETTING_UNLIMITED = 2;

        // how many FPS below your monitor's maximum refresh rate to target.
        // This helps adaptive refresh rate monitors, as it reduces the chance of exceeding their refresh rate limit when VSync is off.
        private readonly static int FPS_HEADROOM = 5;

        internal static new ManualLogSource? Logger;

        internal static ConfigEntry<bool>? fullMoon;
        internal static ConfigEntry<bool>? skipWaits;
        internal static ConfigEntry<bool>? christmas;
        internal static ConfigEntry<bool>? summer;
        internal static ConfigEntry<bool>? disableVsync;
        internal static ConfigEntry<bool>? fixAllSpellCheck;
        internal static ConfigEntry<bool>? fixAllWeaponCheck;
        internal static ConfigEntry<bool>? allLoot;
        internal static ConfigEntry<bool>? debugLogs;
        internal static ConfigEntry<bool>? verboseLogs;
        internal static ConfigEntry<bool>? debugInventory;
        internal static ConfigEntry<bool>? debugLoot;

        private void Awake()
        {
            try
            {
                Logger = base.Logger; // this lets us access the logger from static contexts later: namely our patches.

                fullMoon = Config.Bind("General", "Force Full Moon", true, "Force full moon exclusive objects to appear on level load, and maximize the moon multiplier.");
                skipWaits = Config.Bind("General", "Skip Waits", false, "Force all checks to see if the player has waited some duration of time (sometimes minutes, somtimes months) to pass.");
                christmas = Config.Bind("General", "Force Christmas", false, "Force Christmas exclusive objects to appear on level load, and allow the Jingle Bells spell to be cast.");
                summer = Config.Bind("General", "Force Summer", false, "Force Summer exclusive objects to appear on level load.");
                disableVsync = Config.Bind("General", "Disable VSync with high FPS", false, "Forcibly disables VSync when using the high FPS option. Designed for adapative refresh rate monitors.");
                fixAllSpellCheck = Config.Bind("Bugfixes", "Fix All-Spell Check", true, "Fix the all-spell check to not include normally unobtainable spells in your total.");
                fixAllWeaponCheck = Config.Bind("Bugfixes", "Fix All-Weapon Check", true, "Fix the all-weapon check to not not break if your Shadow/Shining blade has nonzero weapon XP.");

#if DEBUG
                allLoot = Config.Bind("Cheats", "Drop All Loot", false, "Enemies drop everything in their loot table when killed.");
                debugLogs = Config.Bind("Developer", "Enable Debug Logs", false, "Emit BepInEx logs when certain time checks are detected. Useful for figuring out which levels contain which checks.");
                verboseLogs = Config.Bind("Developer", "Enable Verbose Logs", false, "Emit BepInEx logs in a higher level of verbosity."); // currently unused
                debugInventory = Config.Bind("Developer", "Enable Inventory Debug Logs", false, "Emit BepInEx logs that contain inventory state information.");
                debugLoot = Config.Bind("Developer", "Enable Loot Debug Logs", false, "Emit BepInEx logs that contain drop table information on enemy kill.");
#endif

                if (Application.version != EXPECTED_LUNACID_VERSION)
                {
                    Logger.LogWarning($"Lunacid is on version {Application.version}, but {MOD_NAME} was built for Lunacid {EXPECTED_LUNACID_VERSION}. While {MOD_NAME} was designed to be as future-proof as reasonably possible, it may behave in unintended ways as I can't forsee every change Kira might make.");
                }

                Harmony harmony = new Harmony(GUID);

                AllSpells.Init();
                AllWeapons.Init();

                harmony.PatchAll();

                disableVsync.SettingChanged += (sender, args) => Logger.LogInfo("woah");

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

        // used to hijacking all DateTime.Now calls the transpiled method makes and replace the date with that of a full moon
        private static IEnumerable<CodeInstruction> FullMoonTranspiler(IEnumerable<CodeInstruction> instructions)
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

        [HarmonyPatch]
        private static class HarmonyPatches
        {
            // Enable the March-August timed content by skipping the check.
            // This is used to put some flowers on Patchouli's branch-horn-things.
            [HarmonyPrefix]
            [HarmonyPatch(typeof(Season_Con), "OnEnable")]
            private static bool SeasonCon(Season_Con __instance)
            {
                if (debugLogs?.Value ?? false)
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
                    if (debugLogs?.Value ?? false)
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
                    if (debugLogs?.Value ?? false)
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
                if (debugLogs?.Value ?? false)
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

            // Make it always a full moon. This is used for MOON_MULT and Spawn_if_moon checks.
            [HarmonyTranspiler]
            [HarmonyPatch(typeof(SimpleMoon), "Start")]
            private static IEnumerable<CodeInstruction> SimpleMoon(IEnumerable<CodeInstruction> instructions)
            {
                return FullMoonTranspiler(instructions);
            }

            // Make it always a full moon. I'm not sure if this class is used by the game.
            [HarmonyTranspiler]
            [HarmonyPatch(typeof(Moon_scr), "Start")]
            private static IEnumerable<CodeInstruction> MoonScr(IEnumerable<CodeInstruction> instructions)
            {
                return FullMoonTranspiler(instructions);
            }

            // Always enable objects that are Christmas-exclusive. This is mostly decorations, but also the Christmas spell.
            [HarmonyPrefix]
            [HarmonyPatch(typeof(CONTROL), nameof(CONTROL.SetFPS))]
            private static bool DisableVsync(CONTROL __instance)
            {
                if (disableVsync!.Value && __instance.CURRENT_SYS_DATA.SETTING_EX7 == FPS_SETTING_UNLIMITED)
                {
                    Application.targetFrameRate = Screen.currentResolution.refreshRate - FPS_HEADROOM;
                    QualitySettings.vSyncCount = 0;
                    if (debugLogs?.Value ?? false)
                    {
                        Logger!.LogInfo($"Set target FPS to {Application.targetFrameRate}");
                    }
                    return false; // skip original method
                }
                else
                {
                    return true; // run original method
                }
            }

#if DEBUG
            // log levels that contain full moon exclusive objects
            [HarmonyPrefix]
            [HarmonyPatch(typeof(Spawn_if_moon), "OnEnable")]
            private static void LogMoonCheck(Spawn_if_moon __instance)
            {
                if (debugLogs?.Value ?? false)
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
                if (debugLogs?.Value ?? false)
                {
                    Logger!.LogInfo($"Level {SceneManager.GetActiveScene().name} contains {__instance.Mats.Length} materials and {__instance.Lights.Length} lights affected by MOON_MULT ({__instance.MOON.MOON_MULT}).");
                }
            }

            // log levels that enemies with moon-based health scaling
            [HarmonyPrefix]
            [HarmonyPatch(typeof(NPC_Scaling), "Scale_NPC")]
            private static void LogMoonHealth(NPC_Scaling __instance)
            {
                if ((debugLogs?.Value ?? false) && __instance.Scaling_Type == SCALING_TYPE_MOON)
                {
                    var scale_factor = Mathf.Lerp(1f, __instance.scale_str, __instance.MOON.MOON_MULT / 8f);
                    Logger!.LogInfo($"Level {SceneManager.GetActiveScene().name} contains NPC {__instance.AI.gameObject.name} with health scaled by {scale_factor} to {__instance.AI.health_max} by MOON_MULT ({__instance.MOON.MOON_MULT}).");
                }
            }

            // drop all possible loot from an enemy (instead of just one item from the loot table). THIS IS CHEATING! I used this to debug the all weapons achievement check, because ain't no way I was gonna get both an Obsidian Cursebrand and an Obsidian Posisonguard naturally.
            [HarmonyPrefix]
            [HarmonyPatch(typeof(Loot_scr), "OnEnable")]
            private static bool AllLoot(Loot_scr __instance)
            {
                if (debugLoot?.Value ?? false)
                {
                    Logger!.LogInfo($"{__instance.gameObject?.name} loot table:");
                    int totalChance = 0;
                    foreach (var reward in __instance.LOOTS)
                    {
                        totalChance += reward.CHANCE;
                    }

                    foreach (var reward in __instance.LOOTS)
                    {
                        string? name = reward.ITEM?.name ?? "null";
                        double chance = 100d * reward.CHANCE / totalChance;
                        Logger!.LogInfo($"* {chance,6:0.00}%: {name}");
                    }
                }

                if (!(allLoot?.Value ?? false))
                {
                    return true; // run original method
                }

                for (int index = 0; index < __instance.LOOTS.Length; index += 1)
                {
                    if (__instance.LOOTS[index].ITEM != null)
                    {
                        GameObject gameObject = Instantiate(__instance.LOOTS[index].ITEM, __instance.transform.position, Quaternion.identity);
                        gameObject.SetActive(false);
                        gameObject.AddComponent<Place_on_Ground>();
                        gameObject.GetComponent<Place_on_Ground>().LOOTED = true;
                        gameObject.SetActive(true);
                    }
                }

                return false; // skip original method
            }
#endif

        }

    }

}
