using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;

namespace JohnRelayMod
{
    // Minimal UI/controller stub for selecting relays. Initialized by the mod on enable.
    public static class RelaySelectionUI
    {
        public static void Init()
        {
            try
            {
                Debug.Log("[RelaySelectionUI] Initialized.");
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
                Debug.Log("[RelaySelectionUI] Shutdown.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    // Patch the connection manager's server-browser click handler to log when a registered server is clicked
    [HarmonyPatch(typeof(ConnectionManagerController), "Event_Client_OnServerBrowserClickServer")]
    static class ConnectionManagerController_Event_Client_OnServerBrowserClickServer_Patch
    {
        static void Prefix(Dictionary<string, object> message)
        {
            try
            {
                if (message == null) return;
                if (!message.ContainsKey("serverBrowserServer")) return;
                var serverObj = message["serverBrowserServer"] as ServerBrowserServer;
                if (serverObj == null) return;

                var entry = RelayRouterHelpers.FindServerEntry(serverObj.ipAddress, serverObj.port);
                if (entry != null)
                {
                    Debug.Log(string.Format("[RelaySelectionUI] Clicked registered original server {0}:{1}", serverObj.ipAddress, serverObj.port));
                    try
                    {
                        // Build a simple text listing available relays
                        string text = string.Format("Original: {0}:{1}\n\nAvailable relays:\n", serverObj.ipAddress, serverObj.port);
                        if (entry.RelayOptions != null && entry.RelayOptions.Count > 0)
                        {
                            foreach (var r in entry.RelayOptions)
                            {
                                text += string.Format("- {0} ({1}:{2})\n", r.Name, r.Address, r.Port);
                            }
                        }
                        else
                        {
                            text += "(none)\n";
                        }

                        var popupName = string.Format("relaySelect_{0}_{1}", serverObj.ipAddress.Replace('.', '_'), serverObj.port);
                        var popupMgr = UIManager.Instance?.PopupManager;
                        if (popupMgr != null)
                        {
                            var content = new PopupContentText(popupMgr.popupContentTextAsset, text);
                            popupMgr.ShowPopup(popupName, "Relay Options", content, true, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
