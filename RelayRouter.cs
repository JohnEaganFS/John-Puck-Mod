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
                if (RelayRouterHelpers.GlobalKnownRelays != null && RelayRouterHelpers.GlobalKnownRelays.Count > 0)
                {
                    var first = RelayRouterHelpers.GlobalKnownRelays[0];
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
        public static List<RelayServerConfig> KnownRelays_Linode = new List<RelayServerConfig>
        {
            new RelayServerConfig { Name = "Linode (Chicago 1)", Address = "172.236.114.212", Port = 8010 },
            new RelayServerConfig { Name = "Clouvider (Chicago 1)", Address = "193.239.237.67", Port = 8010 },
            new RelayServerConfig { Name = "Cherry (Chicago 1)", Address = "84.32.131.121", Port = 8010 },
            new RelayServerConfig { Name = "Vultr (Chicago 1)", Address = "149.28.119.78", Port = 8010 }
        };

        public static List<RelayServerConfig> KnownRelays_Clouvider = new List<RelayServerConfig>
        {
            new RelayServerConfig { Name = "Linode (Chicago 1)", Address = "172.236.114.212", Port = 8011 },
            new RelayServerConfig { Name = "Cherry (Chicago 1)", Address = "84.32.131.121", Port = 8011 },
            new RelayServerConfig { Name = "Vultr (Chicago 1)", Address = "149.28.119.78", Port = 8011 }
        };

        // Global fallback list of known relays (generic)
        public static List<RelayServerConfig> GlobalKnownRelays = new List<RelayServerConfig>();

        // Per-original-server mapping of known relays. Key format: "address:port"
        public static Dictionary<string, List<RelayServerConfig>> KnownRelaysByServer = new Dictionary<string, List<RelayServerConfig>>();

        private static string ServerKey(string address, ushort port) => string.Format("{0}:{1}", address, port);

        // Register a global known relay
        public static void RegisterKnownRelay(RelayServerConfig relay)
        {
            if (relay == null) return;
            GlobalKnownRelays.Add(relay);
        }

        public static void RegisterKnownRelay(string name, string address, ushort port)
        {
            GlobalKnownRelays.Add(new RelayServerConfig { Name = name, Address = address, Port = port });
        }

        // Register a relay specifically for an original server (originalAddress:originalPort)
        public static void RegisterKnownRelayForServer(string originalAddress, ushort originalPort, RelayServerConfig relay)
        {
            if (relay == null) return;
            var key = ServerKey(originalAddress, originalPort);
            if (!KnownRelaysByServer.ContainsKey(key)) KnownRelaysByServer[key] = new List<RelayServerConfig>();
            KnownRelaysByServer[key].Add(relay);
        }

        public static void RegisterKnownRelayForServer(string originalAddress, ushort originalPort, string name, string address, ushort port)
        {
            RegisterKnownRelayForServer(originalAddress, originalPort, new RelayServerConfig { Name = name, Address = address, Port = port });
        }

        // Find a known relay by name in the global list
        public static RelayServerConfig FindKnownRelayByName(string name)
        {
            foreach (var r in GlobalKnownRelays)
                if (string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
                    return r;
            return null;
        }

        // Find a known relay by name scoped to an original server
        public static RelayServerConfig FindKnownRelayByNameForServer(string originalAddress, ushort originalPort, string name)
        {
            var key = ServerKey(originalAddress, originalPort);
            if (KnownRelaysByServer.TryGetValue(key, out var list))
            {
                foreach (var r in list)
                    if (string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
                        return r;
            }
            return null;
        }

        // Remove a global known relay by name
        public static bool RemoveKnownRelayByName(string name)
        {
            var r = FindKnownRelayByName(name);
            if (r != null) return GlobalKnownRelays.Remove(r);
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

                var entry_1 = new ServerRelayEntry("172.237.155.226", 7779); // Linode server as original
                var entry_2 = new ServerRelayEntry("193.239.237.67", 7779); // Clouvider server as original
                // Prefer per-server configured lists, fall back to the pre-seeded lists
                var key1 = ServerKey(entry_1.OriginalAddress, entry_1.OriginalPort);
                if (KnownRelaysByServer.TryGetValue(key1, out var perList1) && perList1 != null && perList1.Count > 0)
                {
                    foreach (var r in perList1) entry_1.RelayOptions.Add(r);
                }
                else if (KnownRelays_Linode != null && KnownRelays_Linode.Count > 0)
                {
                    foreach (var r in KnownRelays_Linode) entry_1.RelayOptions.Add(r);
                }

                var key2 = ServerKey(entry_2.OriginalAddress, entry_2.OriginalPort);
                if (KnownRelaysByServer.TryGetValue(key2, out var perList2) && perList2 != null && perList2.Count > 0)
                {
                    foreach (var r in perList2) entry_2.RelayOptions.Add(r);
                }
                else if (KnownRelays_Clouvider != null && KnownRelays_Clouvider.Count > 0)
                {
                    foreach (var r in KnownRelays_Clouvider) entry_2.RelayOptions.Add(r);
                }
                ServerRegistry.Add(entry_1);
                ServerRegistry.Add(entry_2);
                Debug.Log(string.Format("[RelayRouterHelpers] Server registry seeded with {0}:{1}", entry_1.OriginalAddress, entry_1.OriginalPort));
                Debug.Log(string.Format("[RelayRouterHelpers] Server registry seeded with {0}:{1}", entry_2.OriginalAddress, entry_2.OriginalPort));
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
