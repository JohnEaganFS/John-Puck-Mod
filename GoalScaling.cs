using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using HarmonyLib;

namespace OfficialPuckMod
{
    // Harmony postfix helpers to scale goal trigger colliders when goals spawn
    static class GoalScalingHelpers
    {
        // Set this to your desired multiplier (1.0 = no change). Edit and rebuild or set at runtime.
        public static float GoalTriggerScale = 1.0f;

        // Keep original collider values so we don't scale repeatedly
        class OriginalData
        {
            public Vector3? BoxSize;
            public float? SphereRadius;
            public float? CapsuleRadius;
            public float? CapsuleHeight;
            public Vector3? TransformScale;
            // store original localScale for visual-only transforms (mesh renderers)
            public Dictionary<int, Vector3> VisualScales;
        }

        static readonly Dictionary<int, OriginalData> originals = new Dictionary<int, OriginalData>();

        internal static void ApplyScaleToGoal(Goal goal)
        {
            try
            {
                if (goal == null) return;
                if (Math.Abs(GoalTriggerScale - 1.0f) < 0.000001f) return; // no-op

                // Find all GoalTrigger components in the scene and pick those that reference this goal
                var allTriggers = UnityEngine.Object.FindObjectsOfType<GoalTrigger>(true);
                if (allTriggers == null || allTriggers.Length == 0) return;

                // reflection access to private 'goal' field on GoalTrigger
                var fi = typeof(GoalTrigger).GetField("goal", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                foreach (var gt in allTriggers)
                {
                    if (gt == null) continue;
                    try
                    {
                        Goal referenced = null;
                        if (fi != null)
                        {
                            referenced = fi.GetValue(gt) as Goal;
                        }
                        else
                        {
                            // fallback: try GetComponentInParent<Goal>()
                            referenced = gt.GetComponentInParent<Goal>();
                        }

                        if (referenced != goal) continue;

                        ApplyScaleToTriggerCollider(gt);
                    }
                    catch { }
                }
                // Also apply visual scaling so the visible goal model matches the trigger size
                try
                {
                    ApplyVisualScaleToGoal(goal);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // Scale visual meshes of the goal to match colliders. Only scales MeshRenderers
        // that are not also colliders (so we avoid double-scaling physics colliders).
        // Stores original localScale per transform to avoid cumulative scaling.
        public static float GoalVisualScale = GoalTriggerScale; // default to the same multiplier

        static void ApplyVisualScaleToGoal(Goal goal)
        {
            if (goal == null) return;
            if (Math.Abs(GoalVisualScale - 1.0f) < 0.000001f) return; // no-op

            var root = goal.gameObject;
            if (root == null) return;

            // find all MeshRenderers under the goal
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers == null || renderers.Length == 0) return;

            foreach (var mr in renderers)
            {
                if (mr == null) continue;
                try
                {
                    var rgo = mr.gameObject;

                    // Skip if this renderer's GameObject also has a Collider - we don't want
                    // to change collider-bearing objects here (colliders handled separately).
                    if (rgo.GetComponent<Collider>() != null) continue;

                    int goalId = root.GetInstanceID();
                    if (!originals.TryGetValue(goalId, out var orig))
                    {
                        orig = new OriginalData();
                        originals[goalId] = orig;
                    }

                    if (orig.VisualScales == null) orig.VisualScales = new Dictionary<int, Vector3>();

                    var t = mr.transform;
                    int tid = t.GetInstanceID();
                    if (!orig.VisualScales.TryGetValue(tid, out var saved))
                    {
                        saved = t.localScale;
                        orig.VisualScales[tid] = saved;
                    }

                    t.localScale = Vector3.Scale(saved, Vector3.one * GoalVisualScale);
                }
                catch { }
            }
            Debug.Log("[GoalScaling] Applied visual scaling for goal.");
        }

        static void ApplyScaleToTriggerCollider(GoalTrigger gt)
        {
            if (gt == null) return;
            var go = gt.gameObject;
            if (go == null) return;

            int id = go.GetInstanceID();
            if (!originals.TryGetValue(id, out var orig))
            {
                orig = new OriginalData();
                originals[id] = orig;
            }

            // Try primary collider on same GameObject
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                ApplyScaleToCollider(col, orig, go.transform);
            }

            // Also apply to child colliders (some goals have trigger colliders on children)
            var childCols = go.GetComponentsInChildren<Collider>(true);
            if (childCols != null && childCols.Length > 0)
            {
                foreach (var c in childCols)
                {
                    if (c == null) continue;
                    ApplyScaleToCollider(c, orig, c.transform);
                }
            }
        }

        static void ApplyScaleToCollider(Collider c, OriginalData orig, Transform owner)
        {
            if (c == null || owner == null) return;

            try
            {
                if (c is BoxCollider bc)
                {
                    if (orig.BoxSize == null) orig.BoxSize = bc.size;
                    bc.size = Vector3.Scale(orig.BoxSize.Value, Vector3.one * GoalTriggerScale);
                }
                else if (c is SphereCollider sc)
                {
                    if (orig.SphereRadius == null) orig.SphereRadius = sc.radius;
                    sc.radius = orig.SphereRadius.Value * GoalTriggerScale;
                }
                else if (c is CapsuleCollider cc)
                {
                    if (orig.CapsuleRadius == null) orig.CapsuleRadius = cc.radius;
                    if (orig.CapsuleHeight == null) orig.CapsuleHeight = cc.height;
                    cc.radius = orig.CapsuleRadius.Value * GoalTriggerScale;
                    cc.height = orig.CapsuleHeight.Value * GoalTriggerScale;
                }
                else
                {
                    // For mesh or unknown colliders, scale the transform once
                    if (orig.TransformScale == null) orig.TransformScale = owner.localScale;
                    owner.localScale = Vector3.Scale(orig.TransformScale.Value, Vector3.one * GoalTriggerScale);
                }
            }
            catch { }
        }
    }

    // Harmony patch: run after GoalController.OnNetworkSpawn so goal and its trigger colliders are ready
    [HarmonyPatch(typeof(GoalController), "OnNetworkSpawn")]
    static class GoalController_ScalePostfix_Patch
    {
        static void Postfix(GoalController __instance)
        {
            try
            {
                if (__instance == null) return;
                var goal = __instance.GetComponent<Goal>();
                if (goal == null) return;
                // Apply scale for this goal instance
                GoalScalingHelpers.ApplyScaleToGoal(goal);
                Debug.Log("[GoalScaling] Applied trigger scaling for goal.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
