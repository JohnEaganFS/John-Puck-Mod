using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using HarmonyLib;
using System.Reflection;

namespace OfficialPuckMod
{
    // Mod entry implementing IPuckMod. Follows the same OnEnable/OnDisable pattern as Class1.cs
    public class DisableCollisionsMod : IPuckMod
    {
        // Keeps Harmony concerns local to the helper, but expose a quick patch call here to mirror Class1.cs
        public bool OnEnable()
        {
            try
            {
                DisableCollisionHelpers.Init();
                DisableCollisionHelpers.RegisterEventListeners();

                // Ensure ValueTweaks (puck/goal scaling) is initialized as well
                try { ValueTweaksHelpers.Init(); } catch (Exception e) { Debug.LogException(e); }
                // Ensure FaceoffTweaks is initialized when this assembly's single IPuckMod is loaded
                try { FaceoffTweaksHelpers.Init(); } catch (Exception e) { Debug.LogException(e); }

                // Apply rules immediately to existing objects if we're the server
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                {
                    DisableCollisionHelpers.ApplyRulesToExistingObjects();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            Debug.Log("[DisableCollisionsMod] 20 Enabled.");
            return true;
        }

        public bool OnDisable()
        {
            try
            {
                DisableCollisionHelpers.UnregisterEventListeners();
                DisableCollisionHelpers.Shutdown();
                try { ValueTweaksHelpers.Shutdown(); } catch (Exception e) { Debug.LogException(e); }
                try { FaceoffTweaksHelpers.Shutdown(); } catch (Exception e) { Debug.LogException(e); }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            Debug.Log("[DisableCollisionsMod] Disabled.");
            return true;
        }
    }

    // Static helper class that actually implements the collision logic and Harmony patches
    static class DisableCollisionHelpers
    {
        internal static readonly Harmony harmony = new Harmony("John.OfficialPuckMod.DisableCollisions");

        // Configurable stick output limits (applied via Harmony patch on StickPositioner)
        public static float StickPositionerOutputMax = 1200f;
        public static float StickPositionerOutputMin = -1200f;

    // Layer-based logic removed; this helper now uses only per-collider ignore logic.

        public static void Init()
        {
            try
            {
                harmony.PatchAll();
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

        public static void RegisterEventListeners()
        {
            try
            {
                var ev = MonoBehaviourSingleton<EventManager>.Instance;
                if (ev != null)
                {
                    ev.AddEventListener("Event_OnPuckSpawned", new Action<Dictionary<string, object>>(OnPuckSpawned));
                    ev.AddEventListener("Event_OnPlayerBodySpawned", new Action<Dictionary<string, object>>(OnPlayerBodySpawned));
                    ev.AddEventListener("Event_OnStickSpawned", new Action<Dictionary<string, object>>(OnStickSpawned));
                    Debug.Log("[DisableCollisionHelpers] Registered EventManager listeners.");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public static void UnregisterEventListeners()
        {
            try
            {
                var ev = MonoBehaviourSingleton<EventManager>.Instance;
                if (ev != null)
                {
                    ev.RemoveEventListener("Event_OnPuckSpawned", new Action<Dictionary<string, object>>(OnPuckSpawned));
                    ev.RemoveEventListener("Event_OnPlayerBodySpawned", new Action<Dictionary<string, object>>(OnPlayerBodySpawned));
                    ev.RemoveEventListener("Event_OnStickSpawned", new Action<Dictionary<string, object>>(OnStickSpawned));
                    Debug.Log("[DisableCollisionHelpers] Unregistered EventManager listeners.");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public static void ApplyRulesToExistingObjects()
        {
            try
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

                // Per-collider fallback for all existing objects
                var allPucks = UnityEngine.Object.FindObjectsOfType<Puck>();
                if (allPucks != null)
                {
                    foreach (var puck in allPucks) if (puck != null) DisablePuckPlayerCollisions(puck);
                }

                var pm = NetworkBehaviourSingleton<PlayerManager>.Instance;
                if (pm != null)
                {
                    foreach (var player in pm.GetPlayers(false)) if (player != null && player.PlayerBody != null) DisablePlayerBodyPuckCollisions(player.PlayerBody);
                }

                var allSticks = UnityEngine.Object.FindObjectsOfType<Stick>();
                if (allSticks != null)
                {
                    foreach (var stick in allSticks) if (stick != null) ApplyStickRules(stick);
                }

                Debug.Log("[DisableCollisionHelpers] Applied rules to existing objects (per-collider).");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // Layer-based initialization removed. This project uses per-collider ignores only.

        // Event handlers
        private static void OnPuckSpawned(Dictionary<string, object> message)
        {
            try
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
                if (message == null || !message.ContainsKey("puck")) return;
                var puck = message["puck"] as Puck;
                if (puck == null) return;
                Debug.Log("[DisableCollisionHelpers] Event: PuckSpawned");
                ApplyPuckRules(puck);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        private static void OnPlayerBodySpawned(Dictionary<string, object> message)
        {
            try
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
                if (message == null || !message.ContainsKey("playerBody")) return;
                var body = message["playerBody"] as PlayerBodyV2;
                if (body == null) return;
                Debug.Log("[DisableCollisionHelpers] Event: PlayerBodySpawned");
                ApplyPlayerBodyRules(body);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        private static void OnStickSpawned(Dictionary<string, object> message)
        {
            try
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
                if (message == null || !message.ContainsKey("stick")) return;
                var stick = message["stick"] as Stick;
                if (stick == null) return;
                Debug.Log("[DisableCollisionHelpers] Event: StickSpawned");
                ApplyStickRules(stick);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        // Harmony postfix patches (also ensure behavior if EventManager isn't available)
        [HarmonyPatch(typeof(Puck), "OnNetworkPostSpawn")]
        static class Puck_OnNetworkPostSpawn_Patch
        {
            static void Postfix(Puck __instance)
            {
                try
                {
                    if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
                    ApplyPuckRules(__instance);
                }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        [HarmonyPatch(typeof(PlayerBodyV2), "OnNetworkPostSpawn")]
        static class PlayerBody_OnNetworkPostSpawn_Patch
        {
            static void Postfix(PlayerBodyV2 __instance)
            {
                try
                {
                    if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
                    ApplyPlayerBodyRules(__instance);
                }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        [HarmonyPatch(typeof(Stick), "OnNetworkPostSpawn")]
        static class Stick_OnNetworkPostSpawn_Patch
        {
            static void Postfix(Stick __instance)
            {
                try
                {
                    if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
                    ApplyStickRules(__instance);
                }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        // Apply rules for a single instance (per-collider only)
        private static void ApplyPuckRules(Puck puck)
        {
            if (puck == null) return;
            DisablePuckPlayerCollisions(puck);
        }

        private static void ApplyPlayerBodyRules(PlayerBodyV2 body)
        {
            if (body == null) return;
            DisablePlayerBodyPuckCollisions(body);
        }

        private static void ApplyStickRules(Stick stick)
        {
            if (stick == null) return;
            var myCols = stick.GetComponentsInChildren<Collider>(true);
            if (myCols == null || myCols.Length == 0) return;
            var allSticks = UnityEngine.Object.FindObjectsOfType<Stick>();
            foreach (var other in allSticks)
            {
                if (other == null || other == stick) continue;
                var otherCols = other.GetComponentsInChildren<Collider>(true);
                if (otherCols == null || otherCols.Length == 0) continue;
                foreach (var a in myCols)
                {
                    if (a == null) continue;
                    bool aBlade = a.tag == "Stick Blade";
                    foreach (var b in otherCols)
                    {
                        if (b == null) continue;
                        bool bBlade = b.tag == "Stick Blade";
                        if (aBlade && bBlade) Physics.IgnoreCollision(a, b, false);
                        else Physics.IgnoreCollision(a, b, true);
                    }
                }
            }
        }

        // Collision helpers (puck <-> body)
        private static void DisablePuckPlayerCollisions(Puck puck)
        {
            if (puck == null) return;
            var puckCols = puck.GetComponentsInChildren<Collider>(true);
            if (puckCols == null || puckCols.Length == 0) return;
            var pm = NetworkBehaviourSingleton<PlayerManager>.Instance;
            if (pm == null) return;
            foreach (var player in pm.GetPlayers(false))
            {
                if (player == null || player.PlayerBody == null) continue;
                try { if (player.Role.Value == PlayerRole.Goalie) continue; } catch { }
                var bodyCols = player.PlayerBody.GetComponentsInChildren<Collider>(true);
                if (bodyCols == null) continue;
                foreach (var bodyCol in bodyCols)
                {
                    if (bodyCol.GetComponentInParent<Stick>() != null) continue;
                    foreach (var puckCol in puckCols)
                    {
                        if (puckCol != null && bodyCol != null) Physics.IgnoreCollision(puckCol, bodyCol, true);
                    }
                }
            }
        }

        private static void DisablePlayerBodyPuckCollisions(PlayerBodyV2 body)
        {
            if (body == null) return;
            var bodyCols = body.GetComponentsInChildren<Collider>(true);
            if (bodyCols == null || bodyCols.Length == 0) return;
            var player = body.Player;
            try { Debug.Log("[DisableCollisionHelpers] Disabling puck collisions for player body"); } catch { }
            if (player != null)
            {
                try { if (player.Role.Value == PlayerRole.Goalie) return; } catch { }
            }
            var puckManager = NetworkBehaviourSingleton<PuckManager>.Instance;
            if (puckManager == null) return;
            foreach (var puck in puckManager.GetPucks(false))
            {
                if (puck == null) continue;
                var puckCols = puck.GetComponentsInChildren<Collider>(true);
                if (puckCols == null) continue;
                foreach (var bodyCol in bodyCols)
                {
                    if (bodyCol.GetComponentInParent<Stick>() != null) continue;
                    foreach (var puckCol in puckCols)
                    {
                        if (puckCol != null && bodyCol != null) Physics.IgnoreCollision(puckCol, bodyCol, true);
                    }
                }
            }
            try { Debug.Log("[DisableCollisionHelpers] Done disabling collisions for player body"); } catch { }
        }

        // Layer helper methods removed; per-collider logic only.
    }

    // Patch StickPositioner.OnNetworkPostSpawn to apply configured outputMax/outputMin
    [HarmonyPatch]
    static class StickPositioner_ApplyConfig_Patch
    {
        static FieldInfo GetPrivateField(Type t, string name)
        {
            return t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        }

        // Target the method by name on the type so the patch will apply even if overloads change
        static System.Reflection.MethodBase TargetMethod()
        {
            var t = AccessTools.TypeByName("StickPositioner");
            if (t == null) return null;
            return AccessTools.Method(t, "OnNetworkPostSpawn");
        }

        static void Postfix(object __instance)
        {
            try
            {
                if (__instance == null) return;
                var t = __instance.GetType();
                // Try to set both outputMax and outputMin private fields; fall back to properties if needed
                var fiMax = GetPrivateField(t, "outputMax");
                if (fiMax != null)
                {
                    fiMax.SetValue(__instance, DisableCollisionHelpers.StickPositionerOutputMax);
                }
                else
                {
                    var piMax = t.GetProperty("outputMax", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (piMax != null && piMax.CanWrite)
                    {
                        piMax.SetValue(__instance, DisableCollisionHelpers.StickPositionerOutputMax);
                    }
                }

                var fiMin = GetPrivateField(t, "outputMin");
                if (fiMin != null)
                {
                    fiMin.SetValue(__instance, DisableCollisionHelpers.StickPositionerOutputMin);
                }
                else
                {
                    var piMin = t.GetProperty("outputMin", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (piMin != null && piMin.CanWrite)
                    {
                        piMin.SetValue(__instance, DisableCollisionHelpers.StickPositionerOutputMin);
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