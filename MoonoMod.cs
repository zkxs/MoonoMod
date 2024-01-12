// This file is part of MoonoMod and is licenced under the GNU GPL v3.0.
// See LICENSE file for full text.
// Copyright © 2024 Michael Ripley

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace MoonoMod
{
    [BepInPlugin("dev.zkxs.moonomod", "MoonoMod", "1.0.0")]
    public class MoonoMod : BaseUnityPlugin
    {
        private static new ManualLogSource? Logger;

        private static ConfigEntry<bool>? fullmoon;
        private static ConfigEntry<bool>? skipwaits;
        private static ConfigEntry<bool>? christmas;
        private static ConfigEntry<bool>? summer;
        private static ConfigEntry<bool>? debugLogs;

        private void Awake()
        {
            Logger = base.Logger; // this lets us access the logger from static contexts later: namely our patches.

            fullmoon = Config.Bind("General", "Force Full Moon", true, "Force full moon exclusive objects to appear on level load, and maximize the moon multiplier.");
            skipwaits = Config.Bind("General", "Skip Waits", false, "Force all checks to see if the player has waited some duration of time (sometimes minutes, somtimes months) to pass.");
            christmas = Config.Bind("General", "Force Christmas", false, "Force Christmas exclusive objects to appear on level load, and allow the Jingle Bells spell to be cast.");
            summer = Config.Bind("General", "Force Summer", false, "Force Summer exclusive objects to appear on level load.");
            debugLogs = Config.Bind("Developer", "Enable Debug Logs", false, "Emit BepInEx logs when certain time checks are detected. Useful for figuring out which levels contain which checks.");

            Harmony harmony = new Harmony("dev.zkxs.moonomod");
            try
            {
                harmony.PatchAll();
                Logger.LogInfo("You're about to hack time, are you sure?"); // kung fury quote
            }
            catch (Exception e)
            {
                Logger.LogError($"Something has gone terribly wrong:\n{e}");
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

            if (fullmoon!.Value)
            {
                // This date ALSO counts as a full moon, somewhat less obviously.
                return new(2023, 12, 25);
            }
            else
            {
                return DateTime.Now;
            }
        }

        [HarmonyPatch]
        private static class HarmonyPatches
        {
            // Enable the March-August timed content by skipping the check. This is needed because the above patch sets the month to December.
            // This is used to put some flowers on Patchouli's branch-horn-things.
            [HarmonyPrefix]
            [HarmonyPatch(typeof(Season_Con), "OnEnable")]
            private static bool SeasonCon(Season_Con __instance)
            {
                if (debugLogs!.Value)
                {
                    Logger!.LogInfo("Level contains a Summer check");
                }

                if (!summer!.Value)
                {
                    return true; // run original method
                }

                __instance.transform.GetChild(0).gameObject.SetActive(true);
                return false; // skip original method
            }

            // Skip the wait-a-month skeleton egg check. Normally it requires the month to change... except we've frozen the time to 2023-12-25 so it will never change.
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
                        Logger!.LogInfo("Level contains a wait-a-month check.");
                    }

                    if (!skipwaits!.Value)
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
                        Logger!.LogInfo($"Level contains a {__instance.length} minutes wait check.");
                    }

                    if (!skipwaits!.Value)
                    {
                        return true; // run original method
                    }

                    if (__instance.ACT != null)
                    {
                        __instance.ACT.SetActive(true);
                    }
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
                    Logger!.LogInfo("Level contains a Christmas check.");
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

                for (var index = 0; index < codes.Count; index++)
                {
                    if (santaCast)
                    {
                        // we've found the santa string, so now search for the next DateTime.Now call
                        if (codes[index].Calls(nowMethod))
                        {
                            // replace with a call to our faked DateTime.Now()
                            codes[index] = new CodeInstruction(OpCodes.Call, fakeMethod);
                            return codes.AsEnumerable();
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

                for (int index = 0; index < codes.Count; index++)
                {
                    // we've found the santa string, so now search for the next DateTime.Now call
                    if (codes[index].Calls(nowMethod))
                    {
                        // replace with a call to our faked DateTime.Now()
                        codes[index] = new CodeInstruction(OpCodes.Call, fakeMethod);
                        replacedAny = true;
                    }
                }

                if (replacedAny)
                {
                    return codes.AsEnumerable();
                }
                else
                {
                    throw new TranspilerException("could not find any DateTime.Now calls to patch");
                }
            }

        }

        private class TranspilerException : Exception
        {
            public TranspilerException(string message) : base(message) { }
        }

    }

}
