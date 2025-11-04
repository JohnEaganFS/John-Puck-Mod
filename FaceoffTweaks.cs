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
        public static Vector3 OverridePosition = Vector3.zero;

        // If true, attempt to use the PlayerPosition named/role "Center" as a baseline. Falls back to PuckPosition transform.
        public static bool UsePlayerCenterBaseline = false;

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
    }

    // Patch PuckManager.Server_SpawnPucksForPhase to allow configurable faceoff spawn positions
    [HarmonyPatch(typeof(PuckManager), "Server_SpawnPucksForPhase")]
    static class PuckManager_Server_SpawnPucksForPhase_Patch
    {
        static bool Prefix(GamePhase phase, PuckManager __instance)
        {
            try
            {
                if (!FaceoffTweaksHelpers.Enabled) return true; // let original run
                if (!NetworkManager.Singleton.IsServer) return true; // server-only behavior
                if (phase != GamePhase.FaceOff) return true; // only modify faceoff spawning

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

                // Convenience: cached access to PlayerPositionManager for optional center-baseline
                PlayerPositionManager ppManager = null;
                if (FaceoffTweaksHelpers.UsePlayerCenterBaseline)
                {
                    try { ppManager = NetworkBehaviourSingleton<PlayerPositionManager>.Instance; } catch { ppManager = null; }
                }

                foreach (PuckPosition puckPosition in puckPositions)
                {
                    if (puckPosition == null) continue;
                    if (puckPosition.Phase != phase) continue;

                    Vector3 spawnPos = puckPosition.transform.position;
                    Quaternion spawnRot = puckPosition.transform.rotation;

                    // Option: use override position or PlayerPosition "Center" baseline if requested
                    if (FaceoffTweaksHelpers.UseOverridePosition)
                    {
                        spawnPos = FaceoffTweaksHelpers.OverridePosition;
                    }
                    else if (ppManager != null)
                    {
                        try
                        {
                            // Try to find a PlayerPosition whose Name contains "center" (case-insensitive)
                            PlayerPosition centerPos = ppManager.AllPositions.Find(p => p != null && !string.IsNullOrEmpty(p.Name) && p.Name.ToLower().Contains("center"));
                            if (centerPos != null)
                            {
                                // If the center position is claimed by a player, prefer that player's world position
                                if (centerPos.IsClaimed && centerPos.ClaimedBy != null)
                                {
                                    spawnPos = centerPos.ClaimedBy.transform.position;
                                }
                                else
                                {
                                    spawnPos = centerPos.transform.position;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Ignore and fall back to configured puckPosition transform
                        }
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
                    __instance.Server_SpawnPuck(spawnPos, spawnRot, Vector3.zero, false);
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
}
