using HarmonyLib;
using Mirror;

namespace Marioalexsan.PerfectGuard.Patches
{
    [HarmonyPatch]
    internal static class EffectSpamPatches
    {
        private static readonly AbuseDetector TeleportDetector = new AbuseDetector(4);
        private static readonly AbuseDetector SparkleDetector = new AbuseDetector(4);
        private static readonly AbuseDetector PoofDetector = new AbuseDetector(4);
        private static readonly AbuseDetector JumpDetector = new AbuseDetector(4);

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

        // --- Teleport Effect ---
        [HarmonyPrefix, HarmonyPatch(typeof(PlayerVisual), nameof(PlayerVisual.Cmd_PlayTeleportEffect))]
        static bool RateLimitTeleportCommand(PlayerVisual __instance) => TeleportDetector.TrackIfServerAndCheckBehaviour(__instance._player);
        [HarmonyPrefix, HarmonyPatch(typeof(PlayerVisual), nameof(PlayerVisual.Rpc_PlayTeleportEffect))]
        static bool CheckServerTeleport(PlayerVisual __instance) => TeleportDetector.CheckBehaviour(__instance._player);

        // --- Vanity Sparkle Effect ---
        [HarmonyPrefix, HarmonyPatch(typeof(PlayerVisual), nameof(PlayerVisual.Cmd_VanitySparkleEffect))]
        static bool RateLimitSparkleCommand(PlayerVisual __instance) => SparkleDetector.TrackIfServerAndCheckBehaviour(__instance._player);
        [HarmonyPrefix, HarmonyPatch(typeof(PlayerVisual), nameof(PlayerVisual.Rpc_VanitySparkleEffect))]
        static bool CheckServerSparkle(PlayerVisual __instance) => SparkleDetector.CheckBehaviour(__instance._player);

        // --- Poof Smoke Effect ---
        [HarmonyPrefix, HarmonyPatch(typeof(PlayerVisual), nameof(PlayerVisual.Cmd_PoofSmokeEffect))]
        static bool RateLimitPoofCommand(PlayerVisual __instance) => PoofDetector.TrackIfServerAndCheckBehaviour(__instance._player);
        [HarmonyPrefix, HarmonyPatch(typeof(PlayerVisual), nameof(PlayerVisual.Rpc_PoofSmokeEffect))]
        static bool CheckServerPoof(PlayerVisual __instance) => PoofDetector.CheckBehaviour(__instance._player);

        // --- Jump Attack Effect ---
        [HarmonyPrefix, HarmonyPatch(typeof(PlayerVisual), nameof(PlayerVisual.Cmd_JumpAttackEffect))]
        static bool RateLimitJumpCommand(PlayerVisual __instance) => JumpDetector.TrackIfServerAndCheckBehaviour(__instance._player);
        [HarmonyPrefix, HarmonyPatch(typeof(PlayerVisual), nameof(PlayerVisual.Rpc_JumpAttackEffect))]
        static bool CheckServerJump(PlayerVisual __instance) => JumpDetector.CheckBehaviour(__instance._player);
    }
}