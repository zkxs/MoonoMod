// This file is part of MoonoMod and is licenced under the GNU GPL v3.0.
// See LICENSE file for full text.
// Copyright © 2024 Michael Ripley

using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoonoMod
{
    // used to cache the initial parameters of an NPC before it was scaled, so that I can re-scale it later
    internal class NpcScalingDetails
    {
        // caches initial NPC scaling conditions 
        private readonly static ConditionalWeakTable<NPC_Scaling, NpcScalingDetails> npcScaleCache = new();

        public float initialHealth;
        public float lastScaleMultiplier = 1f;
        private readonly ConditionalWeakTable<OBJ_HEALTH, ValueWrapper<float>> objectHealthCache = new();
        private readonly ConditionalWeakTable<Damage_Trigger, ValueWrapper<float>> damageTriggerPowerCache = new();

        // set up intitial cache
        private NpcScalingDetails(NPC_Scaling npcScaling)
        {
            initialHealth = npcScaling.AI.health;

            if (npcScaling.CON == null)
            {
                npcScaling.CON = GameObject.Find("CONTROL").GetComponent<CONTROL>();
            }
            if (npcScaling.MOON == null)
            {
                npcScaling.MOON = GameObject.Find("CONTROL").GetComponent<SimpleMoon>();
            }
            if (npcScaling.BODY_PARTS.Length < 1)
            {
                npcScaling.BODY_PARTS = npcScaling.GetComponentsInChildren<OBJ_HEALTH>();
            }
            if (npcScaling.HURTS.Length < 1)
            {
                npcScaling.HURTS = npcScaling.GetComponentsInChildren<Damage_Trigger>();
            }

            foreach (OBJ_HEALTH objHealth in npcScaling.BODY_PARTS)
            {
                objectHealthCache.Add(objHealth, objHealth.Health);
            }
            foreach (Damage_Trigger damageTrigger in npcScaling.HURTS)
            {
                damageTriggerPowerCache.Add(damageTrigger, damageTrigger.power);
            }
        }

        // cached get of initial scaling details
        private static NpcScalingDetails CachedGet(NPC_Scaling npcScaling)
        {
            return npcScaleCache.GetValue(npcScaling, (npcScaling) => new(npcScaling));
        }

        public static void Rescale(NPC_Scaling npcScaling)
        {
            // calculate scaling details. This does a lot of weird internal caching.
            NpcScalingDetails scaleDetails = CachedGet(npcScaling);

            // calculate NPC scale multiplier
            float scaleMultiplier = Mathf.Lerp(1f, npcScaling.scale_str, npcScaling.MOON.MOON_MULT / 8f);

            // if new scale is not the same as the last scale, then we need to rescale the enemy
            if (!Mathf.Approximately(scaleMultiplier, scaleDetails.lastScaleMultiplier))
            {
                float newMaxHealth = scaleDetails.initialHealth * scaleMultiplier;

                npcScaling.AI.health = newMaxHealth * npcScaling.AI.health / npcScaling.AI.health_max;
                foreach (OBJ_HEALTH objHealth in npcScaling.BODY_PARTS)
                {
                    if (scaleDetails.objectHealthCache.TryGetValue(objHealth, out var initialObjectHealth))
                    {
                        float currentMaxObjectHealth = initialObjectHealth * scaleDetails.lastScaleMultiplier;
                        float newMaxObjectHealth = initialObjectHealth * scaleMultiplier;
                        objHealth.Health = (objHealth.Health / currentMaxObjectHealth) * newMaxObjectHealth;

                        // I'm aware that algebraically the above expressions simplify to the following expression, however I have concerns with floating point error accumulating, so I'm intentionally using the less simplified form
                        //objHealth.Health = objHealth.Health * scaleMultiplier / scaleDetails.lastScaleMultiplier;
                    }
                }
                foreach (Damage_Trigger damageTrigger in npcScaling.HURTS)
                {
                    if (scaleDetails.damageTriggerPowerCache.TryGetValue(damageTrigger, out var initialPower))
                    {
                        damageTrigger.power = initialPower * scaleMultiplier;
                    }
                }
                npcScaling.AI.health_max = newMaxHealth;

                if (MoonoMod.debugLogs?.Value ?? false)
                {
                    MoonoMod.Logger!.LogInfo($"Level {SceneManager.GetActiveScene().name} contains NPC {npcScaling.AI.gameObject.name} rescaled from {scaleDetails.lastScaleMultiplier}x to {scaleMultiplier}x. MOON_MULT={npcScaling.MOON.MOON_MULT}; health_max={npcScaling.AI.health_max}");
                }

                scaleDetails.lastScaleMultiplier = scaleMultiplier;
            }
            else if (MoonoMod.debugLogs?.Value ?? false)
            {
                MoonoMod.Logger!.LogInfo($"Level {SceneManager.GetActiveScene().name} contains NPC {npcScaling.AI.gameObject.name} that DID NOT need rescaling from {scaleDetails.lastScaleMultiplier}x to {scaleMultiplier}x. MOON_MULT={npcScaling.MOON.MOON_MULT}; health_max={npcScaling.AI.health_max}");
            }
        }
    }

    internal class ValueWrapper<T> where T : struct
    {
        public T value;

        public ValueWrapper(T value)
        {
            this.value = value;
        }

        public static implicit operator T(ValueWrapper<T> instance) => instance.value;
        public static implicit operator ValueWrapper<T>(T value) => new(value);

    }
}
