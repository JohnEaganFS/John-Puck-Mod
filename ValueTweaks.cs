using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using HarmonyLib;

namespace OfficialPuckMod
{
    // Mod entry to hold value tweaks (puck scale, future values)
    public class ValueTweaksMod : IPuckMod
    {
        public bool OnEnable()
        {
            try
            {
                ValueTweaksHelpers.Init();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Debug.Log("[ValueTweaksMod] Enabled.");
            return true;
        }

        public bool OnDisable()
        {
            try
            {
                ValueTweaksHelpers.Shutdown();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Debug.Log("[ValueTweaksMod] Disabled.");
            return true;
        }
    }

    static class ValueTweaksHelpers
    {
        internal static readonly Harmony harmony = new Harmony("John.OfficialPuckMod.ValueTweaks");

    // Configurable puck scale (1.0 = default). Change this to make pucks bigger/smaller.
    public static float PuckScale = 0.92f;

        public static void Init()
        {
            try
            {
                harmony.PatchAll();
                // No goal scaling configured; only puck scaling is active.
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public static void Shutdown()
        {
            try
            {
                harmony.UnpatchSelf();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // No goal-scaling helpers in this module at the moment.
    }

    // Patch Puck.OnNetworkPostSpawn to apply the configured scale so colliders and visuals match
    [HarmonyPatch(typeof(Puck), "OnNetworkPostSpawn")]
    static class Puck_Scale_Patch
    {
        static void Postfix(Puck __instance)
        {
            try
            {
                if (__instance == null) return;

                // Apply scale on both server and client so visuals and colliders match
                float s = ValueTweaksHelpers.PuckScale;
                if (s <= 0f) s = 1f; // guard

                // Set localScale; SphereCollider and other colliders are in local space so this scales them too
                __instance.transform.localScale = Vector3.one * s;

                // If the puck has a serialized netSphereCollider, we may wish to adjust its base radius if needed.
                // The collider's radius is in local space so scaling the transform is generally sufficient.
            }
            catch (Exception e)
            {
                try { Debug.LogException(e); } catch { }
            }
        }
    }

    // (Goal-instantiation scaling removed)

    // (GoalController scaling patch removed)

    // (Goal.Client_AddNetClothSphereCollider patch removed)

}
