using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace OfficialPuckMod
{
    // Defensive replacement for Puck.OnCollisionEnter for Goal Net / Goal Post collisions.
    // Prefix handles those collisions safely (avoids normal-averaging NaNs) and then skips the original.
    [HarmonyPatch(typeof(Puck), "OnCollisionEnter")]
    static class LatvianFix_Puck_OnCollisionEnter
    {
        static bool Prefix(Puck __instance, Collision collision)
        {
            try
            {
                // replicate the stick-touch behaviour from original method
                var stick = collision.gameObject.GetComponent<Stick>();
                if (stick)
                {
                    // TouchingStick and ShotSpeed have non-public setters; use backing-field reflection
                    var touchField = typeof(Puck).GetField("<TouchingStick>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (touchField != null)
                    {
                        touchField.SetValue(__instance, stick);
                    }
                    var shotField = typeof(Puck).GetField("<ShotSpeed>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (shotField != null)
                    {
                        shotField.SetValue(__instance, 0f);
                    }
                }

                // If puck is grounded, original early-return behaviour
                if (__instance.IsGrounded)
                {
                    return false; // skip original
                }

                int layer = collision.gameObject.layer;
                int goalNetLayer = LayerMask.NameToLayer("Goal Net");
                int goalPostLayer = LayerMask.NameToLayer("Goal Post");

                // Only override behaviour for Goal Net / Goal Post — otherwise let original run
                if (layer != goalNetLayer && layer != goalPostLayer)
                {
                    return true; // run original
                }

                // Choose a robust contact normal: pick the contact most aligned with relative velocity
                Vector3 relVel = collision.relativeVelocity;
                Vector3 chosenNormal = Vector3.zero;
                float bestWeight = -1f;
                foreach (var cp in collision.contacts)
                {
                    float weight = relVel.sqrMagnitude > 1e-8f ? Mathf.Abs(Vector3.Dot(relVel, cp.normal)) : 1f;
                    if (weight > bestWeight)
                    {
                        bestWeight = weight;
                        chosenNormal = cp.normal;
                    }
                }
                if (chosenNormal.sqrMagnitude < 1e-8f)
                {
                    // fallback
                    chosenNormal = (collision.contacts.Length > 0) ? collision.contacts[0].normal : Vector3.up;
                }
                chosenNormal.Normalize();

                // compute influence t defensively
                float t = 0f;
                if (relVel.sqrMagnitude > 1e-8f)
                {
                    t = Mathf.Clamp01(Mathf.Abs(Vector3.Dot(relVel.normalized, chosenNormal)));
                }

                // Read the private limits via reflection (fall back to safe defaults if missing)
                float maxLinear = 2f;
                float maxAngular = 2f;
                var fiLinear = typeof(Puck).GetField("goalNetLinearVelocityMaximumMagnitude", BindingFlags.Instance | BindingFlags.NonPublic);
                var fiAngular = typeof(Puck).GetField("goalNetAngularVelocityMaximumMagnitude", BindingFlags.Instance | BindingFlags.NonPublic);
                try { if (fiLinear != null) maxLinear = Convert.ToSingle(fiLinear.GetValue(__instance)); } catch { }
                try { if (fiAngular != null) maxAngular = Convert.ToSingle(fiAngular.GetValue(__instance)); } catch { }

                var rb = __instance.Rigidbody;
                if (rb != null)
                {
                    // Clamp horizontal speed while preserving vertical component
                    if (rb.linearVelocity.magnitude > maxLinear)
                    {
                        Vector3 current = rb.linearVelocity;
                        Vector3 horiz = new Vector3(current.x, 0f, current.z);
                        Vector3 clampedHoriz = Vector3.ClampMagnitude(horiz, maxLinear);
                        Vector3 target = new Vector3(clampedHoriz.x, current.y, clampedHoriz.z);
                        rb.linearVelocity = Vector3.Lerp(current, target, t);
                    }

                    // Clamp angular velocity magnitude defensively
                    if (rb.angularVelocity.magnitude > maxAngular)
                    {
                        Vector3 clampedAng = Vector3.ClampMagnitude(rb.angularVelocity, maxAngular);
                        rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, clampedAng, t);
                    }
                }

                // We've handled Goal collisions — skip the original method entirely
                return false;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                // On error, let original run so we don't change behaviour unpredictably
                return true;
            }
        }
    }
}
