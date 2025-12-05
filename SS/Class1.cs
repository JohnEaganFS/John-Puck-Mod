using UnityEngine;
using Unity.Netcode;
using HarmonyLib;
using System;
using System.Linq;
using System.Collections.Generic;

namespace OfficialPuckMod
{
    // Your mod entry implementing IPuckMod
    public class OfficialPuckMod : IPuckMod
    {
        // <-- Field must be inside a class
        private static readonly Harmony harmony = new Harmony("John.OfficialPuckMod");

        // Configurable value: desired maximum output for StickPositioner
        // Lower this to slow how quickly the blade target/ratchet responds.
    public static float StickPositionerOutputMax = 1200f;
    // Configurable minimum output (negative direction)
    public static float StickPositionerOutputMin = -1200f;
        // Layer names (create these layers in Unity's Tags & Layers and set their indices)
        public static string LayerName_Puck = "Puck";
        public static string LayerName_PlayerBody = "PlayerBody";
        public static string LayerName_StickBlade = "StickBlade";
        public static string LayerName_StickShaft = "StickShaft";

        // Resolved layer indices (set at runtime)
        public static int Layer_Puck = -1;
        public static int Layer_PlayerBody = -1;
        public static int Layer_StickBlade = -1;
        public static int Layer_StickShaft = -1;
        // Test spawner GameObject (created on enable)
        private GameObject _testSpawnerGO;

        public bool OnEnable()
        {
            // Apply patches
            harmony.PatchAll();
            // Listen for game events that announce new objects so we can apply collision rules as things spawn
            try
            {
                var ev = MonoBehaviourSingleton<EventManager>.Instance;
                if (ev != null)
                {
                    ev.AddEventListener("Event_OnPuckSpawned", new Action<Dictionary<string, object>>(Event_OnPuckSpawned));
                    ev.AddEventListener("Event_OnPlayerBodySpawned", new Action<Dictionary<string, object>>(Event_OnPlayerBodySpawned));
                    ev.AddEventListener("Event_OnStickSpawned", new Action<Dictionary<string, object>>(Event_OnStickSpawned));
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            // If we're running on the server, immediately apply puck<->body collision rules to existing objects
            try
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                {
                    // Try to initialize layer-based rules; if successful, assign layers and set matrix.
                    bool layersOk = CollisionHelpers.InitializeLayerBasedCollisionMatrix();
                    if (layersOk)
                    {
                        CollisionHelpers.ApplyLayerAssignmentsToExistingObjects();
                    }
                    else
                    {
                        // Fallback: apply per-object collision ignores
                        CollisionHelpers.ApplyPuckBodyCollisionRulesToAll();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            // create a small runtime helper to spawn test sticks
            try
            {
                _testSpawnerGO = new GameObject("OfficialPuckMod_TestSpawner");
                _testSpawnerGO.AddComponent<TestStickSpawner>();
                UnityEngine.Object.DontDestroyOnLoad(_testSpawnerGO);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Debug.Log("[OfficialPuckMod] v0.3 Enabled.");
            return true;
        }

        public bool OnDisable()
        {
            // Remove patches applied by this Harmony instance
            try
            {
                var ev = MonoBehaviourSingleton<EventManager>.Instance;
                if (ev != null)
                {
                    ev.RemoveEventListener("Event_OnPuckSpawned", new Action<Dictionary<string, object>>(Event_OnPuckSpawned));
                    ev.RemoveEventListener("Event_OnPlayerBodySpawned", new Action<Dictionary<string, object>>(Event_OnPlayerBodySpawned));
                    ev.RemoveEventListener("Event_OnStickSpawned", new Action<Dictionary<string, object>>(Event_OnStickSpawned));
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            harmony.UnpatchSelf();
            try
            {
                if (_testSpawnerGO != null)
                {
                    UnityEngine.Object.Destroy(_testSpawnerGO);
                    _testSpawnerGO = null;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Debug.Log("[OfficialPuckMod] Disabled.");
            return true;
        }

    // Event handlers for runtime spawn events so collision rules are applied as objects join/spawn
    static void Event_OnPuckSpawned(Dictionary<string, object> message)
    {
        try
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (message == null || !message.ContainsKey("puck")) return;
            var puck = message["puck"] as Puck;
            if (puck == null) return;
            if (OfficialPuckMod.Layer_Puck >= 0)
            {
                CollisionHelpers.AssignPuckLayer(puck);
            }
            else
            {
                CollisionHelpers.DisablePuckPlayerCollisions(puck);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    static void Event_OnPlayerBodySpawned(Dictionary<string, object> message)
    {
        try
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (message == null || !message.ContainsKey("playerBody")) return;
            var body = message["playerBody"] as PlayerBodyV2;
            if (body == null) return;
            if (OfficialPuckMod.Layer_PlayerBody >= 0)
            {
                CollisionHelpers.AssignPlayerBodyLayer(body);
            }
            else
            {
                CollisionHelpers.DisablePlayerBodyPuckCollisions(body);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    static void Event_OnStickSpawned(Dictionary<string, object> message)
    {
        try
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (message == null || !message.ContainsKey("stick")) return;
            var stick = message["stick"] as Stick;
            if (stick == null) return;
            if (OfficialPuckMod.Layer_StickBlade >= 0 && OfficialPuckMod.Layer_StickShaft >= 0)
            {
                CollisionHelpers.AssignStickLayers(stick);
            }
            else
            {
                // Fallback: ensure stick-to-stick collider ignores are set for this new stick vs existing sticks
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
                            if (aBlade && bBlade)
                            {
                                Physics.IgnoreCollision(a, b, false);
                            }
                            else
                            {
                                Physics.IgnoreCollision(a, b, true);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // Patch Puck.OnNetworkPostSpawn (postfix) so we can disable puck->player collisions after spawn
    [HarmonyPatch(typeof(Puck), "OnNetworkPostSpawn")]
    static class Puck_OnNetworkPostSpawn_Patch
    {
        static void Postfix(Puck __instance)
        {
            try
            {
                // Only run on server (server-authoritative physics)
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
                // If layer-based mode is active, assign puck layer so matrix governs collisions
                if (OfficialPuckMod.Layer_Puck >= 0)
                {
                    CollisionHelpers.AssignPuckLayer(__instance);
                }
                else
                {
                    CollisionHelpers.DisablePuckPlayerCollisions(__instance);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    // Patch PlayerBodyV2.OnNetworkPostSpawn (postfix) so new player bodies ignore collisions with existing pucks
    [HarmonyPatch(typeof(PlayerBodyV2), "OnNetworkPostSpawn")]
    static class PlayerBodyV2_OnNetworkPostSpawn_Patch
    {
        static void Postfix(PlayerBodyV2 __instance)
        {
            try
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
                if (OfficialPuckMod.Layer_PlayerBody >= 0)
                {
                    CollisionHelpers.AssignPlayerBodyLayer(__instance);
                }
                else
                {
                    CollisionHelpers.DisablePlayerBodyPuckCollisions(__instance);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    // Helper functions used by the patches
    static class CollisionHelpers
    {
        public static void DisablePuckPlayerCollisions(Puck puck)
        {
            if (puck == null) return;
            var puckCols = puck.GetComponentsInChildren<Collider>(true);
            if (puckCols == null || puckCols.Length == 0) return;

            var pm = NetworkBehaviourSingleton<PlayerManager>.Instance;
            if (pm == null) return;

            foreach (var player in pm.GetPlayers(false))
            {
                if (player == null || player.PlayerBody == null) continue;

                // Skip goalies so goalies KEEP colliding with puck
                try
                {
                    if (player.Role.Value == PlayerRole.Goalie) continue;
                }
                catch { /* best-effort: if Role not ready, skip checking */ }

                var bodyCols = player.PlayerBody.GetComponentsInChildren<Collider>(true);
                if (bodyCols == null) continue;

                foreach (var bodyCol in bodyCols)
                {
                    // keep stick collisions
                    if (bodyCol.GetComponentInParent<Stick>() != null) continue;

                    foreach (var puckCol in puckCols)
                    {
                        if (puckCol != null && bodyCol != null)
                            Physics.IgnoreCollision(puckCol, bodyCol, true);
                    }
                }
            }
        }

        public static void DisablePlayerBodyPuckCollisions(PlayerBodyV2 body)
        {
            if (body == null) return;
            var bodyCols = body.GetComponentsInChildren<Collider>(true);
            if (bodyCols == null || bodyCols.Length == 0) return;

            var player = body.Player; // PlayerBodyV2.Player
            if (player != null)
            {
                try
                {
                    // If this body belongs to a goalie, don't ignore collisions
                    if (player.Role.Value == PlayerRole.Goalie) return;
                }
                catch { /* Role might not be initialized yet */ }
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
                        if (puckCol != null && bodyCol != null)
                            Physics.IgnoreCollision(puckCol, bodyCol, true);
                    }
                }
            }
        }

        // Apply puck<->body collision rules to all currently-existing objects (server-only)
        public static void ApplyPuckBodyCollisionRulesToAll()
        {
            try
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

                // Apply for all existing pucks
                var allPucks = UnityEngine.Object.FindObjectsOfType<Puck>();
                if (allPucks != null)
                {
                    foreach (var puck in allPucks)
                    {
                        if (puck == null) continue;
                        DisablePuckPlayerCollisions(puck);
                    }
                }

                // Apply for all existing player bodies
                var pm = NetworkBehaviourSingleton<PlayerManager>.Instance;
                if (pm != null)
                {
                    foreach (var player in pm.GetPlayers(false))
                    {
                        if (player == null || player.PlayerBody == null) continue;
                        DisablePlayerBodyPuckCollisions(player.PlayerBody);
                    }
                }

                Debug.Log("[OfficialPuckMod] Applied puck<->body collision rules to existing objects (server).");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // Try to initialize layer indices and configure Physics collision matrix.
        // Returns true if layer names were found and matrix applied.
        public static bool InitializeLayerBasedCollisionMatrix()
        {
            try
            {
                // Resolve layer indices by name
                OfficialPuckMod.Layer_Puck = LayerMask.NameToLayer(OfficialPuckMod.LayerName_Puck);
                OfficialPuckMod.Layer_PlayerBody = LayerMask.NameToLayer(OfficialPuckMod.LayerName_PlayerBody);
                OfficialPuckMod.Layer_StickBlade = LayerMask.NameToLayer(OfficialPuckMod.LayerName_StickBlade);
                OfficialPuckMod.Layer_StickShaft = LayerMask.NameToLayer(OfficialPuckMod.LayerName_StickShaft);

                if (OfficialPuckMod.Layer_Puck < 0 || OfficialPuckMod.Layer_PlayerBody < 0 || OfficialPuckMod.Layer_StickBlade < 0 || OfficialPuckMod.Layer_StickShaft < 0)
                {
                    Debug.LogWarning("[OfficialPuckMod] One or more configured layer names were not found. Layer-based collision will be disabled and fallback logic will be used.");
                    return false;
                }

                // Configure collision matrix:
                // - Puck should NOT collide with PlayerBody
                // - Puck should NOT collide with StickShaft
                // - StickShaft should NOT collide with StickShaft or StickBlade
                // - StickBlade should collide with StickBlade (ensure enabled)
                Physics.IgnoreLayerCollision(OfficialPuckMod.Layer_Puck, OfficialPuckMod.Layer_PlayerBody, true);
                Physics.IgnoreLayerCollision(OfficialPuckMod.Layer_Puck, OfficialPuckMod.Layer_StickShaft, true);

                Physics.IgnoreLayerCollision(OfficialPuckMod.Layer_StickShaft, OfficialPuckMod.Layer_StickShaft, true);
                Physics.IgnoreLayerCollision(OfficialPuckMod.Layer_StickShaft, OfficialPuckMod.Layer_StickBlade, true);
                Physics.IgnoreLayerCollision(OfficialPuckMod.Layer_StickBlade, OfficialPuckMod.Layer_StickBlade, false);

                Debug.Log("[OfficialPuckMod] Layer-based collision matrix configured.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        // Assign layers to existing objects (pucks, player bodies, sticks) so layer matrix takes effect
        public static void ApplyLayerAssignmentsToExistingObjects()
        {
            try
            {
                // Pucks
                var allPucks = UnityEngine.Object.FindObjectsOfType<Puck>();
                if (allPucks != null)
                {
                    foreach (var puck in allPucks)
                    {
                        if (puck == null) continue;
                        AssignPuckLayer(puck);
                    }
                }

                // Player bodies
                var pm = NetworkBehaviourSingleton<PlayerManager>.Instance;
                if (pm != null)
                {
                    foreach (var player in pm.GetPlayers(false))
                    {
                        if (player == null || player.PlayerBody == null) continue;
                        AssignPlayerBodyLayer(player.PlayerBody);
                    }
                }

                // Sticks
                var allSticks = UnityEngine.Object.FindObjectsOfType<Stick>();
                if (allSticks != null)
                {
                    foreach (var stick in allSticks)
                    {
                        if (stick == null) continue;
                        AssignStickLayers(stick);
                    }
                }

                Debug.Log("[OfficialPuckMod] Applied layer assignments to existing objects.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

    public static void AssignPuckLayer(Puck puck)
        {
            if (puck == null) return;
            try
            {
                SetLayerRecursively(puck.gameObject, OfficialPuckMod.Layer_Puck);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

    public static void AssignPlayerBodyLayer(PlayerBodyV2 body)
        {
            if (body == null) return;
            try
            {
                SetLayerRecursively(body.gameObject, OfficialPuckMod.Layer_PlayerBody);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

    public static void AssignStickLayers(Stick stick)
        {
            if (stick == null) return;
            try
            {
                var cols = stick.GetComponentsInChildren<Collider>(true);
                if (cols == null) return;
                foreach (var c in cols)
                {
                    if (c == null) continue;
                    if (c.tag == "Stick Blade") c.gameObject.layer = OfficialPuckMod.Layer_StickBlade;
                    else if (c.tag == "Stick Shaft") c.gameObject.layer = OfficialPuckMod.Layer_StickShaft;
                }
                // Also set stick root to some default (optional)
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        static void SetLayerRecursively(GameObject go, int layer)
        {
            if (go == null) return;
            try
            {
                go.layer = layer;
                foreach (Transform child in go.transform)
                {
                    SetLayerRecursively(child.gameObject, layer);
                }
            }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    // Patch StickPositioner.OnNetworkPostSpawn to apply our configured outputMax
    [HarmonyPatch]
    static class StickPositioner_ApplyConfig_Patch
    {
        static System.Reflection.FieldInfo GetPrivateField(Type t, string name)
        {
            return t.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
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
                    fiMax.SetValue(__instance, OfficialPuckMod.StickPositionerOutputMax);
                }
                else
                {
                    var piMax = t.GetProperty("outputMax", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                    if (piMax != null && piMax.CanWrite)
                    {
                        piMax.SetValue(__instance, OfficialPuckMod.StickPositionerOutputMax);
                    }
                }

                var fiMin = GetPrivateField(t, "outputMin");
                if (fiMin != null)
                {
                    fiMin.SetValue(__instance, OfficialPuckMod.StickPositionerOutputMin);
                }
                else
                {
                    var piMin = t.GetProperty("outputMin", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                    if (piMin != null && piMin.CanWrite)
                    {
                        piMin.SetValue(__instance, OfficialPuckMod.StickPositionerOutputMin);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
    
    // Patch Stick.OnNetworkPostSpawn to ensure sticks only collide blade-to-blade
    [HarmonyPatch(typeof(Stick), "OnNetworkPostSpawn")]
    static class Stick_CollisionSetup_Patch
    {
        static void Postfix(Stick __instance)
        {
            try
            {
                if (__instance == null) return;

                // If layer-based mode is active, assign stick colliders to the configured layers
                if (OfficialPuckMod.Layer_StickBlade >= 0 && OfficialPuckMod.Layer_StickShaft >= 0)
                {
                    CollisionHelpers.AssignStickLayers(__instance);
                    return;
                }

                // Fallback: per-collider ignore logic (existing behavior)
                var myCols = __instance.GetComponentsInChildren<Collider>(true);
                if (myCols == null || myCols.Length == 0) return;

                var allSticks = UnityEngine.Object.FindObjectsOfType<Stick>();
                foreach (var other in allSticks)
                {
                    if (other == null || other == __instance) continue;

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

                            if (aBlade && bBlade)
                            {
                                Physics.IgnoreCollision(a, b, false);
                            }
                            else
                            {
                                Physics.IgnoreCollision(a, b, true);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}}