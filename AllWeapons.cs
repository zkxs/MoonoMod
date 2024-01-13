// This file is part of MoonoMod and is licenced under the GNU GPL v3.0.
// See LICENSE file for full text.
// Copyright © 2024 Michael Ripley

using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace MoonoMod
{
    internal class AllWeapons
    {
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

        // there's 51 distinct weapons you can technically have. Also you can have duplicates... Kira checks for 48. Which 3 didn't they want to count?
        private readonly static HashSet<string> TERMINAL_WEAPONS = new(new string[] {
            "EMPTY", // included for completeness
            "AXE OF HARMING",
            "BATTLE AXE",
            "BLADE OF JUSZTINA",
            "BLADE OF OPHELIA",
            "BLESSED WIND",
            "BRITTLE ARMING SWORD", // numeric suffix
            "CORRUPTED DAGGER",
            "CURSED BLADE",
            "DARK GREATSWORD",
            "DARK RAPIER",
            "DOUBLE CROSSBOW",
            "ELFEN BOW",
            "ELFEN LONGSWORD",
            "FIRE SWORD",
            "FISHING SPEAR",
            "GOLDEN KHOPESH",
            "GOLDEN SICKLE",
            "HALBERD",
            "HAMMER OF CRUELTY",
            "HERITAGE SWORD",
            "ICE SICKLE",
            "IRON TORCH",
            "JAILORS CANDLE",
            "JOTUNN SLAYER",
            "LUCID BLADE",
            "LYRIAN GREATSWORD",
            "MARAUDER BLACK FLAIL",
            "MOONLIGHT",
            "POISON CLAW",
            "PRIVATEER MUSKET",
            "RITUAL DAGGER",
            "SAINT ISHII",
            "SERPENT FANG",
            "SILVER RAPIER",
            "SKELETON AXE",
            "STEEL CLUB",
            "STEEL LANCE",
            "STEEL NEEDLE",
            "STEEL SPEAR",
            "SUCSARIAN DAGGER",
            "SUCSARIAN SPEAR",
            "TWISTED STAFF",
            "VAMPIRE HUNTER SWORD",
            "WAND OF POWER",
            "WOLFRAM GREATSWORD",
            "WOODEN SHIELD",
        });

        // My best guess as to the weapons Kira didn't want to count in the 48
        private readonly static HashSet<string> SECRET_WEAPONS = new(new string[] {
            "DEATH SCYTHE", // numeric suffix. Might be considered secret?
            "LIMBO", // might be considered secret?
        });

        // player must have AT LEAST one of these two... because oh my god you can have infinite of these in your inventory
        private readonly static HashSet<string> OBSIDIAN_WEAPONS = new(new string[] {
            "OBSIDIAN CURSEBRAND", // numeric suffix
            "OBSIDIAN POISONGUARD", // numeric suffix
        });

        // player must have one of these two
        private readonly static HashSet<string> SHADOW_SHINING_BLADE = new(new string[] {
            "SHADOW BLADE", // numeric suffix
            "SHINING BLADE", // numeric suffix
        });

        // if remains on 0 and isn't updated, we'll just not do the relevant patches
        private static int TOTAL_WEAPON_COUNT = 0;

        // fun fact: I count 50 obtainable weapons. Maybe 51 if you can get both an Obsidian Cursebrand and Obsidian Poisonguard to drop. So I have no idea where Kira got 48 from.
        private readonly static int EXPECTED_TOTAL_WEAPONS = 48;

        // this matches kira's expected count of 15 (at least after I solved the "SHINING BLADE"/"SHADOW BLADE" debacle)
        private readonly static int EXPECTED_SPECIAL_WEAPONS = SPECIAL_WEAPONS.Count;

        internal static void Init()
        {
            try
            {
                TOTAL_WEAPON_COUNT = ComputeTotalWeaponCount();
            }
            catch (TranspilerException e)
            {
                MoonoMod.Logger!.LogWarning($"Disabling total weapon count fix due to error:\n{e}");
            }
            if (TOTAL_WEAPON_COUNT != EXPECTED_TOTAL_WEAPONS)
            {
                MoonoMod.Logger!.LogWarning($"Found evidence of {TOTAL_WEAPON_COUNT} weapons, but expected {EXPECTED_TOTAL_WEAPONS}. Kira may have added new weapons or fixed the weapon count bug this mod fixes.");
            }
        }

        // get total weapon count in a dynamic way. This is non-trivial, because the weapons are unloaded in an asset bundle and Unity 2020.3.4f1 has no way to enumerate them.
        // instead we'll just read how many weapons Kira thinks there are, because they've got a hardcoded count lying around.
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

        private static bool ShouldPatchWeaponCount()
        {
            return MoonoMod.fixAllWeaponCheck!.Value && TOTAL_WEAPON_COUNT != 0;
        }

        private static bool HasAllWeapons(CONTROL control)
        {
            string[] weapons = control.CURRENT_PL_DATA.WEPS;
            HashSet<string> weaponSet = new();

            int weapon_count = -1; // presumably Kira's way of dealing with EMPTY. It's terrible, but here I am doing it too.
            int special_weapon_count = 0;
            for (int index = 0; index < weapons.Length && weapons[index] != null && weapons[index] != ""; index += 1)
            {
                string weaponName = Util.TrimTrailingNumbers(weapons[index]);
                weaponSet.Add(weaponName);

                // log ALL weapons
                if (MoonoMod.debugInventory?.Value ?? false)
                {
                    MoonoMod.Logger!.LogMessage($"you have {weapons[index]} = {weaponName}");
                }

                weapon_count += 1;
                if (SPECIAL_WEAPONS.Contains(weapons[index]))
                {
                    special_weapon_count += 1;
                }
            }

            if (MoonoMod.debugLogs?.Value ?? false)
            {
                bool anyObsidian = weaponSet.Overlaps(OBSIDIAN_WEAPONS);
                bool anyShadowShining = weaponSet.Overlaps(SHADOW_SHINING_BLADE);
                HashSet<string> missingWeapons = new(TERMINAL_WEAPONS);
                missingWeapons.ExceptWith(weaponSet);
                MoonoMod.Logger!.LogInfo($"You have {weapon_count} / {TOTAL_WEAPON_COUNT} weapons, and {special_weapon_count} / {EXPECTED_SPECIAL_WEAPONS} special weapons. obsidian={weaponSet.Overlaps(OBSIDIAN_WEAPONS)} shadowShining={anyShadowShining} missing={missingWeapons.Count}");
                if (MoonoMod.debugInventory?.Value ?? false)
                {
                    foreach (string missingWeapon in missingWeapons)
                    {
                        MoonoMod.Logger!.LogInfo($"missing {missingWeapon}");
                    }
                }

                return anyObsidian && anyShadowShining && missingWeapons.Count == 0;
            }
            else
            {
                return weaponSet.Overlaps(OBSIDIAN_WEAPONS) && weaponSet.Overlaps(SHADOW_SHINING_BLADE) && weaponSet.IsSupersetOf(TERMINAL_WEAPONS);
            }

        }

        [HarmonyPatch]
        private static class HarmonyPatches
        {
            // Fix the achievment check against all weapons to count Shadow/Shining blade
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

        }

    }

}
