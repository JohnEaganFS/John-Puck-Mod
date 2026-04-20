using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using HarmonyLib;
using UnityEngine.UIElements;
using System.Reflection;
using System.Text;
using UnityEngine.Networking;

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

        // Public helper to open the relay selection popup for a given server IP/port
        public static void ShowRelaySelectionForServer(string ipAddress, ushort port)
        {
            try
            {
                var entry = RelayRouterHelpers.FindServerEntry(ipAddress, port);
                if (entry == null)
                {
                    Debug.Log(string.Format("[RelaySelectionUI] No relay entry registered for {0}:{1}", ipAddress, port));
                    return;
                }

                var popupMgr = UIManager.Instance?.PopupManager;
                if (popupMgr == null)
                {
                    Debug.Log("[RelaySelectionUI] PopupManager not available.");
                    return;
                }

                var popupName = string.Format("relaySelect_{0}", ipAddress.Replace('.', '_'));
                var content = new PopupContentText(popupMgr.popupContentTextAsset, "Select a relay.");
                popupMgr.ShowPopup(popupName, "Relay Options", content, true, true);

                try
                {
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
                                // Direct connection button
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
                                directBtn.style.paddingLeft = 12;
                                directBtn.style.paddingRight = 6;
                                directBtn.style.paddingTop = 4;
                                directBtn.style.paddingBottom = 4;
                                directBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
                                directBtn.RegisterCallback<MouseEnterEvent>((evt) =>
                                {
                                    try {
                                        directBtn.style.backgroundColor = new UnityEngine.Color(0.95f, 0.95f, 0.95f, 1f);
                                        directBtn.style.color = new UnityEngine.Color(0f, 0f, 0f, 1f);
                                    }
                                    catch (Exception) { }
                                });
                                directBtn.RegisterCallback<MouseLeaveEvent>((evt) =>
                                {
                                    try {
                                        directBtn.style.backgroundColor = new UnityEngine.Color(0f, 0f, 0f, 0f);
                                        directBtn.style.color = new UnityEngine.Color(1f, 1f, 1f, 1f);
                                    }
                                    catch (Exception) { }
                                });
                                contentContainer.Add(directBtn);

                                if (entry.RelayOptions != null && entry.RelayOptions.Count > 0)
                                {
                                    // Header row for columns
                                    var headerRow = new VisualElement();
                                    headerRow.style.flexDirection = FlexDirection.Row;
                                    headerRow.style.alignItems = Align.Center;
                                    var hProv = new Label("Provider");
                                    hProv.style.width = 550;
                                    hProv.style.unityTextAlign = TextAnchor.MiddleLeft;
                                    var hPing = new Label("Ping to Relay");
                                    hPing.style.width = 250;
                                    hPing.style.unityTextAlign = TextAnchor.MiddleLeft;
                                    headerRow.Add(hProv);
                                    headerRow.Add(hPing);
                                    contentContainer.Add(headerRow);

                                    foreach (var r in entry.RelayOptions)
                                    {
                                        var relay = r; // closure copy
                                        var btn = new Button(() =>
                                        {
                                            try
                                            {
                                                var evtMgr = MonoBehaviourSingleton<EventManager>.Instance;
                                                if (evtMgr != null)
                                                {
                                                    evtMgr.StartCoroutine(RegisterRelayAndSelect(relay, ipAddress, port, popupMgr, popupName));
                                                }
                                                else
                                                {
                                                    RelayRouterHelpers.SetSelectedRelay(relay.Address, relay.Port, relay.Address);
                                                    var hideMethod = popupMgr.GetType().GetMethod("HidePopup", BindingFlags.Public | BindingFlags.Instance);
                                                    hideMethod?.Invoke(popupMgr, new object[] { popupName });
                                                    Debug.Log(string.Format("[RelaySelectionUI] Relay selected (fallback): {0}:{1}", relay.Address, relay.Port));
                                                }
                                            }
                                            catch (Exception exBtn)
                                            {
                                                Debug.LogException(exBtn);
                                            }
                                          }) { text = string.Format("{0}:{1}", relay.Address, relay.Port) };
                                        btn.style.marginBottom = 4;
                                        btn.style.borderTopWidth = 1;
                                        btn.style.borderBottomWidth = 1;
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
                                                btn.style.paddingLeft = 12;
                                                btn.style.paddingRight = 6;
                                                btn.style.paddingTop = 4;
                                                btn.style.paddingBottom = 4;
                                                btn.style.unityTextAlign = TextAnchor.MiddleLeft;
                                                btn.text = "";
                                                var row = new VisualElement();
                                                row.style.flexDirection = FlexDirection.Row;
                                                row.style.alignItems = Align.Center;
                                                var providerLabel = new Label("");
                                                providerLabel.style.width = 600;
                                                providerLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                                                providerLabel.text = "";
                                                var pingLabel = new Label("...");
                                                pingLabel.style.width = 250;
                                                pingLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                                                try
                                                {
                                                    providerLabel.text = relay.Name ?? relay.Address ?? "";
                                                }
                                                catch { providerLabel.text = relay.Name ?? relay.Address ?? ""; }
                                                row.Add(providerLabel);
                                                row.Add(pingLabel);
                                                btn.Add(row);
                                                btn.RegisterCallback<MouseEnterEvent>((evt) =>
                                                {
                                                    try {
                                                        btn.style.backgroundColor = new UnityEngine.Color(0.95f, 0.95f, 0.95f, 1f);
                                                        btn.style.color = new UnityEngine.Color(0f, 0f, 0f, 1f);
                                                    }
                                                    catch (Exception) { }
                                                });
                                                btn.RegisterCallback<MouseLeaveEvent>((evt) =>
                                                {
                                                    try {
                                                        btn.style.backgroundColor = new UnityEngine.Color(0f, 0f, 0f, 0f);
                                                        btn.style.color = new UnityEngine.Color(1f, 1f, 1f, 1f);
                                                    }
                                                    catch (Exception) { }
                                                });
                                                contentContainer.Add(btn);
                                                try
                                                {
                                                    var ipForPing = relay.Address as string;
                                                    var evtMgr = MonoBehaviourSingleton<EventManager>.Instance;
                                                    if (evtMgr != null)
                                                    {
                                                        evtMgr.StartCoroutine(PingAndUpdate(ipForPing, pingLabel, relay.Address as string));
                                                    }
                                                }
                                                catch (Exception) { }
                                        
                                    }
                                }
                                else
                                {
                                    var none = new Label("No relays available");
                                    none.style.marginBottom = 4;
                                    none.style.paddingLeft = 12;
                                    none.style.unityTextAlign = TextAnchor.MiddleLeft;
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
                // When popup hides, resume connect flow to the provided ip/port
                Action<Dictionary<string, object>> onHide = null;
                onHide = (d) =>
                {
                    try
                    {
                        if (d == null) return;
                        var nameObj = d["name"] as string;
                        if (nameObj == popupName)
                        {
                            MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPopupHide", onHide);
                            ConnectionManager.Instance.Client_StartClient(ipAddress, port, "");
                        }
                    }
                    catch (Exception ex2)
                    {
                        Debug.LogException(ex2);
                    }
                };
                MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPopupHide", onHide);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public static IEnumerator PingAndUpdate(string ip, UnityEngine.UIElements.Label pingLabel, string name)
        {
            if (string.IsNullOrEmpty(ip) || pingLabel == null)
            {
                yield break;
            }

            UnityEngine.Ping p = null;
            try
            {
                p = new UnityEngine.Ping(ip);
            }
            catch (Exception)
            {
                yield break;
            }

            float start = UnityEngine.Time.realtimeSinceStartup;
            float timeout = 2.0f; // seconds
            while (!p.isDone && UnityEngine.Time.realtimeSinceStartup - start < timeout)
            {
                yield return null;
            }

            if (p.isDone)
            {
                try
                {
                    pingLabel.text = string.Format("{0} ms", p.time);
                }
                catch (Exception) { }
            }
            else
            {
                try
                {
                    pingLabel.text = string.Format("(timeout)");
                }
                catch (Exception) { }
            }
        }

        [Serializable]
        class RegisterResponse
        {
            public int external_port;
        }

        // NOTE: Production should use proper TLS with valid certificates.

        public static IEnumerator RegisterRelayAndSelect(RelayServerConfig relay, string serverIp, ushort serverPort, object popupMgr, string popupName)
        {
            if (relay == null || string.IsNullOrEmpty(relay.Address)) yield break;

            int apiPort = RelayRouterHelpers.RelayApiPort;
            string apiPath = RelayRouterHelpers.RelayRegisterPath ?? "/register";
            string body = "{\"target_ip\":\"" + serverIp + "\",\"target_port\":" + serverPort + "}";

            // Use `Domain` for API requests if provided, otherwise fall back to the relay Address.
            var requestHost = !string.IsNullOrEmpty(relay.Domain) ? relay.Domain : relay.Address;
            string httpsUrl = string.Format("https://{0}:{1}{2}", requestHost, apiPort, apiPath);
            string respText = null;

            // Log httpsURL being requested for easier debugging of connectivity issues
            Debug.Log(string.Format("[RelaySelectionUI] Attempting to register relay via HTTPS request to: {0}", httpsUrl));

            // Log body of the request for debugging purposes, but redact any sensitive tokens
            string logBody = body;
            if (logBody.Contains("X-Relay-Token"))
            {
                logBody = logBody.Replace(RelayRouterHelpers.RelayApiToken ?? "changeme", "REDACTED");
            }
            Debug.Log(string.Format("[RelaySelectionUI] HTTPS request body: {0}", logBody));

            UnityWebRequest uwr = null;
            try
            {
                uwr = new UnityWebRequest(httpsUrl, "POST");
                byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                uwr.uploadHandler = new UploadHandlerRaw(bodyRaw);
                uwr.downloadHandler = new DownloadHandlerBuffer();
                uwr.SetRequestHeader("Content-Type", "application/json");
                uwr.SetRequestHeader("X-Relay-Token", RelayRouterHelpers.RelayApiToken ?? "changeme");
                uwr.timeout = 10;
            }
            catch (InvalidOperationException ioe)
            {
                Debug.Log(string.Format("[RelaySelectionUI] HTTPS attempt threw during setup: {0}", ioe.Message));
                uwr = null;
            }

            if (uwr != null)
            {
                yield return uwr.SendWebRequest();

                bool error = false;
#if UNITY_2020_1_OR_NEWER
                error = uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError;
#else
                error = uwr.isNetworkError || uwr.isHttpError;
#endif
                if (!error)
                {
                    respText = uwr.downloadHandler != null ? uwr.downloadHandler.text : null;
                }
                else
                {
                    Debug.Log(string.Format("[RelaySelectionUI] HTTPS register attempt failed ({0}): {1}", relay.Address, uwr.error));
                    yield break;
                }
            }
            if (string.IsNullOrEmpty(respText))
            {
                Debug.Log(string.Format("[RelaySelectionUI] Empty register response from {0}", relay.Address));
                yield break;
            }

            try
            {
                var resp = JsonUtility.FromJson<RegisterResponse>(respText);
                if (resp != null && resp.external_port > 0)
                {
                    RelayRouterHelpers.SetSelectedRelay(relay.Address, (ushort)resp.external_port, relay.Address);
                    var hideMethod = popupMgr.GetType().GetMethod("HidePopup", BindingFlags.Public | BindingFlags.Instance);
                    hideMethod?.Invoke(popupMgr, new object[] { popupName });
                    Debug.Log(string.Format("[RelaySelectionUI] Relay registered and selected: {0} (api host: {1}) -> external:{2}", relay.Address, requestHost, resp.external_port));
                    yield break;
                }
                else
                {
                    Debug.Log(string.Format("[RelaySelectionUI] Register response missing external_port: {0}", respText));
                }
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
                        var content = new PopupContentText(popupMgr.popupContentTextAsset, string.Format("Select a relay."));
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
                                        directBtn.style.paddingLeft = 12;
                                        directBtn.style.paddingRight = 6;
                                        directBtn.style.paddingTop = 4;
                                        directBtn.style.paddingBottom = 4;
                                        directBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
                                        directBtn.RegisterCallback<MouseEnterEvent>((evt) =>
                                        {
                                            try {
                                                directBtn.style.backgroundColor = new UnityEngine.Color(0.95f, 0.95f, 0.95f, 1f);
                                                directBtn.style.color = new UnityEngine.Color(0f, 0f, 0f, 1f);
                                            }
                                            catch (Exception) { }
                                        });
                                        directBtn.RegisterCallback<MouseLeaveEvent>((evt) =>
                                        {
                                            try {
                                                directBtn.style.backgroundColor = new UnityEngine.Color(0f, 0f, 0f, 0f);
                                                directBtn.style.color = new UnityEngine.Color(1f, 1f, 1f, 1f);
                                            }
                                            catch (Exception) { }
                                        });
                                        contentContainer.Add(directBtn);

                                        if (entry.RelayOptions != null && entry.RelayOptions.Count > 0)
                                        {
                                                // Header row for columns
                                                var headerRow = new VisualElement();
                                                headerRow.style.flexDirection = FlexDirection.Row;
                                                headerRow.style.alignItems = Align.Center;
                                                var hProv = new Label("Provider");
                                                hProv.style.width = 550;
                                                hProv.style.unityTextAlign = TextAnchor.MiddleLeft;
                                                var hPing = new Label("Ping to Relay");
                                                hPing.style.width = 250;
                                                hPing.style.unityTextAlign = TextAnchor.MiddleLeft;
                                                headerRow.Add(hProv);
                                                headerRow.Add(hPing);
                                                contentContainer.Add(headerRow);

                                                foreach (var r in entry.RelayOptions)
                                            {
                                                var relay = r; // closure copy
                                                var btn = new Button(() =>
                                                {
                                                    try
                                                    {
                                                        var evtMgr = MonoBehaviourSingleton<EventManager>.Instance;
                                                        if (evtMgr != null)
                                                        {
                                                            evtMgr.StartCoroutine(RelaySelectionUI.RegisterRelayAndSelect(relay, serverObj.ipAddress, serverObj.port, popupMgr, popupName));
                                                        }
                                                        else
                                                        {
                                                            RelayRouterHelpers.SetSelectedRelay(relay.Address, relay.Port, relay.Address);
                                                            var hideMethod = popupMgr.GetType().GetMethod("HidePopup", BindingFlags.Public | BindingFlags.Instance);
                                                            hideMethod?.Invoke(popupMgr, new object[] { popupName });
                                                            Debug.Log(string.Format("[RelaySelectionUI] Relay selected (fallback) and popup hidden: {0} {1}:{2}", relay.Address, relay.Address, relay.Port));
                                                        }
                                                    }
                                                    catch (Exception exBtn)
                                                    {
                                                        Debug.LogException(exBtn);
                                                    }
                                                }) { text = string.Format("{0}:{1}", relay.Address, relay.Port) };
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
                                                btn.style.paddingLeft = 12;
                                                btn.style.paddingRight = 6;
                                                btn.style.paddingTop = 4;
                                                btn.style.paddingBottom = 4;
                                                btn.style.unityTextAlign = TextAnchor.MiddleLeft;
                                                btn.text = "";
                                                var row = new VisualElement();
                                                row.style.flexDirection = FlexDirection.Row;
                                                row.style.alignItems = Align.Center;
                                                var providerLabel = new Label("");
                                                providerLabel.style.width = 600;
                                                providerLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                                                providerLabel.text = "";
                                                var pingLabel = new Label("...");
                                                pingLabel.style.width = 250;
                                                pingLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                                                try
                                                {
                                                      providerLabel.text = relay.Name ?? relay.Address ?? "";
                                                }
                                                   catch { providerLabel.text = relay.Name ?? relay.Address ?? ""; }
                                                row.Add(providerLabel);
                                                row.Add(pingLabel);
                                                btn.Add(row);
                                                btn.RegisterCallback<MouseEnterEvent>((evt) =>
                                                {
                                                    try {
                                                        btn.style.backgroundColor = new UnityEngine.Color(0.95f, 0.95f, 0.95f, 1f);
                                                        btn.style.color = new UnityEngine.Color(0f, 0f, 0f, 1f);
                                                    }
                                                    catch (Exception) { }
                                                });
                                                btn.RegisterCallback<MouseLeaveEvent>((evt) =>
                                                {
                                                    try {
                                                        btn.style.backgroundColor = new UnityEngine.Color(0f, 0f, 0f, 0f);
                                                        btn.style.color = new UnityEngine.Color(1f, 1f, 1f, 1f);
                                                    }
                                                    catch (Exception) { }
                                                });
                                                contentContainer.Add(btn);
                                                try
                                                {
                                                    var ipForPing = relay.Address as string;
                                                    var evtMgr = MonoBehaviourSingleton<EventManager>.Instance;
                                                    if (evtMgr != null)
                                                    {
                                                        evtMgr.StartCoroutine(RelaySelectionUI.PingAndUpdate(ipForPing, pingLabel, relay.Address as string));
                                                    }
                                                }
                                                catch (Exception) { }
                                            }
                                        }
                                        else
                                        {
                                            var none = new Label("No relays available");
                                            none.style.marginBottom = 4;
                                            none.style.paddingLeft = 12;
                                            none.style.unityTextAlign = TextAnchor.MiddleLeft;
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
