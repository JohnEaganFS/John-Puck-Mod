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
        // Minimum linear speed required before drag is applied
        public static float MinSpeedForDrag = 25f;
        // Drag mode: 0 = none, 1 = linear, 2 = quadratic
        public static int DragMode = 2;
        // Quadratic coefficient (deceleration = C * speed^2)
        public static float QuadraticCoefficient = 0.01f;
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

                int mode = PuckDragHelpers.DragMode;
                Vector3 v = rb.linearVelocity;
                float speed = v.magnitude;
                // Only apply drag if current speed exceeds threshold
                if (speed <= PuckDragHelpers.MinSpeedForDrag) return;

                float dt = Time.fixedDeltaTime;

                if (mode == 1)
                {
                    // Linear multiplicative drag: v_new = v * (1 - k * dt)
                    float k = PuckDragHelpers.DragCoefficient;
                    if (k <= 0f) return;
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
                else if (mode == 2)
                {
                    // Quadratic drag: deceleration magnitude = C * speed^2
                    float C = PuckDragHelpers.QuadraticCoefficient;
                    if (C <= 0f) return;
                    // Compute reduction in speed over this timestep
                    float decel = C * speed * speed;
                    float newSpeed = speed - decel * dt;
                    if (newSpeed <= 0f)
                    {
                        rb.linearVelocity = Vector3.zero;
                    }
                    else
                    {
                        rb.linearVelocity = v.normalized * newSpeed;
                    }
                }
            }
            catch (Exception e)
            {
                try { Debug.LogException(e); } catch { }
            }
        }
    }
}
