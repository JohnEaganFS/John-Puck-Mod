using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using HarmonyLib;

namespace OfficialPuckMod
{
    // Patch Movement.Move so airborne players still get overspeed drag applied
    [HarmonyPatch(typeof(Movement), "Move")]
    static class Movement_Move_Patch
    {
        static bool Prefix(Movement __instance)
        {
            try
            {
                if (__instance == null) return true;
                // If player is not grounded, apply overspeed drag (if over max) and skip original Move
                var hover = __instance.Hover;
                if (hover != null && !hover.IsGrounded)
                {
                    try
                    {
                        if (__instance.Speed > __instance.MaximumSpeed)
                        {
                            // read private field overspeedDrag via reflection
                            var fi = typeof(Movement).GetField("overspeedDrag", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                            float overspeedDrag = 0.025f;
                            if (fi != null)
                            {
                                try { overspeedDrag = (float)fi.GetValue(__instance); } catch { }
                            }
                            var rb = __instance.Rigidbody;
                            if (rb != null)
                            {
                                rb.linearVelocity *= 1f - overspeedDrag * Time.fixedDeltaTime;
                            }
                        }
                    }
                    catch (Exception) { }

                    // Skip original Move since we handled airborne overspeed
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            return true;
        }
    }
}
