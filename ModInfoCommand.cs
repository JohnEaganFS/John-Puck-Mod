using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using HarmonyLib;
using Unity.Netcode;

namespace OfficialPuckMod
{
    // Harmony postfix on UIChatController.Event_Server_OnChatCommand to intercept server chat commands
    [HarmonyPatch(typeof(UIChatController), "Event_Server_OnChatCommand")]
    static class ModInfo_UIChatController_ChatCmd_Patch
    {
        static void Postfix(UIChatController __instance, Dictionary<string, object> message)
        {
            try
            {
                if (message == null) return;
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

                if (!message.ContainsKey("clientId") || !message.ContainsKey("command")) return;
                ulong clientId = (ulong)message["clientId"];
                string command = message["command"] as string;
                string[] args = message.ContainsKey("args") ? message["args"] as string[] : new string[0];

                // follow LevelManagerController pattern: locate player and optionally perform checks
                var pm = NetworkBehaviourSingleton<PlayerManager>.Instance;
                var player = pm != null ? pm.GetPlayerByClientId(clientId) : null;
                Debug.Log($"[ModInfoPatch] Received chat command '{command}' from client {clientId} (player={(player?player.Username.Value:"<none>")}).");

                if (command != "/info" && command != "/modinfo") return;

                string info = "John's Official Puck Mod. Changes include:\n" +
                              "Puck Size: 0.92\n" +
                              "Stick Speed: 1200\n" +
                              "Puck-Body Collision: Disabled\n" +
                              "Blade on Blade Only\n" +
                              "PHL Turning Values\n" +
                              "Faceoff Puck Spawns on Ice\n";

                // Try to get the private 'uiChat' field from the UIChatController instance via reflection
                UIChat uiChat = null;
                var fi = typeof(UIChatController).GetField("uiChat", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fi != null)
                {
                    uiChat = fi.GetValue(__instance) as UIChat;
                }

                if (uiChat != null)
                {
                    uiChat.Server_SendSystemChatMessage(info, clientId);
                }
                else
                {
                    // Fallback to UIManager.Chat if available
                    var ui = MonoBehaviourSingleton<UIManager>.Instance;
                    if (ui != null && ui.Chat != null)
                    {
                        ui.Chat.Server_SendSystemChatMessage(info, clientId);
                    }
                    else
                    {
                        Debug.Log("[ModInfoPatch] UIChat not available to send info.");
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
