using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace JohnRelayMod
{
    public static class InServerRelaySelectionUI
    {
        static GameObject _runnerObject;

        public static void Init()
        {
            try
            {
                Debug.Log("[InServerRelaySelectionUI] Initialized.");
                if (_runnerObject == null)
                {
                    _runnerObject = new GameObject("InServerRelaySelectionRunner");
                    UnityEngine.Object.DontDestroyOnLoad(_runnerObject);
                    _runnerObject.AddComponent<InServerRelaySelectionRunner>();
                }
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
                Debug.Log("[InServerRelaySelectionUI] Shutdown.");
                if (_runnerObject != null)
                {
                    UnityEngine.Object.Destroy(_runnerObject);
                    _runnerObject = null;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public static void ShowRelaySelectionForCurrentServer()
        {
            try
            {
                if (ConnectionManager.Instance == null)
                {
                    Debug.Log("[InServerRelaySelectionUI] ConnectionManager not available.");
                    return;
                }

                string targetIp = RelayRouterHelpers.ClientLastTargetAddress;
                ushort targetPort = RelayRouterHelpers.ClientLastTargetPort;
                if (string.IsNullOrEmpty(targetIp))
                {
                    Debug.Log("[InServerRelaySelectionUI] ClientLastTarget not set; cannot determine original server.");
                    return;
                }

                RelaySelectionUI.ShowRelaySelectionForServer(targetIp, targetPort, null);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    [HarmonyPatch(typeof(ChatManagerController), "Event_Server_OnChatCommand")]
    static class InServerRelay_ChatManagerController_ChatCmd_Patch
    {
        static void Postfix(ChatManagerController __instance, Dictionary<string, object> message)
        {
            try
            {
                if (message == null || !message.ContainsKey("command")) return;
                var command = message["command"] as string;
                if (!string.Equals(command, "/relay", StringComparison.OrdinalIgnoreCase)) return;

                ulong clientId = 0UL;
                if (message.ContainsKey("clientId"))
                {
                    clientId = (ulong)message["clientId"];
                }

                Debug.Log(string.Format("[InServerRelaySelectionUI] Received /relay from client {0}", clientId));
                InServerRelaySelectionUI.ShowRelaySelectionForCurrentServer();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    [HarmonyPatch(typeof(ChatManagerController), "Event_OnChatSubmitMessage")]
    static class ChatManagerController_Event_OnChatSubmitMessage_Patch
    {
        static bool Prefix(ChatManagerController __instance, Dictionary<string, object> message)
        {
            try
            {
                if (message == null || !message.ContainsKey("content")) return true;
                var content = message["content"] as string;
                if (string.IsNullOrEmpty(content)) return true;
                if (!string.Equals(content.Trim(), "/relay", StringComparison.OrdinalIgnoreCase)) return true;

                Debug.Log("[InServerRelaySelectionUI] Intercepted /relay locally; opening relay UI and preventing send.");
                InServerRelaySelectionUI.ShowRelaySelectionForCurrentServer();
                return false;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(UIPauseMenu), "Initialize")]
    static class UIPauseMenu_Initialize_Patch
    {
        static void Postfix(UIPauseMenu __instance, VisualElement rootVisualElement)
        {
            try
            {
                if (rootVisualElement == null) return;
                var container = rootVisualElement.Q("PauseMenu");
                if (container == null) return;

                var existing = container.Q<Button>("RelaySelectionButton");
                if (existing != null) return;

                var relayBtn = new Button(() => { InServerRelaySelectionUI.ShowRelaySelectionForCurrentServer(); }) { text = "Relay Selection" };
                relayBtn.name = "RelaySelectionButton";
                relayBtn.style.marginTop = 4;
                relayBtn.style.marginBottom = 4;
                relayBtn.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
                relayBtn.style.color = new StyleColor(Color.white);
                relayBtn.style.borderTopWidth = 1;
                relayBtn.style.borderBottomWidth = 1;
                relayBtn.style.borderLeftWidth = 1;
                relayBtn.style.borderRightWidth = 1;
                relayBtn.style.borderTopColor = new StyleColor(new Color(0.35f, 0.35f, 0.35f));
                relayBtn.style.borderBottomColor = new StyleColor(new Color(0.35f, 0.35f, 0.35f));
                relayBtn.style.borderLeftColor = new StyleColor(new Color(0.35f, 0.35f, 0.35f));
                relayBtn.style.borderRightColor = new StyleColor(new Color(0.35f, 0.35f, 0.35f));
                relayBtn.style.paddingLeft = 6;
                relayBtn.style.paddingRight = 6;
                relayBtn.style.paddingTop = 2;
                relayBtn.style.paddingBottom = 2;

                container.Add(relayBtn);
                Debug.Log("[InServerRelaySelectionUI] Injected Relay Selection button into pause menu via Initialize postfix.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
