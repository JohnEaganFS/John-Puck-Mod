using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using HarmonyLib;

namespace OfficialPuckMod
{
    // Apply runtime tweaks to Movement serialized fields via Harmony postfix on Awake
    static class MovementTweaksHelpers
    {
        // Toggle helpers on/off
        public static bool Enabled = true;

        // Multipliers (default 1.0 = no change). Set at runtime to tweak behaviour.
        public static float TurnAccelerationMultiplier = 1.18f;
        public static float TurnMaxSpeedMultiplier = 1.0764f;
        public static float MaxBackwardsSpeedMultiplier = 1.035f;
        public static float MaxBackwardsSprintSpeedMultiplier = 1.05f;
        public static float BackwardsAccelerationMultiplier = 1.5f;
        public static float BackwardsSprintAccelerationMultiplier = 1.5f;

        // Additional movement multipliers
        public static float TurnBrakeAccelerationMultiplier = 2.3f;
        public static float TurnDragMultiplier = 1.0f;

        // VelocityLean multiplier for angular force
        public static float VelocityLeanAngularForceMultiplier = 0.87f;

        // PlayerBodyV2 multipliers for balance timings
        // balanceRecoveryTime: how long it takes to recover balance after a fall (higher = slower recovery)
        public static float BalanceRecoveryTimeMultiplier = 0.3f;
        // balanceLossTime: how long it takes to lose balance when slipping (higher = slower loss)
        public static float BalanceLossTimeMultiplier = 0.3f;

        // Keep originals per-instance so we don't compound multipliers
        class OriginalValues
        {
            public readonly Dictionary<string, float> Values = new Dictionary<string, float>(StringComparer.Ordinal);
        }

        static readonly Dictionary<int, OriginalValues> originals = new Dictionary<int, OriginalValues>();

        public static void ApplyTo(Movement m)
        {
            if (!Enabled || m == null) return;

            try
            {
                int id = m.GetInstanceID();
                if (!originals.TryGetValue(id, out var orig))
                {
                    orig = new OriginalValues();
                    originals[id] = orig;
                }

                // Helper to apply a multiplier to a private float field safely
                void ApplyMul(string fieldName, float multiplier)
                {
                    if (Math.Abs(multiplier - 1.0f) < 0.0000001f) return; // no-op

                    var fi = typeof(Movement).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (fi == null) return;

                    if (!orig.Values.TryGetValue(fieldName, out var original))
                    {
                        try
                        {
                            original = Convert.ToSingle(fi.GetValue(m));
                        }
                        catch
                        {
                            return;
                        }
                        orig.Values[fieldName] = original;
                    }

                    try
                    {
                        float newVal = original * multiplier;
                        fi.SetValue(m, newVal);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                ApplyMul("turnAcceleration", TurnAccelerationMultiplier);
                ApplyMul("turnMaxSpeed", TurnMaxSpeedMultiplier);
                ApplyMul("maxBackwardsSpeed", MaxBackwardsSpeedMultiplier);
                ApplyMul("maxBackwardsSprintSpeed", MaxBackwardsSprintSpeedMultiplier);
                ApplyMul("backwardsAcceleration", BackwardsAccelerationMultiplier);
                ApplyMul("backwardsSprintAcceleration", BackwardsSprintAccelerationMultiplier);
                ApplyMul("turnBrakeAcceleration", TurnBrakeAccelerationMultiplier);
                ApplyMul("turnDrag", TurnDragMultiplier);

                Debug.Log($"[MovementTweaks] Applied multipliers to Movement '{m.name}' (id={id}).");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // Convenience: apply to all existing Movement instances
        public static void ApplyToAllExisting()
        {
            try
            {
                var all = UnityEngine.Object.FindObjectsOfType<Movement>(true);
                if (all == null) return;
                foreach (var m in all) ApplyTo(m);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        
        // Apply tweaks to VelocityLean instances (angularForceMultiplier)
        public static void ApplyTo(VelocityLean vl)
        {
            if (vl == null) return;
            try
            {
                int id = vl.GetInstanceID();
                if (!originals.TryGetValue(id, out var orig))
                {
                    orig = new OriginalValues();
                    originals[id] = orig;
                }

                void ApplyMulVL(string fieldName, float multiplier)
                {
                    if (Math.Abs(multiplier - 1.0f) < 0.0000001f) return;
                    var fi = typeof(VelocityLean).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (fi == null) return;
                    if (!orig.Values.TryGetValue(fieldName, out var original))
                    {
                        try { original = Convert.ToSingle(fi.GetValue(vl)); }
                        catch { return; }
                        orig.Values[fieldName] = original;
                    }
                    try { fi.SetValue(vl, original * multiplier); }
                    catch (Exception e) { Debug.LogException(e); }
                }

                ApplyMulVL("angularForceMultiplier", VelocityLeanAngularForceMultiplier);
                Debug.Log($"[MovementTweaks] Applied VelocityLean multipliers to '{vl.name}' (id={id}).");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // Convenience: apply to all existing VelocityLean instances
        public static void ApplyToAllVelocityLeanExisting()
        {
            try
            {
                var all = UnityEngine.Object.FindObjectsOfType<VelocityLean>(true);
                if (all == null) return;
                foreach (var vl in all) ApplyTo(vl);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        
        // Apply tweaks to PlayerBodyV2 instances (balanceRecoveryTime)
        public static void ApplyTo(PlayerBodyV2 pb)
        {
            if (pb == null) return;
            try
            {
                int id = pb.GetInstanceID();
                if (!originals.TryGetValue(id, out var orig))
                {
                    orig = new OriginalValues();
                    originals[id] = orig;
                }

                void ApplyMulPB(string fieldName, float multiplier)
                {
                    if (Math.Abs(multiplier - 1.0f) < 0.0000001f) return;
                    var fi = typeof(PlayerBodyV2).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (fi == null) return;
                    if (!orig.Values.TryGetValue(fieldName, out var original))
                    {
                        try { original = Convert.ToSingle(fi.GetValue(pb)); }
                        catch { return; }
                        orig.Values[fieldName] = original;
                    }
                    try { fi.SetValue(pb, original * multiplier); }
                    catch (Exception e) { Debug.LogException(e); }
                }

                ApplyMulPB("balanceRecoveryTime", BalanceRecoveryTimeMultiplier);
                ApplyMulPB("balanceLossTime", BalanceLossTimeMultiplier);
                Debug.Log($"[MovementTweaks] Applied PlayerBodyV2 multipliers to '{pb.name}' (id={id}).");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // Convenience: apply to all existing PlayerBodyV2 instances
        public static void ApplyToAllPlayerBodyExisting()
        {
            try
            {
                var all = UnityEngine.Object.FindObjectsOfType<PlayerBodyV2>(true);
                if (all == null) return;
                foreach (var pb in all) ApplyTo(pb);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    // Patch Movement.Awake so we change serialized values early (before Start runs)
    [HarmonyPatch(typeof(Movement), "Awake")]
    static class Movement_Awake_Patch
    {
        static void Postfix(Movement __instance)
        {
            try
            {
                MovementTweaksHelpers.ApplyTo(__instance);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    // Patch VelocityLean.Awake so we apply the angularForceMultiplier early
    [HarmonyPatch(typeof(VelocityLean), "Awake")]
    static class VelocityLean_Awake_Patch
    {
        static void Postfix(VelocityLean __instance)
        {
            try
            {
                MovementTweaksHelpers.ApplyTo(__instance);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    // Patch PlayerBodyV2.Awake so we apply balanceRecoveryTime multiplier early
    [HarmonyPatch(typeof(PlayerBodyV2), "Awake")]
    static class PlayerBodyV2_Awake_Patch
    {
        static void Postfix(PlayerBodyV2 __instance)
        {
            try
            {
                MovementTweaksHelpers.ApplyTo(__instance);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
