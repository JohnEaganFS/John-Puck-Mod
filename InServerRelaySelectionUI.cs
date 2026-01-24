using System;
using UnityEngine;
using HarmonyLib;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.UIElements;
using System.Reflection;

namespace JohnRelayMod
{
    // In-server relay selection UI: minimal init/shutdown hooks.
    public static class InServerRelaySelectionUI
    {
        // GameObject used for runtime checks/injection fallback
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

        // Show the relay selection popup for the currently connected/last-connected server
        public static void ShowRelaySelectionForCurrentServer()
        {
            try
            {
                var connMgr = ConnectionManager.Instance;
                if (connMgr == null)
                {
                    Debug.Log("[InServerRelaySelectionUI] ConnectionManager not available.");
                    return;
                }

                // Always prefer the tracked client last target for determining the original server
                string targetIp = RelayRouterHelpers.ClientLastTargetAddress;
                ushort targetPort = RelayRouterHelpers.ClientLastTargetPort;
                if (string.IsNullOrEmpty(targetIp))
                {
                    Debug.Log("[InServerRelaySelectionUI] ClientLastTarget not set; cannot determine original server.");
                    return;
                }

                // Delegate to the shared RelaySelectionUI popup helper
                RelaySelectionUI.ShowRelaySelectionForServer(targetIp, targetPort);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    // Patch server-side chat commands and log when a client issues /relay
    [HarmonyPatch(typeof(UIChatController), "Event_Server_OnChatCommand")]
    static class InServerRelay_UIChatController_ChatCmd_Patch
    {
        static void Postfix(UIChatController __instance, Dictionary<string, object> message)
        {
            try
            {
                if (message == null) return;
                if (!message.ContainsKey("command")) return;
                var command = message["command"] as string;
                if (string.Equals(command, "/relay", StringComparison.OrdinalIgnoreCase))
                {
                    ulong clientId = 0UL;
                    if (message.ContainsKey("clientId"))
                    {
                        clientId = (ulong)message["clientId"];
                    }
                    Debug.Log(string.Format("[InServerRelaySelectionUI] Received /relay from client {0}", clientId));
                    InServerRelaySelectionUI.ShowRelaySelectionForCurrentServer();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    // Intercept the client-side send method so typing /relay doesn't send to server
    [HarmonyPatch(typeof(UIChat), "Client_SendClientChatMessage")]
    static class UIChat_Client_SendClientChatMessage_Patch
    {
        static bool Prefix(UIChat __instance, ref string message, bool useTeamChat)
        {
            try
            {
                if (string.IsNullOrEmpty(message)) return true;
                if (string.Equals(message.Trim(), "/relay", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log("[InServerRelaySelectionUI] Intercepted /relay locally — opening relay UI and preventing send.");
                    InServerRelaySelectionUI.ShowRelaySelectionForCurrentServer();
                    return false; // prevent original method from sending the chat
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            return true;
        }
    }

    // Inject a Relay Selection button into the pause menu using the Initialize's rootVisualElement
    [HarmonyPatch(typeof(UIPauseMenu), "Initialize")]
    static class UIPauseMenu_Initialize_Patch
    {
        static void Postfix(UIPauseMenu __instance, VisualElement rootVisualElement)
        {
            try
            {
                if (rootVisualElement == null) return;
                var container = rootVisualElement.Q("PauseMenuContainer");
                if (container == null) return;

                // avoid duplicate by a fixed element name
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

                // add the relay button to the pause menu
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