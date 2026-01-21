using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using UnityEngine.UIElements;
using System.Reflection;

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
                        var popupName = string.Format("relaySelect_{0}", serverObj.ipAddress.Replace('.', '_'));
                        var popupMgr = UIManager.Instance?.PopupManager;
                        if (popupMgr == null)
                        {
                            // no popup manager available; allow original handler
                            return true;
                        }

                        // Show a simple popup; we'll replace its content with buttons
                        var content = new PopupContentText(popupMgr.popupContentTextAsset, string.Format("Select a relay for {0}", serverObj.ipAddress));
                        popupMgr.ShowPopup(popupName, "Relay Options", content, true, true);

                        try
                        {
                            // Access UIPopupManager.activePopups via reflection to get the created Popup
                            var activeField = popupMgr.GetType().GetField("activePopups", BindingFlags.NonPublic | BindingFlags.Instance);
                            var activeObj = activeField?.GetValue(popupMgr);
                            var dict = activeObj as System.Collections.IDictionary;
                            if (dict != null && dict.Contains(popupName))
                            {
                                var popupObj = dict[popupName];
                                var popup = popupObj as Popup;
                                if (popup != null)
                                {
                                    var contentContainer = popup.VisualElement.Q("ContentContainer");
                                    if (contentContainer != null)
                                    {
                                        // Add an explicit option to use no relay (direct connection)
                                        var directBtn = new Button(() =>
                                        {
                                            try
                                            {
                                                RelayRouterHelpers.ClearSelectedRelay();
                                                var hideMethod = popupMgr.GetType().GetMethod("HidePopup", BindingFlags.Public | BindingFlags.Instance);
                                                hideMethod?.Invoke(popupMgr, new object[] { popupName });
                                                Debug.Log("[RelaySelectionUI] Selected direct connection (no relay)");
                                            }
                                            catch (Exception exBtn)
                                            {
                                                Debug.LogException(exBtn);
                                            }
                                        }) { text = "Direct connection (no relay)" };
                                        directBtn.style.marginBottom = 6;
                                        directBtn.style.borderTopWidth = 1;
                                        directBtn.style.borderBottomWidth = 1;
                                        directBtn.style.borderLeftWidth = 1;
                                        directBtn.style.borderRightWidth = 1;
                                        directBtn.style.borderTopColor = new UnityEngine.Color(0.6f, 0.6f, 0.6f, 1f);
                                        directBtn.style.borderBottomColor = new UnityEngine.Color(0.6f, 0.6f, 0.6f, 1f);
                                        directBtn.style.borderLeftColor = new UnityEngine.Color(0.6f, 0.6f, 0.6f, 1f);
                                        directBtn.style.borderRightColor = new UnityEngine.Color(0.6f, 0.6f, 0.6f, 1f);
                                        directBtn.style.paddingLeft = 6;
                                        directBtn.style.paddingRight = 6;
                                        directBtn.style.paddingTop = 4;
                                        directBtn.style.paddingBottom = 4;
                                        directBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
                                        contentContainer.Add(directBtn);

                                        if (entry.RelayOptions != null && entry.RelayOptions.Count > 0)
                                        {
                                            foreach (var r in entry.RelayOptions)
                                            {
                                                var relay = r; // closure copy
                                                var btn = new Button(() =>
                                                {
                                                    try
                                                    {
                                                        // set the selected relay so the connection will be redirected
                                                        RelayRouterHelpers.SetSelectedRelay(relay.Address, relay.Port, relay.Name);
                                                        // hide the popup to allow the onHide handler to resume the connection
                                                        var hideMethod = popupMgr.GetType().GetMethod("HidePopup", BindingFlags.Public | BindingFlags.Instance);
                                                        hideMethod?.Invoke(popupMgr, new object[] { popupName });
                                                        Debug.Log(string.Format("[RelaySelectionUI] Relay selected and popup hidden: {0} {1}:{2}", relay.Name, relay.Address, relay.Port));
                                                    }
                                                    catch (Exception exBtn)
                                                    {
                                                        Debug.LogException(exBtn);
                                                    }
                                                }) { text = string.Format("{0} ({1})", relay.Name, relay.Address) };
                                                btn.style.marginBottom = 4;
                                                // make button look like an outlined option
                                                btn.style.borderTopWidth = 1;
                                                btn.style.borderBottomWidth = 1;
                                                btn.style.borderLeftWidth = 1;
                                                btn.style.borderRightWidth = 1;
                                                btn.style.borderTopColor = new UnityEngine.Color(0.6f, 0.6f, 0.6f, 1f);
                                                btn.style.borderBottomColor = new UnityEngine.Color(0.6f, 0.6f, 0.6f, 1f);
                                                btn.style.borderLeftColor = new UnityEngine.Color(0.6f, 0.6f, 0.6f, 1f);
                                                btn.style.borderRightColor = new UnityEngine.Color(0.6f, 0.6f, 0.6f, 1f);
                                                btn.style.paddingLeft = 6;
                                                btn.style.paddingRight = 6;
                                                btn.style.paddingTop = 4;
                                                btn.style.paddingBottom = 4;
                                                btn.style.unityTextAlign = TextAnchor.MiddleLeft;
                                                contentContainer.Add(btn);
                                            }
                                        }
                                        else
                                        {
                                            var none = new Label("No relays available");
                                            none.style.marginBottom = 4;
                                            contentContainer.Add(none);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception injEx)
                        {
                            Debug.LogException(injEx);
                        }

                        // When popup hides, resume original connect flow
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
                                    ConnectionManager.Instance.Client_StartClient(serverObj.ipAddress, serverObj.port, "");
                                }
                            }
                            catch (Exception ex2)
                            {
                                Debug.LogException(ex2);
                            }
                        };
                        MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPopupHide", onHide);

                        // intercept original handler until popup closes
                        return false;
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
