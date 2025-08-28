using HarmonyLib;
using Marioalexsan.PerfectGuard;
using Mirror;

[HarmonyPatch]
static class PreventPoofSmokeSpam
{
    private static readonly AbuseDetector Detector = new AbuseDetector(4);

    static PreventPoofSmokeSpam()
    {
        Detector.OnSuspicionRaised += OnSuspicionRaised;
        Detector.OnConfirmRaised += OnConfirmRaised;
    }

    private static void OnSuspicionRaised(object sender, Player player)
    {
        PerfectGuard.Logger.LogWarning($"Player {player.name} is suspicious: using too many poof smoke particles.");
    }

    private static void OnConfirmRaised(object sender, Player player)
    {
        PerfectGuard.Logger.LogWarning($"Player {player.name} is malicious! Trying to crash the server using poof smoke spam.");
    }

    [HarmonyPatch(typeof(PlayerVisual), nameof(PlayerVisual.UserCode_Rpc_PoofSmokeEffect))]
    [HarmonyPrefix]
    static bool CheckClientTeleporterEffect(PlayerVisual __instance) => Detector.TrackIfClientAndCheckBehaviour(__instance._player);

    [HarmonyPatch(typeof(PlayerVisual), nameof(PlayerVisual.Rpc_PoofSmokeEffect))]
    [HarmonyPrefix]
    static bool CheckServerTeleporterEffect(PlayerVisual __instance) => Detector.TrackIfServerAndCheckBehaviour(__instance._player);
}