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
        static bool Prefix(Dictionary<string, object> message)
        {
            try
            {
                if (message == null) return true;
                if (!message.ContainsKey("serverBrowserServer")) return true;
                var serverObj = message["serverBrowserServer"] as ServerBrowserServer;
                if (serverObj == null) return true;

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

                            // add a one-time listener to resume connection when the popup is closed
                            Action<Dictionary<string, object>> onHide = null;
                            onHide = (d) =>
                            {
                                try
                                {
                                    if (d == null) return;
                                    if (!d.ContainsKey("name")) return;
                                    var nameObj = d["name"] as string;
                                    if (nameObj == popupName)
                                    {
                                        MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPopupHide", onHide);
                                        // proceed to start client to the original server (Harmony will redirect if SelectedRelay is set)
                                        ConnectionManager.Instance.Client_StartClient(serverObj.ipAddress, serverObj.port, "");
                                    }
                                }
                                catch (Exception ex2)
                                {
                                    Debug.LogException(ex2);
                                }
                            };
                            MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPopupHide", onHide);
                            // do not run the original handler now; wait for popup close
                            return false;
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
            // allow original handler to run if we didn't specially handle the click
            return true;
        }
    }
}
