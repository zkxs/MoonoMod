// This file is part of MoonoMod and is licenced under the GNU GPL v3.0.
// See LICENSE file for full text.
// Copyright © 2024 Michael Ripley

using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MoonoMod
{
    internal class AllWeapons
    {
        // there's 51 distinct weapons you can technically have. Also you can have duplicates... Kira checks for 48. Which 3 didn't they want to count?
        // Kira probably doesn't know about the drop duping bug that would let you have both the CURSEBRAND and the POISONGUARD, so really it's just 50 weapons you can have.
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
            "HERITAGE SWORD",
            "ICE SICKLE",
            "IRON TORCH",
            "JAILORS CANDLE",
            "JOTUNN SLAYER",
            "LIMBO", // might be considered secret?
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

        // player must have AT LEAST one of these two
        private readonly static HashSet<string> OBSIDIAN_WEAPONS = new(new string[] {
            "OBSIDIAN CURSEBRAND", // numeric suffix
            "OBSIDIAN POISONGUARD", // numeric suffix
        });

        // player must have one of these two
        private readonly static HashSet<string> SHADOW_SHINING_BLADE = new(new string[] {
            "SHADOW BLADE", // numeric suffix
            "SHINING BLADE", // numeric suffix
        });

        private static bool ShouldPatchWeaponCount()
        {
            return MoonoMod.fixAllWeaponCheck!.Value;
        }

        private static bool HasAllWeapons(CONTROL control)
        {
            string[] weapons = control.CURRENT_PL_DATA.WEPS;
            HashSet<string> weaponSet = new();
            for (int index = 0; index < weapons.Length && weapons[index] != null && weapons[index] != ""; index += 1)
            {
                string weaponName = Util.TrimTrailingNumbers(weapons[index]);
                weaponSet.Add(weaponName);

                // log ALL weapons
                if (MoonoMod.debugInventory?.Value ?? false)
                {
                    MoonoMod.Logger!.LogMessage($"you have {weapons[index]} = {weaponName}");
                }
            }

            if (MoonoMod.debugInventory?.Value ?? false)
            {
                bool anyObsidian = weaponSet.Overlaps(OBSIDIAN_WEAPONS);
                bool anyShadowShining = weaponSet.Overlaps(SHADOW_SHINING_BLADE);
                HashSet<string> missingWeapons = new(TERMINAL_WEAPONS);
                missingWeapons.ExceptWith(weaponSet);
                foreach (string missingWeapon in missingWeapons)
                {
                    MoonoMod.Logger!.LogInfo($"missing {missingWeapon}");
                }

                return anyObsidian && anyShadowShining && (missingWeapons.Count == 0);
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
