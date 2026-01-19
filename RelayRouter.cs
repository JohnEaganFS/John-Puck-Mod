using System;
using System.Collections.Generic;
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
                // Set first known relay on enable for testing (if any)
                if (RelayRouterHelpers.KnownRelays != null && RelayRouterHelpers.KnownRelays.Count > 0)
                {
                    var first = RelayRouterHelpers.KnownRelays[0];
                    RelayRouterHelpers.SetSelectedRelay(first.Address, first.Port, first.Name);
                    Debug.Log(string.Format("[RelayRouterMod] Enabled. Selected relay set to {0}:{1} ({2})", first.Address, first.Port, first.Name));
                }
                // Initialize the relay selection UI/controller
                RelaySelectionUI.Init();
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
                // Shutdown the relay selection UI/controller
                RelaySelectionUI.Shutdown();
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

        // Global list of known relays you can add to; seeded with a debug relay
        public static List<RelayServerConfig> KnownRelays = new List<RelayServerConfig>
        {
            new RelayServerConfig { Name = "Linode (Chicago 1)", Address = "172.236.114.212", Port = 8010 },
            new RelayServerConfig { Name = "Clouvider (Chicago 1)", Address = "193.239.237.67", Port = 8010 }
        };

        // Helpers to manage the KnownRelays list
        public static void RegisterKnownRelay(RelayServerConfig relay)
        {
            if (relay == null) return;
            KnownRelays.Add(relay);
        }

        public static void RegisterKnownRelay(string name, string address, ushort port)
        {
            KnownRelays.Add(new RelayServerConfig { Name = name, Address = address, Port = port });
        }

        public static RelayServerConfig FindKnownRelayByName(string name)
        {
            foreach (var r in KnownRelays)
                if (string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
                    return r;
            return null;
        }

        public static bool RemoveKnownRelayByName(string name)
        {
            var r = FindKnownRelayByName(name);
            if (r != null) return KnownRelays.Remove(r);
            return false;
        }

        // In-code registry of known original servers and their candidate relays
        public static List<ServerRelayEntry> ServerRegistry = new List<ServerRelayEntry>();

        public static void Init()
        {
            try
            {
                harmony.PatchAll();
                Debug.Log("[RelayRouterHelpers] Harmony patches applied.");
                // populate a small in-code registry if empty
                PopulateServerRegistryIfEmpty();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // Populate a minimal registry with the known game server and a debug relay option
        private static void PopulateServerRegistryIfEmpty()
        {
            try
            {
                if (ServerRegistry.Count > 0) return;

                var entry = new ServerRelayEntry("172.237.155.226", 7779);
                // add the first known relay as an available option for this original server (if present)
                if (KnownRelays != null && KnownRelays.Count > 0)
                {
                    for (int i = 0; i < KnownRelays.Count; i++)
                    {
                        entry.RelayOptions.Add(KnownRelays[i]);
                    }
                }
                ServerRegistry.Add(entry);
                Debug.Log(string.Format("[RelayRouterHelpers] Server registry seeded with {0}:{1}", entry.OriginalAddress, entry.OriginalPort));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // Find a registry entry matching the original server ip/port
        public static ServerRelayEntry FindServerEntry(string originalAddress, ushort originalPort)
        {
            foreach (var s in ServerRegistry)
            {
                if (string.Equals(s.OriginalAddress, originalAddress, StringComparison.OrdinalIgnoreCase) && s.OriginalPort == originalPort)
                    return s;
            }
            return null;
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

    // Represents an original server endpoint and a set of possible relay endpoints
    public class ServerRelayEntry
    {
        public string OriginalAddress;
        public ushort OriginalPort;
        public List<RelayServerConfig> RelayOptions = new List<RelayServerConfig>();

        public ServerRelayEntry(string originalAddress, ushort originalPort)
        {
            OriginalAddress = originalAddress;
            OriginalPort = originalPort;
        }
    }

    // Prefix patch on ConnectionManager.Client_StartClient to swap ip/port when a relay is selected
    [HarmonyPatch(typeof(ConnectionManager), "Client_StartClient")]
    static class ConnectionManager_Client_StartClient_Patch
    {
        static bool Prefix(ref string ipAddress, ref ushort port, string password)
        {
            try
            {
                // Only consider redirecting if the destination matches a registered original server
                var serverEntry = RelayRouterHelpers.FindServerEntry(ipAddress, port);
                if (serverEntry != null)
                {
                    var sel = RelayRouterHelpers.SelectedRelay;
                    if (sel != null)
                    {
                        Debug.Log(string.Format("[RelayRouterMod] Redirecting connection {0}:{1} -> {2}:{3}", ipAddress, port, sel.Address, sel.Port));
                        ipAddress = sel.Address;
                        port = sel.Port;
                    }
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
