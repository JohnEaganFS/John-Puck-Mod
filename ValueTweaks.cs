using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using HarmonyLib;
using Unity.Netcode;

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
        // (Removed) Stick linear/angular velocity transfer multipliers temporarily.
        // Configurable soft collision force used by `StickPositioner.ApplySoftCollision` when hitting "Soft Collider".
        // Default matches the serialized default in `StickPositioner` (1.0f).
        public static float SoftCollisionForce = 5f;

        public static void Init()
        {
            try
            {
                harmony.PatchAll();
                // (Previously applied multipliers to existing Sticks) removed for now.
                // Ensure existing StickPositioner instances pick up configured softCollisionForce
                try { ApplyToExistingStickPositioners(); } catch (Exception e) { Debug.LogException(e); }
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

        // (Removed) Helper to apply configured multipliers to already-spawned Stick instances

        // Helper to apply configured softCollisionForce to already-spawned StickPositioner instances
        public static void ApplyToExistingStickPositioners()
        {
            return;
            // try
            // {
            //     if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            //     var all = UnityEngine.Object.FindObjectsOfType<StickPositioner>();
            //     if (all == null) return;
            //     foreach (var sp in all)
            //     {
            //         if (sp == null) continue;
            //         try
            //         {
            //             var t = sp.GetType();
            //             var fi = t.GetField("softCollisionForce", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            //             if (fi != null) fi.SetValue(sp, ValueTweaksHelpers.SoftCollisionForce);
            //             else
            //             {
            //                 var pi = t.GetProperty("softCollisionForce", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            //                 if (pi != null && pi.CanWrite) pi.SetValue(sp, ValueTweaksHelpers.SoftCollisionForce);
            //             }
            //         }
            //         catch (Exception e)
            //         {
            //             Debug.LogException(e);
            //         }
            //     }
            //     Debug.Log($"[ValueTweaks] Applied SoftCollisionForce to {all.Length} existing StickPositioner instances.");
            // }
            // catch (Exception e)
            // {
            //     Debug.LogException(e);
            // }
        }
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

    // (Removed) Stick.OnNetworkPostSpawn patch for linear/angular multipliers

    // Patch StickPositioner.OnNetworkPostSpawn to apply configured SoftCollisionForce
    [HarmonyPatch]
    static class StickPositioner_SoftCollision_Patch
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var t = AccessTools.TypeByName("StickPositioner");
            if (t == null) return null;
            return AccessTools.Method(t, "OnNetworkPostSpawn");
        }

        static void Postfix(object __instance)
        {
            return;
            // try
            // {
            //     if (__instance == null) return;
            //     // Only apply this on the server (physics authority)
            //     if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            //     var t = __instance.GetType();
            //     var fi = t.GetField("softCollisionForce", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            //     if (fi != null)
            //     {
            //         fi.SetValue(__instance, ValueTweaksHelpers.SoftCollisionForce);
            //     }
            //     else
            //     {
            //         var pi = t.GetProperty("softCollisionForce", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            //         if (pi != null && pi.CanWrite) pi.SetValue(__instance, ValueTweaksHelpers.SoftCollisionForce);
            //     }
            //     try { Debug.Log($"[ValueTweaks] Applied SoftCollisionForce={ValueTweaksHelpers.SoftCollisionForce} to StickPositioner instance={__instance}"); } catch { }
            // }
            // catch (Exception e)
            // {
            //     try { Debug.LogException(e); } catch { }
            // }
        }
    }

}
