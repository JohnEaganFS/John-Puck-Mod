using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace JohnRelayMod
{
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

        public static void ShowRelaySelectionForServer(string ipAddress, ushort port, string password = null)
        {
            try
            {
                var entry = RelayRouterHelpers.FindServerEntry(ipAddress, port);
                if (entry == null)
                {
                    Debug.Log(string.Format("[RelaySelectionUI] No relay entry registered for {0}:{1}", ipAddress, port));
                    return;
                }

                var popupMgr = UIManager.Instance != null ? UIManager.Instance.PopupManager : null;
                if (popupMgr == null)
                {
                    Debug.Log("[RelaySelectionUI] PopupManager not available.");
                    return;
                }

                RelayRouterHelpers.ClearSelectedRelay();

                string popupName = GetPopupName(ipAddress, port);
                if (popupMgr.GetPopupByName(popupName) != null)
                {
                    popupMgr.HidePopup(popupName);
                }

                var content = popupMgr.CreateNotificationContent("Select a relay.");
                popupMgr.ShowPopup(popupName, "RELAY OPTIONS", content, false, true, null);

                var popup = popupMgr.GetPopupByName(popupName);
                var contentRoot = popup != null && popup.Content != null ? popup.Content.VisualElement : null;
                if (contentRoot == null)
                {
                    Debug.Log("[RelaySelectionUI] Popup content root not available.");
                    return;
                }

                BuildRelayOptions(contentRoot, entry, ipAddress, port, password, popupMgr, popupName);
                RegisterConnectOnPopupHide(popupName, ipAddress, port, password);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static string GetPopupName(string ipAddress, ushort port)
        {
            return string.Format("relaySelect_{0}_{1}", ipAddress.Replace('.', '_').Replace(':', '_'), port);
        }

        private static void BuildRelayOptions(VisualElement contentRoot, ServerRelayEntry entry, string ipAddress, ushort port, string password, UIPopupManager popupMgr, string popupName)
        {
            contentRoot.Clear();

            var directBtn = new Button(() =>
            {
                try
                {
                    RelayRouterHelpers.ClearSelectedRelay();
                    popupMgr.HidePopup(popupName);
                    Debug.Log("[RelaySelectionUI] Selected direct connection.");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }) { text = "Direct connection" };
            StyleOptionButton(directBtn);
            contentRoot.Add(directBtn);

            if (entry.RelayOptions == null || entry.RelayOptions.Count == 0)
            {
                var none = new Label("No relays available");
                none.style.marginTop = 6;
                none.style.unityTextAlign = TextAnchor.MiddleLeft;
                contentRoot.Add(none);
                return;
            }

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.marginTop = 8;
            headerRow.style.marginBottom = 4;

            var providerHeader = new Label("Provider");
            providerHeader.style.width = 360;
            providerHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            var pingHeader = new Label("Ping");
            pingHeader.style.width = 120;
            pingHeader.style.unityFontStyleAndWeight = FontStyle.Bold;

            headerRow.Add(providerHeader);
            headerRow.Add(pingHeader);
            contentRoot.Add(headerRow);

            foreach (var relayConfig in entry.RelayOptions)
            {
                var relay = relayConfig;
                var pingLabel = new Label("...");
                pingLabel.style.width = 120;
                pingLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

                var btn = new Button(() =>
                {
                    try
                    {
                        RelayRouterHelpers.StartCoroutine(RegisterRelayAndSelect(relay, ipAddress, port, popupMgr, popupName));
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                });
                StyleOptionButton(btn);

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;

                var providerLabel = new Label(relay.Name ?? relay.Address ?? "Relay");
                providerLabel.style.width = 360;
                providerLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

                row.Add(providerLabel);
                row.Add(pingLabel);
                btn.Add(row);
                contentRoot.Add(btn);

                RelayRouterHelpers.StartCoroutine(PingAndUpdate(relay.Address, pingLabel));
            }
        }

        private static void StyleOptionButton(Button button)
        {
            button.style.marginBottom = 4;
            button.style.borderTopWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            button.style.borderBottomColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            button.style.borderLeftColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            button.style.borderRightColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            button.style.paddingLeft = 12;
            button.style.paddingRight = 8;
            button.style.paddingTop = 4;
            button.style.paddingBottom = 4;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;

            button.RegisterCallback<MouseEnterEvent>((evt) =>
            {
                button.style.backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f);
                button.style.color = Color.black;
            });
            button.RegisterCallback<MouseLeaveEvent>((evt) =>
            {
                button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
                button.style.color = Color.white;
            });
        }

        private static void RegisterConnectOnPopupHide(string popupName, string ipAddress, ushort port, string password)
        {
            Action<Dictionary<string, object>> onHide = null;
            onHide = (message) =>
            {
                try
                {
                    if (message == null || !message.ContainsKey("name")) return;
                    var name = message["name"] as string;
                    if (name != popupName) return;

                    EventManager.RemoveEventListener("Event_OnPopupHide", onHide);
                    ConnectionManager.Instance.Client_StartClient(ipAddress, port, password);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            };

            EventManager.AddEventListener("Event_OnPopupHide", onHide);
        }

        public static IEnumerator PingAndUpdate(string ip, Label pingLabel)
        {
            if (string.IsNullOrEmpty(ip) || pingLabel == null)
            {
                yield break;
            }

            UnityEngine.Ping ping = null;
            try
            {
                ping = new UnityEngine.Ping(ip);
            }
            catch (Exception)
            {
                yield break;
            }

            float start = Time.realtimeSinceStartup;
            const float timeout = 2.0f;
            while (!ping.isDone && Time.realtimeSinceStartup - start < timeout)
            {
                yield return null;
            }

            pingLabel.text = ping.isDone ? string.Format("{0} ms", ping.time) : "(timeout)";
        }

        [Serializable]
        private class RegisterResponse
        {
            public int external_port = 0;
        }

        public static IEnumerator RegisterRelayAndSelect(RelayServerConfig relay, string serverIp, ushort serverPort, UIPopupManager popupMgr, string popupName)
        {
            if (relay == null || string.IsNullOrEmpty(relay.Address))
            {
                yield break;
            }

            int apiPort = RelayRouterHelpers.RelayApiPort;
            string apiPath = RelayRouterHelpers.RelayRegisterPath ?? "/register";
            string body = "{\"target_ip\":\"" + serverIp + "\",\"target_port\":" + serverPort + "}";
            string requestHost = !string.IsNullOrEmpty(relay.Domain) ? relay.Domain : relay.Address;
            string httpsUrl = string.Format("https://{0}:{1}{2}", requestHost, apiPort, apiPath);

            Debug.Log(string.Format("[RelaySelectionUI] Registering relay via HTTPS request to: {0}", httpsUrl));
            Debug.Log(string.Format("[RelaySelectionUI] Register request body: {0}", body));

            using (var request = new UnityWebRequest(httpsUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("X-Relay-Token", RelayRouterHelpers.RelayApiToken ?? "changeme");
                request.timeout = 10;

                yield return request.SendWebRequest();

                bool error = request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError;
                string responseText = request.downloadHandler != null ? request.downloadHandler.text : null;
                if (error)
                {
                    Debug.Log(string.Format("[RelaySelectionUI] Relay register failed ({0}) status={1} error={2} response={3}", relay.Address, request.responseCode, request.error, responseText ?? ""));
                    yield break;
                }

                if (string.IsNullOrEmpty(responseText))
                {
                    Debug.Log(string.Format("[RelaySelectionUI] Empty register response from {0}", relay.Address));
                    yield break;
                }

                RegisterResponse response = null;
                try
                {
                    response = JsonUtility.FromJson<RegisterResponse>(responseText);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                if (response == null || response.external_port <= 0)
                {
                    Debug.Log(string.Format("[RelaySelectionUI] Register response missing external_port: {0}", responseText));
                    yield break;
                }

                RelayRouterHelpers.SetSelectedRelay(relay.Address, (ushort)response.external_port, relay.Name ?? relay.Address);
                popupMgr.HidePopup(popupName);
                Debug.Log(string.Format("[RelaySelectionUI] Relay registered and selected: {0} -> external:{1}", relay.Address, response.external_port));
            }
        }
    }

    [HarmonyPatch(typeof(ConnectionManagerController), "Event_OnServerBrowserClickEndPoint")]
    static class ConnectionManagerController_Event_OnServerBrowserClickEndPoint_Patch
    {
        static bool Prefix(Dictionary<string, object> message)
        {
            try
            {
                if (message == null || !message.ContainsKey("endPoint"))
                {
                    return true;
                }

                var endPoint = message["endPoint"] as EndPoint;
                if (endPoint == null)
                {
                    return true;
                }

                var entry = RelayRouterHelpers.FindServerEntry(endPoint.ipAddress, endPoint.port);
                if (entry == null)
                {
                    return true;
                }

                Debug.Log(string.Format("[RelaySelectionUI] Clicked registered original server {0}:{1}", endPoint.ipAddress, endPoint.port));
                RelaySelectionUI.ShowRelaySelectionForServer(endPoint.ipAddress, endPoint.port, null);
                return false;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return true;
            }
        }
    }
}
