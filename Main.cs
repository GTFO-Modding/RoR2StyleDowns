using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.CrashReportHandler;
using System.IO;
using System.Text.Json;
using GameEvent;
using System;
using System.Collections.Generic;
using Player;
using Nidhogg.Managers;
using System.Runtime.InteropServices;
using System.Reflection;
using CellMenu;

namespace RoR2StyleDowns
{
    [BepInDependency("com.kasuromi.nidhogg", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(GUID, NAME, VERSION)]
    public class Main : BasePlugin
    {
        public const string
            NAME = "RandomDownMessages",
            AUTHOR = "dak",
            VERSION = "1.0.0",
            GUID = "com." + AUTHOR + "." + NAME;

        public static ManualLogSource log;
        public static List<DownMessage> downMessages;
        public static Random Random = new Random();

        public override void Load()
        {
            CrashReportHandler.SetUserMetadata("Modded", "true");
            log = Log;
            string path = Path.Combine(Paths.ConfigPath, "DownMessages.json");
            downMessages = new List<DownMessage>();
            NetworkingManager.RegisterEvent<ChatMsg>(typeof(ChatMsg).AssemblyQualifiedName, OnMessage);

            //if (File.Exists(path))
            //{
            //    downMessages = JsonSerializer.Deserialize<List<DownMessage>>(File.ReadAllText(path));
            //    if (downMessages == null) downMessages = new List<DownMessage>();
            //} else
            //{
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "RoR2StyleDowns.Messages.txt";

            using Stream stream = assembly.GetManifestResourceStream(resourceName);
            using StreamReader reader = new StreamReader(stream);
            string result = reader.ReadToEnd();
            downMessages = JsonSerializer.Deserialize<List<DownMessage>>(result);
            File.WriteAllText(path, JsonSerializer.Serialize(downMessages));
            //}

            if (downMessages.Count == 0)
            {
                downMessages.Add(new DownMessage() { P1 = "You removed all the down messages", P2 = "{0} removed all the downed messages" });
            }

            var harmony = new Harmony(GUID);
            harmony.PatchAll();
        }

        public void OnMessage(ulong id, ChatMsg incomingMsg)
        {
            log.LogDebug($"{incomingMsg}");
            int index = incomingMsg.MessageIndex;
            string p2 = "{0} isn't using the same message set as you";
            if (downMessages.Count - 1 >= index)
                p2 = downMessages[index].P2;

            SendMessage(string.Format(p2, incomingMsg.Player), eGameEventChatLogType.IncomingChat);
        }

        private void SendMessage(string message, eGameEventChatLogType chatLogType)
        {
            GuiManager.PlayerLayer.m_gameEventLog.AddLogItem(message, chatLogType);
            CM_PageLoadout.Current.m_gameEventLog.AddLogItem(message, chatLogType);
            CM_PageMap.Current.m_gameEventLog.AddLogItem(message, chatLogType);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ChatMsg
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 200)]
        public string Player;
        public int MessageIndex;
        public override string ToString()
        {
            return $"Player: {Player}     MessageIndex: {MessageIndex}";
        }
    }

    public struct DownMessage
    {
        public string P1 { get; set; }
        public string P2 { get; set; }
        public override string ToString()
        {
            return $"P1: {P1}\nP2: {P2}";
        }
    }

    [HarmonyPatch(typeof(GameEventManager), "PostEvent", new Type[] { typeof(eGameEvent), typeof(PlayerAgent), typeof(float), typeof(string), typeof(Il2CppSystem.Collections.Generic.Dictionary<string, string>) })]
    public static class GEM
    {
        public static bool CallingPostEvent = false;
        public static PlayerAgent Agent = null;
        public static eGameEvent GameEvent;
        public static bool MessageSent = false;
        public static void Prefix(eGameEvent e, PlayerAgent player)
        {
            CallingPostEvent = true;
            GameEvent = e;
            Agent = player;
        }

        public static void Postfix()
        {
            CallingPostEvent = false;
            GameEvent = 0;
            Agent = null;
            MessageSent = false;
        }
    }

    [HarmonyPatch(typeof(PUI_GameEventLog), "AddLogItem")]
    public static class Patch
    {
        public static bool Prefix(PUI_GameEventLog __instance, ref string log, eGameEventChatLogType type)
        {
            if (type == eGameEventChatLogType.IncomingChat) return true;
            if (GEM.CallingPostEvent)
            {
                if (GEM.GameEvent == eGameEvent.player_downed)
                {
                    string playerName = string.Concat(new string[]
                    {
                    "<color=#",
                    ColorExt.ToHex(GEM.Agent.Owner.PlayerColor),
                    ">",
                    GEM.Agent.Owner.NickName,
                    "</color>"
                    });
                    if (!GEM.Agent.IsLocallyOwned) return false;


                    Main.log.LogMessage($"Player name: {playerName}");

                    int next = Main.Random.Next(0, Main.downMessages.Count - 1);
                    DownMessage dmsg = Main.downMessages[next];

                    if (GEM.MessageSent == false)
                    {
                        NetworkingManager.InvokeEvent(typeof(ChatMsg).AssemblyQualifiedName, new ChatMsg() { MessageIndex = next, Player = playerName });
                        GEM.MessageSent = true;
                    }

                    Main.log.LogMessage(dmsg);
                    log = dmsg.P1;
                }
            }
            return true;
        }
    }
}
