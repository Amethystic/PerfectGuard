using HarmonyLib;

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
            {
                PerfectGuard.Logger.LogWarning($"Player {player.name} is spamming the drop item command! Blocking requests.");
            }
        };
    }

    // The name of the method to patch is found by decompiling the game.
    // It's often a "user code" method that Mirror generates.
    [HarmonyPatch(typeof(PlayerInventory), nameof(PlayerInventory.UserCode_Cmd_DropItem__ItemData__Int32))]
    [HarmonyPrefix]
    static bool RateLimitDropCommand(PlayerInventory __instance)
    {
        // This is a server-side check. We track the command and check the player's behavior.
        // If TrackIfServerAndCheckBehaviour returns false, it means the player is spamming.
        // By returning false ourselves, we cancel the original DropItem command completely.
        return DropCommandDetector.TrackIfServerAndCheckBehaviour(__instance._player);
    }
}