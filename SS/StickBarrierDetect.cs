using System;
using HarmonyLib;
using UnityEngine;

namespace OfficialPuckMod
{
    [HarmonyPatch]
    static class Stick_OnCollisionStay_BarrierDetect_Patch
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var t = AccessTools.TypeByName("Stick");
            if (t == null) return null;
            return AccessTools.Method(t, "OnCollisionStay");
        }

        static void Postfix(object __instance, object collision)
        {
            try
            {
                Debug.Log("[StickDetect] OnCollisionStay triggered.");
                // defensive typed access
                var stick = __instance as UnityEngine.Object;
                if (collision == null) { Debug.Log("Collision is null"); return; }
                var col = collision as UnityEngine.Collision;
                if (col == null) { Debug.Log("Collision is not of type UnityEngine.Collision"); return; }

                foreach (var cp in col.contacts)
                {
                    var other = cp.otherCollider;
                    var thisCol = cp.thisCollider;
                    if (other == null) continue;

                    string stickName = "<unknown>";
                    string owner = "<no-player>";
                    try
                    {
                        // try to get instance info if it's a Stick type
                        var stickObj = __instance as Stick;
                        if (stickObj != null)
                        {
                            stickName = stickObj.name ?? "<stick>";
                            try { owner = (stickObj.Player != null ? stickObj.Player.Username.Value.ToString() : "<no-player>"); } catch { }
                        }
                    }
                    catch { }

                    string otherTag = "<no-tag>";
                    string otherName = "<no-name>";
                    string thisTag = "<no-tag>";
                    string thisName = "<no-name>";
                    try { otherTag = other.tag; } catch { }
                    try { otherName = other.name ?? other.gameObject?.name ?? "<no-name>"; } catch { }
                    try { if (thisCol != null) { thisTag = thisCol.tag; thisName = thisCol.name; } } catch { }

                    Debug.Log($"[StickDetect] Stick='{stickName}' owner='{owner}' thisCollider='{thisName}'(tag='{thisTag}') touched collider='{otherName}'(tag='{otherTag}')");
                    break;
                }
            }
            catch (Exception e)
            {
                try { Debug.LogException(e); } catch { }
            }
        }
    }

    // Also patch OnCollisionEnter to catch first-frame contacts
    [HarmonyPatch]
    static class Stick_OnCollisionEnter_BarrierDetect_Patch
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var t = AccessTools.TypeByName("Stick");
            if (t == null) return null;
            return AccessTools.Method(t, "OnCollisionEnter");
        }

        static void Postfix(object __instance, object collision)
        {
            try
            {
                Debug.Log("[StickDetect] OnCollisionEnter triggered.");
                var col = collision as UnityEngine.Collision;
                if (col == null) return;
                // reuse logging logic by iterating contacts
                foreach (var cp in col.contacts)
                {
                    var other = cp.otherCollider;
                    var thisCol = cp.thisCollider;
                    if (other == null) continue;

                    string stickName = "<unknown>";
                    string owner = "<no-player>";
                    try
                    {
                        var stickObj = __instance as Stick;
                        if (stickObj != null)
                        {
                            stickName = stickObj.name ?? "<stick>";
                            try { owner = (stickObj.Player != null ? stickObj.Player.Username.Value.ToString() : "<no-player>"); } catch { }
                        }
                    }
                    catch { }

                    string otherTag = "<no-tag>";
                    string otherName = "<no-name>";
                    string thisTag = "<no-tag>";
                    string thisName = "<no-name>";
                    try { otherTag = other.tag; } catch { }
                    try { otherName = other.name ?? other.gameObject?.name ?? "<no-name>"; } catch { }
                    try { if (thisCol != null) { thisTag = thisCol.tag; thisName = thisCol.name; } } catch { }

                    Debug.Log($"[StickDetect][Enter] Stick='{stickName}' owner='{owner}' thisCollider='{thisName}'(tag='{thisTag}') touched collider='{otherName}'(tag='{otherTag}')");
                    break;
                }
            }
            catch (Exception e)
            {
                try { Debug.LogException(e); } catch { }
            }
        }
    }

    // Patch trigger-based contacts as well
    [HarmonyPatch]
    static class Stick_OnTriggerEnter_BarrierDetect_Patch
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var t = AccessTools.TypeByName("Stick");
            if (t == null) return null;
            return AccessTools.Method(t, "OnTriggerEnter");
        }

        static void Postfix(object __instance, object other)
        {
            try
            {
                Debug.Log("[StickDetect] OnTriggerEnter triggered.");
                var col = other as UnityEngine.Collider;
                if (col == null) return;

                string stickName = "<unknown>";
                string owner = "<no-player>";
                try
                {
                    var stickObj = __instance as Stick;
                    if (stickObj != null)
                    {
                        stickName = stickObj.name ?? "<stick>";
                        try { owner = (stickObj.Player != null ? stickObj.Player.Username.Value.ToString() : "<no-player>"); } catch { }
                    }
                }
                catch { }

                string otherTag = "<no-tag>";
                string otherName = "<no-name>";
                try { otherTag = col.tag; } catch { }
                try { otherName = col.name ?? col.gameObject?.name ?? "<no-name>"; } catch { }

                Debug.Log($"[StickDetect][TriggerEnter] Stick='{stickName}' owner='{owner}' touched trigger collider='{otherName}'(tag='{otherTag}')");
            }
            catch (Exception e)
            {
                try { Debug.LogException(e); } catch { }
            }
        }
    }

    [HarmonyPatch]
    static class Stick_OnTriggerStay_BarrierDetect_Patch
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var t = AccessTools.TypeByName("Stick");
            if (t == null) return null;
            return AccessTools.Method(t, "OnTriggerStay");
        }

        static void Postfix(object __instance, object other)
        {
            try
            {
                Debug.Log("[StickDetect] OnTriggerStay triggered.");
                var col = other as UnityEngine.Collider;
                if (col == null) return;

                string stickName = "<unknown>";
                string owner = "<no-player>";
                try
                {
                    var stickObj = __instance as Stick;
                    if (stickObj != null)
                    {
                        stickName = stickObj.name ?? "<stick>";
                        try { owner = (stickObj.Player != null ? stickObj.Player.Username.Value.ToString() : "<no-player>"); } catch { }
                    }
                }
                catch { }

                string otherTag = "<no-tag>";
                string otherName = "<no-name>";
                try { otherTag = col.tag; } catch { }
                try { otherName = col.name ?? col.gameObject?.name ?? "<no-name>"; } catch { }

                Debug.Log($"[StickDetect][TriggerStay] Stick='{stickName}' owner='{owner}' touching trigger collider='{otherName}'(tag='{otherTag}')");
            }
            catch (Exception e)
            {
                try { Debug.LogException(e); } catch { }
            }
        }
    }
}
