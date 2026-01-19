using System;
using UnityEngine;
using HarmonyLib;

namespace JohnRelayMod
{
    // Minimal mod entry implementing IPuckMod. Initializes Harmony patches on enable.
    public class RelayRouterMod : IPuckMod
    {
        public bool OnEnable()
        {
            try
            {
                RelayRouterHelpers.Init();
                // Set debug relay on enable for testing (adjust fields in RelayRouterHelpers)
                RelayRouterHelpers.SetSelectedRelay(RelayRouterHelpers.DebugRelayAddress, RelayRouterHelpers.DebugRelayPort, RelayRouterHelpers.DebugRelayName);
                Debug.Log(string.Format("[RelayRouterMod] Enabled. Debug relay set to {0}:{1}", RelayRouterHelpers.DebugRelayAddress, RelayRouterHelpers.DebugRelayPort));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        public bool OnDisable()
        {
            try
            {
                // Clear debug relay when disabling
                RelayRouterHelpers.ClearSelectedRelay();
                RelayRouterHelpers.Shutdown();
                Debug.Log("[RelayRouterMod] Disabled.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }
    }

    static class RelayRouterHelpers
    {
        internal static readonly Harmony harmony = new Harmony("John.RelayRouterMod");

        // When set, this relay will be used to replace the target endpoint
        public static RelayServerConfig SelectedRelay = null;
        // Debug default values (edit as needed for testing)
        public static string DebugRelayAddress = "172.237.155.226";
        public static ushort DebugRelayPort = 7779;
        public static string DebugRelayName = "Debug Relay";

        public static void Init()
        {
            try
            {
                harmony.PatchAll();
                Debug.Log("[RelayRouterHelpers] Harmony patches applied.");
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
                Debug.Log("[RelayRouterHelpers] Harmony patches removed.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // Debug helpers to set/clear the selected relay at runtime for testing
        public static void SetSelectedRelay(string address, ushort port, string name = "Debug Relay")
        {
            try
            {
                SelectedRelay = new RelayServerConfig { Name = name, Address = address, Port = port };
                Debug.Log(string.Format("[RelayRouterHelpers] SelectedRelay set to {0}:{1} ({2})", address, port, name));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public static void ClearSelectedRelay()
        {
            try
            {
                SelectedRelay = null;
                Debug.Log("[RelayRouterHelpers] SelectedRelay cleared.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    // Simple relay config holder for prototyping
    public class RelayServerConfig
    {
        public string Name;
        public string Address;
        public ushort Port;
    }

    // Prefix patch on ConnectionManager.Client_StartClient to swap ip/port when a relay is selected
    [HarmonyPatch(typeof(ConnectionManager), "Client_StartClient")]
    static class ConnectionManager_Client_StartClient_Patch
    {
        static bool Prefix(ref string ipAddress, ref ushort port, string password)
        {
            try
            {
                var sel = RelayRouterHelpers.SelectedRelay;
                if (sel != null)
                {
                    Debug.Log(string.Format("[RelayRouterMod] Redirecting connection {0}:{1} -> {2}:{3}", ipAddress, port, sel.Address, sel.Port));
                    ipAddress = sel.Address;
                    port = sel.Port;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            // continue with original method
            return true;
        }
    }
}
