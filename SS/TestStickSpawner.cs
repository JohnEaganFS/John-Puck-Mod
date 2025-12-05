using System;
using System.Reflection;
using UnityEngine;
using Unity.Netcode;

public class TestStickSpawner : MonoBehaviour
{
    // Key to press to spawn a test stick (client-side spawn if not server)
    public KeyCode spawnKey = KeyCode.K;

    // Offset forward from player body when spawning
    public Vector3 spawnOffset = new Vector3(0f, 0f, 0f);

    void Update()
    {
        if (IsSpawnKeyPressed())
        {
            Debug.Log("[OfficialPuckMod] TestStickSpawner: spawn key pressed");
            SpawnTestStick();
        }
    }

    // When the old Input class is disabled by the new Input System package, calling
    // Input.GetKeyDown throws InvalidOperationException. We attempt the old API first
    // and fall back to the new Input System via reflection so this code compiles whether
    // or not the package is installed.
    private bool _useInputSystemFallback = false;

    bool IsSpawnKeyPressed()
    {
        try
        {
            // Fast path: try the old Input API
            return Input.GetKeyDown(spawnKey);
        }
        catch (InvalidOperationException)
        {
            // Old Input API disabled; fall back to Input System via reflection
            _useInputSystemFallback = true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return false;
        }

        if (!_useInputSystemFallback)
            return false;

        try
        {
            // Try to find the Keyboard type from the Input System package
            Type keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem")
                                ?? Type.GetType("UnityEngine.InputSystem.Keyboard, UnityEngine.InputSystem");
            if (keyboardType == null)
            {
                Debug.LogWarning("[OfficialPuckMod] Input System keyboard type not found via reflection.");
                return false;
            }

            var currentProp = keyboardType.GetProperty("current", BindingFlags.Static | BindingFlags.Public);
            if (currentProp == null)
            {
                Debug.LogWarning("[OfficialPuckMod] Keyboard.current property not found.");
                return false;
            }

            var keyboardInstance = currentProp.GetValue(null);
            if (keyboardInstance == null) return false;

            // Build a likely property name for the key control (e.g., K -> kKey, Space -> spaceKey)
            string propName = spawnKey.ToString().ToLower() + "Key";
            var controlMember = keyboardInstance.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
                                ?? (MemberInfo)keyboardInstance.GetType().GetField(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (controlMember == null)
            {
                Debug.LogWarning($"[OfficialPuckMod] Keyboard does not expose a control named '{propName}'.");
                return false;
            }

            object control = null;
            if (controlMember is PropertyInfo pi) control = pi.GetValue(keyboardInstance);
            else if (controlMember is FieldInfo fi) control = fi.GetValue(keyboardInstance);
            if (control == null) return false;

            // KeyControl has a property wasPressedThisFrame
            var wasPressedProp = control.GetType().GetProperty("wasPressedThisFrame", BindingFlags.Instance | BindingFlags.Public);
            if (wasPressedProp == null)
            {
                Debug.LogWarning("[OfficialPuckMod] Key control does not have wasPressedThisFrame property.");
                return false;
            }

            var val = wasPressedProp.GetValue(control);
            if (val is bool b) return b;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        return false;
    }

    void SpawnTestStick()
    {
        try
        {
            Debug.Log("[OfficialPuckMod] TestStickSpawner: SpawnTestStick() called");
            var pm = NetworkBehaviourSingleton<PlayerManager>.Instance;
            if (pm == null)
            {
                Debug.LogWarning("[OfficialPuckMod] PlayerManager not found.");
                return;
            }

            var localPlayer = pm.GetLocalPlayer();
            if (localPlayer == null)
            {
                Debug.LogWarning("[OfficialPuckMod] Local player not found.");
                return;
            }

            // If running on server, use the server spawn so the stick is networked
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                Vector3 pos = localPlayer.transform.position + localPlayer.transform.TransformDirection(spawnOffset);
                Quaternion rot = Quaternion.identity;
                Debug.Log($"[OfficialPuckMod] TestStickSpawner: Server spawn at {pos}");
                localPlayer.Server_SpawnStick(pos, rot, localPlayer.Role.Value);
                Debug.Log("[OfficialPuckMod] Spawned networked test stick (server).");
                return;
            }

            // Otherwise, try to instantiate the local prefab for quick testing (non-networked)
            // The prefab fields are private; use reflection to access them on the Player instance
            Type playerType = localPlayer.GetType();
            FieldInfo prefabField = playerType.GetField("stickAttackerPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            Debug.Log($"[OfficialPuckMod] TestStickSpawner: prefabField found? {prefabField != null}");
            object prefabObj = null;
            if (prefabField != null)
            {
                prefabObj = prefabField.GetValue(localPlayer);
            }

            if (prefabObj == null)
            {
                Debug.LogWarning("[OfficialPuckMod] stickAttackerPrefab not found on Player (cannot spawn client-only stick).");
                return;
            }

            var stickPrefab = prefabObj as Stick;
            if (stickPrefab == null)
            {
                Debug.LogWarning($"[OfficialPuckMod] stickAttackerPrefab is not a Stick prefab. Actual type: {prefabObj.GetType().FullName}");
                return;
            }

            Vector3 spawnPos = localPlayer.transform.position + localPlayer.transform.TransformDirection(spawnOffset);
            Quaternion spawnRot = Quaternion.identity;
            var stickInstance = UnityEngine.Object.Instantiate<Stick>(stickPrefab, spawnPos, spawnRot);
            // Make it persist for testing; it's local-only and won't be networked
            stickInstance.name = "TestStick_Local";
            Debug.Log($"[OfficialPuckMod] Spawned local test stick (client-only) at {spawnPos}");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }
}
