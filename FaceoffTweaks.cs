using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace OfficialPuckMod
{
    // Mod entry exposing enable/disable for the faceoff tweaks
    public class FaceoffTweaksMod : IPuckMod
    {
        public bool OnEnable()
        {
            try
            {
                FaceoffTweaksHelpers.Init();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Debug.Log("[FaceoffTweaksMod] Enabled.");
            return true;
        }

        public bool OnDisable()
        {
            try
            {
                FaceoffTweaksHelpers.Shutdown();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Debug.Log("[FaceoffTweaksMod] Disabled.");
            return true;
        }
    }

    // Ensure the faceoff tweaks are initialized as early as possible so patches are applied before phase events
    static class FaceoffTweaksBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoad()
        {
            try
            {
                FaceoffTweaksHelpers.Init();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    static class FaceoffTweaksHelpers
    {
        internal static readonly Harmony harmony = new Harmony("John.OfficialPuckMod.FaceoffTweaks");

        // Public knobs you can tweak at runtime (assign via other runtime config or console)
        public static bool Enabled = true;

        // Offsets relative to the configured PuckPosition transform
        public static float ForwardOffset = 0f; // along PuckPosition.transform.forward
        public static float SideOffset = 0f; // along PuckPosition.transform.right
        public static float VerticalOffset = 0f; // added to y

        // Random jitter radius in meters (horizontal plane)
        public static float RandomRadius = 0f;

        // If true, use a fixed override position instead of the scene PuckPosition
        public static bool UseOverridePosition = true;
        // When UseOverridePosition is true this value is used directly (world space)
        public static Vector3 OverridePosition = new Vector3(0f, -0.49f, 0f);

    // Internal flag set when GameManager transitions FaceOff -> Playing so we can treat the Playing spawn as a faceoff
    // Defaults to false; the patch will only modify the Playing-phase spawn when this flag is set by the phase-change hook.
    public static bool TreatNextPlayingSpawnAsFaceoff = false;

        public static void Init()
        {
            try
            {
                harmony.PatchAll();
                Debug.Log("[FaceoffTweaksHelpers] Initialized (patched).");
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
                Debug.Log("[FaceoffTweaksHelpers] Shutdown (unpatched).");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    // Patch PuckManager.Server_SpawnPucksForPhase to allow configurable faceoff spawn positions
    [HarmonyPatch(typeof(PuckManager), "Server_SpawnPucksForPhase")]
    static class PuckManager_Server_SpawnPucksForPhase_Patch
    {
        // Use conventional Harmony parameter order (instance first) and add diagnostics
        static bool Prefix(PuckManager __instance, GamePhase phase)
        {
            try
            {
                Debug.Log(string.Format("[FaceoffTweaks] Prefix invoked. Enabled={0}, phase={1}", FaceoffTweaksHelpers.Enabled, phase));
                if (!FaceoffTweaksHelpers.Enabled)
                {
                    Debug.Log("[FaceoffTweaks] Disabled, letting original run.");
                    return true; // let original run
                }
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                {
                    Debug.Log("[FaceoffTweaks] Not server or NetworkManager missing, letting original run.");
                    return true; // server-only behavior
                }
                // Only apply when the manager is spawning for the Playing phase and we've been marked to treat that spawn as a faceoff.
                bool treatAsFaceoff = (phase == GamePhase.Playing && FaceoffTweaksHelpers.TreatNextPlayingSpawnAsFaceoff);
                if (!treatAsFaceoff)
                {
                    Debug.Log("[FaceoffTweaks] Not treating this Playing spawn as faceoff; letting original run.");
                    return true; // only modify the designated Playing-phase faceoff spawn
                }

                // Access private puckPositions field via reflection
                FieldInfo fi = typeof(PuckManager).GetField("puckPositions", BindingFlags.NonPublic | BindingFlags.Instance);
                if (fi == null)
                {
                    Debug.LogWarning("[FaceoffTweaks] Failed to find puckPositions field on PuckManager; aborting tweak.");
                    return true;
                }

                var puckPositions = fi.GetValue(__instance) as List<PuckPosition>;
                if (puckPositions == null)
                {
                    Debug.LogWarning("[FaceoffTweaks] puckPositions is null or not a List<PuckPosition>.");
                    return true;
                }

                Debug.Log(string.Format("[FaceoffTweaks] Found {0} puckPositions.", puckPositions.Count));

                // (No player-position baseline in this helper; puck spawn uses PuckPosition or an explicit override.)

                // Only adjust puck positions that are configured for Playing (the manager will spawn those when phase==Playing)
                foreach (PuckPosition puckPosition in puckPositions)
                {
                    if (puckPosition == null) continue;
                    if (puckPosition.Phase != GamePhase.Playing) continue;

                    Vector3 spawnPos = puckPosition.transform.position;
                    Quaternion spawnRot = puckPosition.transform.rotation;

                    Debug.Log(string.Format("[FaceoffTweaks] Base puckPosition at {0}", spawnPos));

                    // Option: use override position if requested; otherwise keep the configured puckPosition transform
                    if (FaceoffTweaksHelpers.UseOverridePosition)
                    {
                        spawnPos = FaceoffTweaksHelpers.OverridePosition;
                    }

                    // Apply configured offsets relative to puckPosition transform axes
                    spawnPos += puckPosition.transform.forward * FaceoffTweaksHelpers.ForwardOffset;
                    spawnPos += puckPosition.transform.right * FaceoffTweaksHelpers.SideOffset;
                    spawnPos.y += FaceoffTweaksHelpers.VerticalOffset;

                    // Apply horizontal jitter if configured
                    if (FaceoffTweaksHelpers.RandomRadius > 0f)
                    {
                        Vector2 jitter2 = UnityEngine.Random.insideUnitCircle * FaceoffTweaksHelpers.RandomRadius;
                        spawnPos += new Vector3(jitter2.x, 0f, jitter2.y);
                    }

                    // Spawn using the manager's spawn helper
                    Debug.Log(string.Format("[FaceoffTweaks] Spawning puck at {0}", spawnPos));
                    __instance.Server_SpawnPuck(spawnPos, spawnRot, Vector3.zero, false);
                }

                // If we treated a Playing spawn as faceoff, clear the flag so subsequent Playing spawns are normal
                if (phase == GamePhase.Playing && FaceoffTweaksHelpers.TreatNextPlayingSpawnAsFaceoff)
                {
                    FaceoffTweaksHelpers.TreatNextPlayingSpawnAsFaceoff = false;
                    Debug.Log("[FaceoffTweaks] Cleared TreatNextPlayingSpawnAsFaceoff flag.");
                }

                // Skip original to avoid double-spawning
                return false;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                // On error, allow original method to run to avoid breaking gameplay
                return true;
            }
        }
    }

    // Patch PuckManagerController.Event_OnGamePhaseChanged to mark Playing spawns that immediately follow FaceOff
    [HarmonyPatch(typeof(PuckManagerController), "Event_OnGamePhaseChanged")]
    static class PuckManagerController_Event_OnGamePhaseChanged_Patch
    {
        static void Prefix(Dictionary<string, object> message)
        {
            try
            {
                if (message == null) return;
                if (!message.ContainsKey("oldGamePhase") || !message.ContainsKey("newGamePhase")) return;
                GamePhase oldPhase = (GamePhase)message["oldGamePhase"];
                GamePhase newPhase = (GamePhase)message["newGamePhase"];
                // Only set the flag when transitioning from FaceOff to Playing. This flag enables a single modification
                // of the Playing-phase spawn immediately after a FaceOff. We do not touch FaceOff-phase spawns.
                if (oldPhase == GamePhase.FaceOff && newPhase == GamePhase.Playing)
                {
                    FaceoffTweaksHelpers.TreatNextPlayingSpawnAsFaceoff = true;
                    Debug.Log("[FaceoffTweaks] Marked next Playing spawn as faceoff.");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
