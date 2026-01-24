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
                InServerRelaySelectionUI.Init();
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
                InServerRelaySelectionUI.Shutdown();
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
        
        // Track the client's last attempted connection target (original ip/port)
        public static string ClientLastTargetAddress = null;
        public static ushort ClientLastTargetPort = 0;

        public static void SetClientLastTarget(string address, ushort port)
        {
            try
            {
                ClientLastTargetAddress = address;
                ClientLastTargetPort = port;
                Debug.Log(string.Format("[RelayRouterHelpers] ClientLastTarget set to {0}:{1}", address, port));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // Global list of known relays you can add to; seeded with a debug relay
        public static List<RelayServerConfig> KnownRelays_Linode = new List<RelayServerConfig>
        {
            new RelayServerConfig { Name = "Linode (Chicago 1)", Address = "172.236.114.212", Port = 8010 },
            new RelayServerConfig { Name = "Clouvider (Chicago 1)", Address = "193.239.237.67", Port = 8010 },
            new RelayServerConfig { Name = "Cherry (Chicago 1)", Address = "84.32.131.121", Port = 8010 }
        };

        public static List<RelayServerConfig> KnownRelays_Clouvider = new List<RelayServerConfig>
        {
            new RelayServerConfig { Name = "Linode (Chicago 1)", Address = "172.236.114.212", Port = 8011 },
            new RelayServerConfig { Name = "Cherry (Chicago 1)", Address = "84.32.131.121", Port = 8011 }
        };

        // PHL Official 1
        public static List<RelayServerConfig> Relays_PHLOfficial1 = new List<RelayServerConfig>
        {
            new RelayServerConfig { Name = "Vultr (Atlanta)", Address = "96.30.192.184", Port = 9001 }, // Vultr (Atlanta)
            new RelayServerConfig { Name = "Vultr (Chicago)", Address = "149.28.119.78", Port = 9001 },  // Vultr (Chicago)
            new RelayServerConfig { Name = "Vultr (Dallas)", Address = "149.28.247.103", Port = 9001 },  // Vultr (Dallas)
            new RelayServerConfig { Name = "Clouvider (Chicago)", Address = "193.239.237.14", Port = 9001 },  // Clouvider (Chicago)
            new RelayServerConfig { Name = "Clouvider (Los Angeles)", Address = "45.149.172.59", Port = 9001 },  // Clouvider (Los Angeles)
            new RelayServerConfig { Name = "Clouvider (Virginia)", Address = "77.247.127.41", Port = 9001 },  // Clouvider (Virginia)
            new RelayServerConfig { Name = "OVH (Los Angeles)", Address = "40.160.254.25", Port = 9001 },  // OVH (Los Angeles)
            new RelayServerConfig { Name = "OVH (New York)", Address = "40.160.231.161", Port = 9001 },  // OVH (New York)
            new RelayServerConfig { Name = "OVH (Virginia)", Address = "15.204.223.103", Port = 9001 }  // OVH (Virginia)
        };

        // PHL Official 2
        public static List<RelayServerConfig> Relays_PHLOfficial2 = new List<RelayServerConfig>
        {
            new RelayServerConfig { Name = "Vultr (Atlanta)", Address = "96.30.192.184", Port = 9002 }, // Vultr (Atlanta)
            new RelayServerConfig { Name = "Vultr (Chicago)", Address = "149.28.119.78", Port = 9002 },  // Vultr (Chicago)
            new RelayServerConfig { Name = "Vultr (Dallas)", Address = "149.28.247.103", Port = 9002 },  // Vultr (Dallas)
            new RelayServerConfig { Name = "Clouvider (Chicago)", Address = "193.239.237.14", Port = 9002 },  // Clouvider (Chicago)
            new RelayServerConfig { Name = "Clouvider (Los Angeles)", Address = "45.149.172.59", Port = 9002 },  // Clouvider (Los Angeles)
            new RelayServerConfig { Name = "Clouvider (Virginia)", Address = "77.247.127.41", Port = 9002 },  // Clouvider (Virginia)
            new RelayServerConfig { Name = "OVH (Los Angeles)", Address = "40.160.254.25", Port = 9002 },  // OVH (Los Angeles)
            new RelayServerConfig { Name = "OVH (New York)", Address = "40.160.231.161", Port = 9002 },  // OVH (New York)
            new RelayServerConfig { Name = "OVH (Virginia)", Address = "15.204.223.103", Port = 9002 }  // OVH (Virginia)
        };

        // PHL Official 3
        public static List<RelayServerConfig> Relays_PHLOfficial3 = new List<RelayServerConfig>
        {
            new RelayServerConfig { Name = "Vultr (Atlanta)", Address = "96.30.192.184", Port = 9003 }, // Vultr (Atlanta)
            new RelayServerConfig { Name = "Vultr (Chicago)", Address = "149.28.119.78", Port = 9003 },  // Vultr (Chicago)
            new RelayServerConfig { Name = "Vultr (Dallas)", Address = "149.28.247.103", Port = 9003 },  // Vultr (Dallas)
            new RelayServerConfig { Name = "Clouvider (Chicago)", Address = "193.239.237.14", Port = 9003 },  // Clouvider (Chicago)
            new RelayServerConfig { Name = "Clouvider (Los Angeles)", Address = "45.149.172.59", Port = 9003 },  // Clouvider (Los Angeles)
            new RelayServerConfig { Name = "Clouvider (Virginia)", Address = "77.247.127.41", Port = 9003 },  // Clouvider (Virginia)
            new RelayServerConfig { Name = "OVH (Los Angeles)", Address = "40.160.254.25", Port = 9003 },  // OVH (Los Angeles)
            new RelayServerConfig { Name = "OVH (New York)", Address = "40.160.231.161", Port = 9003 },  // OVH (New York)
            new RelayServerConfig { Name = "OVH (Virginia)", Address = "15.204.223.103", Port = 9003 }  // OVH (Virginia)
        };

        // PHL Official 4
        public static List<RelayServerConfig> Relays_PHLOfficial4 = new List<RelayServerConfig>
        {
            new RelayServerConfig { Name = "Vultr (Atlanta)", Address = "96.30.192.184", Port = 9004 }, // Vultr (Atlanta)
            new RelayServerConfig { Name = "Vultr (Chicago)", Address = "149.28.119.78", Port = 9004 },  // Vultr (Chicago)
            new RelayServerConfig { Name = "Vultr (Dallas)", Address = "149.28.247.103", Port = 9004 },  // Vultr (Dallas)
            new RelayServerConfig { Name = "Clouvider (Chicago)", Address = "193.239.237.14", Port = 9004 },  // Clouvider (Chicago)
            new RelayServerConfig { Name = "Clouvider (Los Angeles)", Address = "45.149.172.59", Port = 9004 },  // Clouvider (Los Angeles)
            new RelayServerConfig { Name = "Clouvider (Virginia)", Address = "77.247.127.41", Port = 9004 },  // Clouvider (Virginia)
            new RelayServerConfig { Name = "OVH (Los Angeles)", Address = "40.160.254.25", Port = 9004 },  // OVH (Los Angeles)
            new RelayServerConfig { Name = "OVH (New York)", Address = "40.160.231.161", Port = 9004 },  // OVH (New York)
            new RelayServerConfig { Name = "OVH (Virginia)", Address = "15.204.223.103", Port = 9004 }  // OVH (Virginia)
        };

        // PHL Official 5
        public static List<RelayServerConfig> Relays_PHLOfficial5 = new List<RelayServerConfig>
        {
            new RelayServerConfig { Name = "Vultr (Atlanta)", Address = "96.30.192.184", Port = 9005 }, // Vultr (Atlanta)
            new RelayServerConfig { Name = "Vultr (Chicago)", Address = "149.28.119.78", Port = 9005 },  // Vultr (Chicago)
            new RelayServerConfig { Name = "Vultr (Dallas)", Address = "149.28.247.103", Port = 9005 },  // Vultr (Dallas)
            new RelayServerConfig { Name = "Clouvider (Chicago)", Address = "193.239.237.14", Port = 9005 },  // Clouvider (Chicago)
            new RelayServerConfig { Name = "Clouvider (Los Angeles)", Address = "45.149.172.59", Port = 9005 },  // Clouvider (Los Angeles)
            new RelayServerConfig { Name = "Clouvider (Virginia)", Address = "77.247.127.41", Port = 9005 },  // Clouvider (Virginia)
            new RelayServerConfig { Name = "OVH (Los Angeles)", Address = "40.160.254.25", Port = 9005 },  // OVH (Los Angeles)
            new RelayServerConfig { Name = "OVH (New York)", Address = "40.160.231.161", Port = 9005 },  // OVH (New York)
            new RelayServerConfig { Name = "OVH (Virginia)", Address = "15.204.223.103", Port = 9005 }  // OVH (Virginia)
        };

        // PHL Official 6
        public static List<RelayServerConfig> Relays_PHLOfficial6 = new List<RelayServerConfig>
        {
            new RelayServerConfig { Name = "Vultr (Atlanta)", Address = "96.30.192.184", Port = 9006 }, // Vultr (Atlanta)
            new RelayServerConfig { Name = "Vultr (Chicago)", Address = "149.28.119.78", Port = 9006 },  // Vultr (Chicago)
            new RelayServerConfig { Name = "Vultr (Dallas)", Address = "149.28.247.103", Port = 9006 },  // Vultr (Dallas)
            new RelayServerConfig { Name = "Clouvider (Chicago)", Address = "193.239.237.14", Port = 9006 },  // Clouvider (Chicago)
            new RelayServerConfig { Name = "Clouvider (Los Angeles)", Address = "45.149.172.59", Port = 9006 },  // Clouvider (Los Angeles)
            new RelayServerConfig { Name = "Clouvider (Virginia)", Address = "77.247.127.41", Port = 9006 },  // Clouvider (Virginia)
            new RelayServerConfig { Name = "OVH (Los Angeles)", Address = "40.160.254.25", Port = 9006 },  // OVH (Los Angeles)
            new RelayServerConfig { Name = "OVH (New York)", Address = "40.160.231.161", Port = 9006 },  // OVH (New York)
            new RelayServerConfig { Name = "OVH (Virginia)", Address = "15.204.223.103", Port = 9006 }  // OVH (Virginia)
        };

        // PHL Official 7
        public static List<RelayServerConfig> Relays_PHLOfficial7 = new List<RelayServerConfig>
        {
            new RelayServerConfig { Name = "Vultr (Atlanta)", Address = "96.30.192.184", Port = 9007 }, // Vultr (Atlanta)
            new RelayServerConfig { Name = "Vultr (Chicago)", Address = "149.28.119.78", Port = 9007 },  // Vultr (Chicago)
            new RelayServerConfig { Name = "Vultr (Dallas)", Address = "149.28.247.103", Port = 9007 },  // Vultr (Dallas)
            new RelayServerConfig { Name = "Clouvider (Chicago)", Address = "193.239.237.14", Port = 9007 },  // Clouvider (Chicago)
            new RelayServerConfig { Name = "Clouvider (Los Angeles)", Address = "45.149.172.59", Port = 9007 },  // Clouvider (Los Angeles)
            new RelayServerConfig { Name = "Clouvider (Virginia)", Address = "77.247.127.41", Port = 9007 },  // Clouvider (Virginia)
            new RelayServerConfig { Name = "OVH (Los Angeles)", Address = "40.160.254.25", Port = 9007 },  // OVH (Los Angeles)
            new RelayServerConfig { Name = "OVH (New York)", Address = "40.160.231.161", Port = 9007 },  // OVH (New York)
            new RelayServerConfig { Name = "OVH (Virginia)", Address = "15.204.223.103", Port = 9007 }  // OVH (Virginia)
        };

        // PHL Official 8
        public static List<RelayServerConfig> Relays_PHLOfficial8 = new List<RelayServerConfig>
        {
            new RelayServerConfig { Name = "Vultr (Atlanta)", Address = "96.30.192.184", Port = 9008 }, // Vultr (Atlanta)
            new RelayServerConfig { Name = "Vultr (Chicago)", Address = "149.28.119.78", Port = 9008 },  // Vultr (Chicago)
            new RelayServerConfig { Name = "Vultr (Dallas)", Address = "149.28.247.103", Port = 9008 },  // Vultr (Dallas)
            new RelayServerConfig { Name = "Clouvider (Chicago)", Address = "193.239.237.14", Port = 9008 },  // Clouvider (Chicago)
            new RelayServerConfig { Name = "Clouvider (Los Angeles)", Address = "45.149.172.59", Port = 9008 },  // Clouvider (Los Angeles)
            new RelayServerConfig { Name = "Clouvider (Virginia)", Address = "77.247.127.41", Port = 9008 },  // Clouvider (Virginia)
            new RelayServerConfig { Name = "OVH (Los Angeles)", Address = "40.160.254.25", Port = 9008 },  // OVH (Los Angeles)
            new RelayServerConfig { Name = "OVH (New York)", Address = "40.160.231.161", Port = 9008 },  // OVH (New York)
            new RelayServerConfig { Name = "OVH (Virginia)", Address = "15.204.223.103", Port = 9008 }  // OVH (Virginia)
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

        // Register a ServerRelayEntry programmatically
        public static void RegisterServerEntry(ServerRelayEntry entry)
        {
            if (entry == null) return;
            ServerRegistry.Add(entry);
        }

        // Create and register a server entry, preferring per-server configured relays and falling back to a provided list
        public static ServerRelayEntry CreateAndRegisterServerEntry(string originalAddress, ushort originalPort, List<RelayServerConfig> fallbackRelays = null)
        {
            var entry = new ServerRelayEntry(originalAddress, originalPort);
            var key = ServerKey(originalAddress, originalPort);
            if (KnownRelaysByServer.TryGetValue(key, out var perList) && perList != null && perList.Count > 0)
            {
                entry.RelayOptions.AddRange(perList);
            }
            else if (fallbackRelays != null && fallbackRelays.Count > 0)
            {
                entry.RelayOptions.AddRange(fallbackRelays);
            }
            ServerRegistry.Add(entry);
            return entry;
        }

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

                // Add server entries using helper that prefers per-server lists and falls back to seeded lists
                CreateAndRegisterServerEntry("172.237.155.226", 7779, KnownRelays_Linode); // Linode server as original
                CreateAndRegisterServerEntry("193.239.237.67", 7779, KnownRelays_Clouvider); // Clouvider server as original
                CreateAndRegisterServerEntry("216.128.144.141", 7777, Relays_PHLOfficial1); // PHL Official 1
                CreateAndRegisterServerEntry("216.128.144.141", 7779, Relays_PHLOfficial2); // PHL Official 2
                CreateAndRegisterServerEntry("216.128.144.141", 7781, Relays_PHLOfficial3); // PHL Official 3
                CreateAndRegisterServerEntry("216.128.144.141", 7783, Relays_PHLOfficial4); // PHL Official 4
                CreateAndRegisterServerEntry("216.128.145.10", 7777, Relays_PHLOfficial5); // PHL Official 5
                CreateAndRegisterServerEntry("216.128.145.10", 7779, Relays_PHLOfficial6); // PHL Official 6
                CreateAndRegisterServerEntry("216.128.145.10", 7781, Relays_PHLOfficial7); // PHL Official 7
                CreateAndRegisterServerEntry("216.128.145.10", 7783, Relays_PHLOfficial8); // PHL Official 8
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
                // Debug log original target
                Debug.Log(string.Format("[RelayRouterMod] Original connection target: {0}:{1}", ipAddress, port));
                // Only consider redirecting if the destination matches a registered original server
                var serverEntry = RelayRouterHelpers.FindServerEntry(ipAddress, port);
                if (serverEntry != null)
                {
                    // Track the client's last attempted target
                    RelayRouterHelpers.SetClientLastTarget(ipAddress, port);
                    Debug.Log(string.Format("[ClientLastTarget] Set to {0}:{1}", ipAddress, port));
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
