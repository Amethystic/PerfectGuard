using HarmonyLib;
using Mirror;
using System;
using System.Collections.Generic;

namespace Marioalexsan.PerfectGuard.Patches
{
    [HarmonyPatch]
    internal static class EffectSpamPatches
    {
        private static readonly AbuseDetector TeleportDetector = new(4);
        private static readonly AbuseDetector SparkleDetector = new(4);
        private static readonly AbuseDetector PoofDetector = new(4);
        private static readonly AbuseDetector JumpDetector = new(4);

        static EffectSpamPatches()
        {
            TeleportDetector.OnConfirmRaised += (s, p) => LogConfirmation(p, "teleport effects");
            SparkleDetector.OnConfirmRaised += (s, p) => LogConfirmation(p, "vanity sparkle effects");
            PoofDetector.OnConfirmRaised += (s, p) => LogConfirmation(p, "poof smoke effects");
            JumpDetector.OnConfirmRaised += (s, p) => LogConfirmation(p, "jump attack effects");
        }

        private static void LogConfirmation(Player player, string effectName)
        {
            if (PerfectGuard.EnableDetailedLogging.Value)
                PerfectGuard.Logger.LogWarning($"Player {player.name} is malicious! Trying to crash the server using {effectName}.");
        }

        // --- Teleport ---
        [HarmonyPrefix, HarmonyPatch(typeof(PlayerVisual), nameof(PlayerVisual.Rpc_PlayTeleportEffect))]
        static bool CheckServerTeleport(PlayerVisual __instance) => TeleportDetector.TrackIfServerAndCheckBehaviour(__instance._player);

        // --- Sparkle ---
        [HarmonyPrefix, HarmonyPatch(typeof(PlayerVisual), nameof(PlayerVisual.Rpc_VanitySparkleEffect))]
        static bool CheckServerSparkle(PlayerVisual __instance) => SparkleDetector.TrackIfServerAndCheckBehaviour(__instance._player);

        // --- Poof ---
        [HarmonyPrefix, HarmonyPatch(typeof(PlayerVisual), nameof(PlayerVisual.Rpc_PoofSmokeEffect))]
        static bool CheckServerPoof(PlayerVisual __instance) => PoofDetector.TrackIfServerAndCheckBehaviour(__instance._player);

        // --- Jump Attack ---
        [HarmonyPrefix, HarmonyPatch(typeof(PlayerVisual), nameof(PlayerVisual.Rpc_JumpAttackEffect))]
        static bool CheckServerJump(PlayerVisual __instance) => JumpDetector.TrackIfServerAndCheckBehaviour(__instance._player);
    }
    
    [HarmonyPatch(typeof(HostConsole), nameof(HostConsole.Send_ServerMessage))]
    internal static class PatchHostCommands
    {
        // This transpiler ensures the host can still use commands
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ExecuteCommandsFromHostConsole(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions)
                .MatchForward(false, new CodeMatch(ins => ins.Calls(AccessTools.Method(typeof(HostConsole), nameof(HostConsole.Init_ServerMessage)))))
                .SetInstruction(new CodeInstruction(System.Reflection.Emit.OpCodes.Call, AccessTools.Method(typeof(PatchHostCommands), nameof(ProcessHostConsoleMessage))))
                .InstructionEnumeration();
        }

        internal static void ProcessHostConsoleMessage(HostConsole instance, string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            if (message.Trim().StartsWith('/'))
            {
                var parts = message.Trim().Substring(1).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var command = parts.Length >= 1 ? parts[0] : "";
                var argument = parts.Length >= 2 ? parts[1] : "";
                instance._cmdManager.Init_ConsoleCommand(command, argument);
            }
            else
            {
                // This is a normal message from the host, send it
                ServerMessage serverMessage = default;
                serverMessage.servMsg = message;
                NetworkServer.SendToAll(serverMessage);
                instance.New_LogMessage(message);
            }
        }
    }
}