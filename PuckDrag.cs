using System;
using HarmonyLib;
using UnityEngine;
using Unity.Netcode;

namespace OfficialPuckMod
{
    // Simple helper to hold runtime-configurable drag coefficient
    static class PuckDragHelpers
    {
        // Proportion of speed removed per second (e.g. 0.2 => ~18% lost per physics step at 50Hz)
        public static float DragCoefficient = 0.2f;
    }

    // Apply a linear drag to puck velocity before the original FixedUpdate runs
    [HarmonyPatch(typeof(Puck), "FixedUpdate")]
    static class Puck_FixedUpdate_Drag_Patch
    {
        static void Prefix(Puck __instance)
        {
            try
            {
                if (__instance == null) return;

                // Server authoritative change only
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

                // Avoid affecting replays or frozen pucks
                try { if (__instance.IsReplay != null && __instance.IsReplay.Value) return; } catch { }

                var rb = __instance.Rigidbody;
                if (rb == null) return;
                if (rb.isKinematic) return;

                float k = PuckDragHelpers.DragCoefficient;
                if (k <= 0f) return;

                float dt = Time.fixedDeltaTime;
                // Linear multiplicative drag: v_new = v * (1 - k * dt)
                Vector3 v = rb.linearVelocity;
                float factor = 1f - k * dt;
                if (factor <= 0f)
                {
                    rb.linearVelocity = Vector3.zero;
                }
                else
                {
                    rb.linearVelocity = v * factor;
                }
            }
            catch (Exception e)
            {
                try { Debug.LogException(e); } catch { }
            }
        }
    }
}
