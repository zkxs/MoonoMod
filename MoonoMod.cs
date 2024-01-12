using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace MoonoMod
{
    [BepInPlugin("dev.zkxs.moonomod", "MoonoMod", "1.0.0")]
    public class MoonoMod : BaseUnityPlugin
    {
        private static new ManualLogSource? Logger;

        private void Awake()
        {
            Logger = base.Logger; // this lets us access the logger from static contexts later: namely our patches.
            Harmony harmony = new Harmony("dev.zkxs.moonomod");
            harmony.PatchAll();
            Logger.LogInfo("You're about to hack time, are you sure?"); // kung fury quote
        }

        public DateTime ChristmasFullMoon()
        {
            // This date counts as a full moon AND Christmas.
            return new(2023, 12, 25);
        }

        [HarmonyPatch]
        private static class HarmonyPatches
        {
            // Enable both the full moon and Christmas timed content by telling the game that it's 2023-12-25.
            [HarmonyPrefix]
            [HarmonyPatch(typeof(DateTime), nameof(DateTime.Now), MethodType.Getter)]
            private static bool DateTimeNow(ref DateTime __result)
            {
                // This date counts as a full moon AND Christmas.
                __result = new(2023, 12, 25);

                return false; // skip original method
            }

            // Enable the March-August timed content by skipping the check. This is needed because the above patch sets the month to December.
            [HarmonyPrefix]
            [HarmonyPatch(typeof(Season_Con), "OnEnable")]
            private static bool SeasonCon(Season_Con __instance)
            {
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
                    if (__instance.ACT != null)
                    {
                        __instance.ACT.SetActive(true);
                    }
                    return false; // skip original method
                }
            }

            // Always enable objects that are Christmas-exclusive.
            [HarmonyPrefix]
            [HarmonyPatch(typeof(CRIMPUS), "OnEnable")]
            private static bool Christmas(WaitAMonth __instance)
            {
                __instance.transform.GetChild(0).gameObject.SetActive(true);
                return false; // skip original method
            }

            [HarmonyTranspiler]
            [HarmonyPatch(typeof(Magic_scr), "Cast")]
            private static IEnumerable<CodeInstruction> ChristmasCast(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                var nowMethod = AccessTools.DeclaredPropertyGetter(typeof(DateTime), nameof(DateTime.Now));
                var fakeMethod = AccessTools.DeclaredMethod(typeof(MoonoMod), nameof(ChristmasFullMoon));

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

            [HarmonyTranspiler]
            [HarmonyPatch(typeof(SimpleMoon), "Start")]
            private static IEnumerable<CodeInstruction> FullMoon(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                var nowMethod = AccessTools.DeclaredPropertyGetter(typeof(DateTime), nameof(DateTime.Now));
                var fakeMethod = AccessTools.DeclaredMethod(typeof(MoonoMod), nameof(ChristmasFullMoon));

                bool replacedAny = false;

                for (var index = 0; index < codes.Count; index++)
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
