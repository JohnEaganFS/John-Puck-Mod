using System;
using System.Collections.Generic;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace OfficialPuckMod
{
    // Mod entry for player-position-specific faceoff tweaks
    public class FaceoffPlayerPositionTweaksMod : IPuckMod
    {
        public bool OnEnable()
        {
            try
            {
                FaceoffPlayerPositionTweaksHelpers.Init();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Debug.Log("[FaceoffPlayerPositionTweaksMod] Enabled.");
            return true;
        }

        public bool OnDisable()
        {
            try
            {
                FaceoffPlayerPositionTweaksHelpers.Shutdown();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Debug.Log("[FaceoffPlayerPositionTweaksMod] Disabled.");
            return true;
        }
    }

    // Bootstrap so patches apply early
    static class FaceoffPlayerPositionTweaksBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoad()
        {
            try
            {
                FaceoffPlayerPositionTweaksHelpers.Init();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    static class FaceoffPlayerPositionTweaksHelpers
    {
        internal static readonly Harmony harmony = new Harmony("John.OfficialPuckMod.FaceoffPlayerPositionTweaks");

        // Public knobs
        public static bool Enabled = true;

        // If true, use a fixed override position instead of adjusting based on the scene PlayerPosition
        public static bool UsePlayerCenterOverridePosition = false;
        public static Vector3 PlayerCenterOverridePosition = new Vector3(5f, 5f, 0f);

        // Offsets applied relative to the found PlayerPosition transform (forward/right) or to the override
        public static float PlayerCenterForwardOffset = 5f;
        public static float PlayerCenterSideOffset = 0f;
        public static float PlayerCenterVerticalOffset = 0f;

        // Random jitter radius in meters (horizontal plane)
        public static float PlayerCenterRandomRadius = 0f;

        // Internal state for restoring originals
        private static readonly Dictionary<PlayerPosition, Vector3> CenterOriginalPositions = new Dictionary<PlayerPosition, Vector3>();
        private static bool CentersAdjusted = false;

        public static void Init()
        {
            try
            {
                harmony.PatchAll();
                Debug.Log("[FaceoffPlayerPositionTweaksHelpers] Initialized (patched).");
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
                // Attempt to restore if left adjusted
                try { RestoreCenters(); } catch { }

                harmony.UnpatchSelf();
                Debug.Log("[FaceoffPlayerPositionTweaksHelpers] Shutdown (unpatched).");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void RestoreCenters()
        {
            if (!CentersAdjusted) return;
            try
            {
                foreach (var kv in CenterOriginalPositions)
                {
                    try
                    {
                        if (kv.Key != null)
                        {
                            kv.Key.transform.position = kv.Value;
                        }
                    }
                    catch (Exception) { }
                }
            }
            finally
            {
                CenterOriginalPositions.Clear();
                CentersAdjusted = false;
                Debug.Log("[FaceoffPlayerPositionTweaks] Restored center PlayerPosition transforms.");
            }
        }

        private static void AdjustCenters(PlayerPositionManager ppManager)
        {
            Debug.Log("[FaceoffPlayerPositionTweaks] Adjusting center PlayerPosition transforms...");
            if (ppManager == null) return;
            if (CentersAdjusted) return;

            try
            {
                // For now, apply to all PlayerPosition entries (by index). We'll narrow down later.
                var centers = ppManager.AllPositions; 
                if (centers == null || centers.Count == 0) return;

                int adjustedCount = 0;
                for (int i = 0; i < centers.Count; i++)
                {
                    var centerPos = centers[i];
                    if (centerPos == null) continue;
                    try
                    {
                        // Log identifying info for mapping indexes to in-game positions
                        try
                        {
                            string claimedBy = "null";
                            try { if (centerPos.ClaimedBy != null) claimedBy = centerPos.ClaimedBy.Username.Value.ToString(); } catch { }
                            Debug.Log(string.Format("[FaceoffPlayerPositionTweaks] Pos[{0}] Name='{1}' Role='{2}' Team='{3}' Claimed={4} ClaimedBy='{5}' WorldPos={6}",
                                i,
                                centerPos.Name,
                                centerPos.Role.ToString(),
                                centerPos.Team.ToString(),
                                centerPos.IsClaimed,
                                claimedBy,
                                centerPos.transform.position));
                        }
                        catch { 
                            Debug.Log("[FaceoffPlayerPositionTweaks] (failed to log identifying info)");
                        }

                        // Only apply transforms to positions named exactly 'C' (case-insensitive)
                        bool isCenterName = !string.IsNullOrEmpty(centerPos.Name) && centerPos.Name.Trim().Equals("C", StringComparison.OrdinalIgnoreCase);
                        if (!isCenterName) continue;

                        // store original
                        CenterOriginalPositions[centerPos] = centerPos.transform.position;

                        Vector3 target = centerPos.transform.position;

                        if (UsePlayerCenterOverridePosition)
                        {
                            target = PlayerCenterOverridePosition;
                        }
                        else
                        {
                            // if claimed, use the player's world position
                            try
                            {
                                if (centerPos.IsClaimed && centerPos.ClaimedBy != null)
                                {
                                    target = centerPos.ClaimedBy.transform.position;
                                }
                            }
                            catch { }
                        }

                        // apply relative offsets (relative to centerPos transform)
                        // If this position is the center ('C'), mirror the forward/side offsets between teams
                        try
                        {
                            float sign = 1f;
                            bool isBlueCenter = !string.IsNullOrEmpty(centerPos.Name) && centerPos.Name.Trim().Equals("Blue", StringComparison.OrdinalIgnoreCase);
                            if (isBlueCenter)
                            {
                                sign = 1f;
                                Debug.Log("[FaceoffPlayerPositionTweaks] Mirroring offsets for Blue team.");
                            }
                            else
                            {
                                sign = -1f;
                                Debug.Log("[FaceoffPlayerPositionTweaks] Mirroring offsets for Red team.");
                            }
                            // try { if (centerPos.Team == PlayerTeam.Red) sign = -1f; Debug.Log("[FaceoffPlayerPositionTweaks] Mirroring offsets for Red team."); } catch { }

                            if (isCenterName)
                            {
                                target += centerPos.transform.forward * PlayerCenterForwardOffset * sign;
                                target += centerPos.transform.right * PlayerCenterSideOffset * sign;
                            }
                            else
                            {
                                target += centerPos.transform.forward * PlayerCenterForwardOffset;
                                target += centerPos.transform.right * PlayerCenterSideOffset;
                            }

                            target.y += PlayerCenterVerticalOffset;
                        }
                        catch { }

                        // jitter
                        if (PlayerCenterRandomRadius > 0f)
                        {
                            Vector2 jitter2 = UnityEngine.Random.insideUnitCircle * PlayerCenterRandomRadius;
                            target += new Vector3(jitter2.x, 0f, jitter2.y);
                        }

                        centerPos.transform.position = target;
                        adjustedCount++;
                    }
                    catch (Exception) { }
                }

                if (adjustedCount > 0)
                {
                    CentersAdjusted = true;
                    Debug.Log("[FaceoffPlayerPositionTweaks] Adjusted center PlayerPosition transforms. Count=" + adjustedCount);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // Patch the same GamePhase change event used by the puck tweaks. On entering FaceOff we adjust center markers,
        // and when leaving FaceOff we restore them.
        [HarmonyPatch(typeof(PuckManagerController), "Event_OnGamePhaseChanged")]
        class PuckManagerController_Event_OnGamePhaseChanged_Patch_PP
        {
            static void Prefix(Dictionary<string, object> message)
            {
                try
                {
                    if (!Enabled) return;
                    if (message == null) return;
                    if (!message.ContainsKey("oldGamePhase") || !message.ContainsKey("newGamePhase")) return;
                    GamePhase oldPhase = (GamePhase)message["oldGamePhase"];
                    GamePhase newPhase = (GamePhase)message["newGamePhase"];

                    // Entering FaceOff: adjust centers
                    if (oldPhase != GamePhase.FaceOff && newPhase == GamePhase.FaceOff)
                    {
                        PlayerPositionManager ppManager = null;
                        try { ppManager = NetworkBehaviourSingleton<PlayerPositionManager>.Instance; } catch { ppManager = null; }
                        if (ppManager != null)
                        {
                            AdjustCenters(ppManager);
                        }
                    }

                    // Leaving FaceOff: restore
                    if (oldPhase == GamePhase.FaceOff && newPhase != GamePhase.FaceOff)
                    {
                        RestoreCenters();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}
