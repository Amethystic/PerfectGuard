using HarmonyLib;
using Marioalexsan.PerfectGuard;
using Mirror;

[HarmonyPatch]
static class PreventSparkleSpam
{
    private static readonly AbuseDetector Detector = new AbuseDetector(4);

    static PreventSparkleSpam()
    {
        Detector.OnSuspicionRaised += OnSuspicionRaised;
        Detector.OnConfirmRaised += OnConfirmRaised;
    }

    private static void OnSuspicionRaised(object sender, Player player)
    {
        PerfectGuard.Logger.LogWarning($"Player {player.name} is suspicious: using too many vanity sparkle effects.");
    }

    private static void OnConfirmRaised(object sender, Player player)
    {
        PerfectGuard.Logger.LogWarning($"Player {player.name} is malicious! Trying to crash the server using vanity sparkle effects.");
    }

    [HarmonyPatch(typeof(PlayerVisual), nameof(PlayerVisual.UserCode_Rpc_VanitySparkleEffect))]
    [HarmonyPrefix]
    static bool CheckClientTeleporterEffect(PlayerVisual __instance) => Detector.TrackIfClientAndCheckBehaviour(__instance._player);

    [HarmonyPatch(typeof(PlayerVisual), nameof(PlayerVisual.Rpc_VanitySparkleEffect))]
    [HarmonyPrefix]
    static bool CheckServerTeleporterEffect(PlayerVisual __instance) => Detector.TrackIfServerAndCheckBehaviour(__instance._player);
}