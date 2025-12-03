using System;
using HarmonyLib;
using UnityEngine;

namespace OfficialPuckMod
{
    // Logs the linear velocity transfer impulses and angular transfer applied by Stick.FixedUpdate
    [HarmonyPatch]
    static class Stick_DebugForces_Patch
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var t = AccessTools.TypeByName("Stick");
            if (t == null) return null;
            return AccessTools.Method(t, "FixedUpdate");
        }

        static void Postfix(object __instance)
        {
            try
            {
                var stick = __instance as Stick;
                if (stick == null) return;
                if (stick.PlayerBody == null || stick.Rigidbody == null) return;

                var t = stick.GetType();
                var fiLv = t.GetField("linearVelocityTransferMultiplier", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                float lv = fiLv != null ? (float)fiLv.GetValue(stick) : float.NaN;
                var fiAng = t.GetField("angularVelocityTransferMultiplier", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                float angMul = fiAng != null ? (float)fiAng.GetValue(stick) : float.NaN;

                Vector3 vShaft = Vector3.zero;
                Vector3 vBlade = Vector3.zero;
                try { vShaft = stick.PlayerBody.Rigidbody.GetPointVelocity(stick.ShaftHandlePosition) * lv * Time.fixedDeltaTime; } catch { }
                try { vBlade = stick.PlayerBody.Rigidbody.GetPointVelocity(stick.BladeHandlePosition) * lv * Time.fixedDeltaTime; } catch { }

                Vector3 a3 = Vector3.Scale(stick.Rigidbody.angularVelocity, new Vector3(0.5f, 1f, 0f)) * angMul;

                // If the values are very small, skip logging
                if (vShaft.sqrMagnitude < 1e-6f && vBlade.sqrMagnitude < 1e-6f && a3.sqrMagnitude < 1e-6f)
                {
                    return;
                }

                Debug.Log($"[StickDebug] Stick='{stick.name}' shaftTransfer={vShaft} bladeTransfer={vBlade} lv={lv} angularTransfer={a3} angMul={angMul} rb.angularVelocity={stick.Rigidbody.angularVelocity}");
            }
            catch (Exception e)
            {
                try { Debug.LogException(e); } catch { }
            }
        }
    }
}
