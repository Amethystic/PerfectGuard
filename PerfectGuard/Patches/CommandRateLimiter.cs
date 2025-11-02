using HarmonyLib;
using Mirror;

namespace Marioalexsan.PerfectGuard.Patches;

[HarmonyPatch]
internal static class CommandRateLimiter
{
    // Allow a maximum of 5 drop commands per second. This is generous for a normal player
    // but far too slow for an exploiter to cause a crash.
    private static readonly AbuseDetector DropCommandDetector = new AbuseDetector(5);
    static CommandRateLimiter()
    {
        DropCommandDetector.OnConfirmRaised += (sender, player) =>
        {
            if (PerfectGuard.EnableDetailedLogging.Value)
                PerfectGuard.Logger.LogWarning($"Player {player.name} is spamming the drop item command! Blocking requests.");
        };
    }
    [HarmonyPatch(typeof(PlayerInventory), nameof(PlayerInventory.UserCode_Cmd_DropItem__ItemData__Int32))]
    [HarmonyPrefix]
    static bool RateLimitDropCommand(PlayerInventory __instance)
    {
        if (!NetworkServer.active) return true;
        return DropCommandDetector.TrackIfServerAndCheckBehaviour(__instance._player);
    }
}