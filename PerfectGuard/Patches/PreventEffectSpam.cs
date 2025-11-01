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
}