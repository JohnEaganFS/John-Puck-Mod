using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace OfficialPuckMod
{
    static class BoardBounceTweaksHelpers
    {
        // Initialize any state if needed
        public static void Init()
        {
            // no-op for now
        }

        public static void Shutdown()
        {
            // no-op for now
        }

        public static void RegisterEventListeners()
        {
            try
            {
                var ev = MonoBehaviourSingleton<EventManager>.Instance;
                if (ev != null)
                {
                    ev.AddEventListener("Event_OnStickSpawned", new Action<Dictionary<string, object>>(OnStickSpawned));
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
                    ev.RemoveEventListener("Event_OnStickSpawned", new Action<Dictionary<string, object>>(OnStickSpawned));
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

                var allSticks = UnityEngine.Object.FindObjectsOfType<Stick>();
                if (allSticks == null) return;
                foreach (var stick in allSticks)
                {
                    if (stick == null) continue;
                    ApplyStickBarrierRules(stick);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void OnStickSpawned(Dictionary<string, object> message)
        {
            try
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
                if (message == null || !message.ContainsKey("stick")) return;
                var stick = message["stick"] as Stick;
                if (stick == null) return;
                ApplyStickBarrierRules(stick);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // Ignore collisions between stick colliders and scene colliders tagged "Barrier"
        private static void ApplyStickBarrierRules(Stick stick)
        {
            if (stick == null) return;
            var myCols = stick.GetComponentsInChildren<Collider>(true);
            if (myCols == null || myCols.Length == 0) return;

            try
            {
                var allSceneColliders = UnityEngine.Object.FindObjectsOfType<Collider>(true);
                if (allSceneColliders == null) return;

                foreach (var sceneCol in allSceneColliders)
                {
                    if (sceneCol == null) continue;
                    // only interested in static barriers
                    if (!sceneCol.CompareTag("Barrier")) continue;

                    // ensure it's really a scene barrier (not part of a stick or puck)
                    if (sceneCol.GetComponentInParent<Stick>() != null) continue;
                    if (sceneCol.GetComponentInParent<Puck>() != null) continue;

                    foreach (var a in myCols)
                    {
                        if (a == null) continue;
                        try { Physics.IgnoreCollision(a, sceneCol, true); } catch { }
                    }
                }
            }
            catch (Exception) { }
        }
    }
}
