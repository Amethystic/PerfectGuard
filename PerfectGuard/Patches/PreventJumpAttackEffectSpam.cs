using HarmonyLib;
using Marioalexsan.PerfectGuard;
using Mirror;
using System.Reflection;

[HarmonyPatch]
static class PreventJumpAttackEffectSpam
{
    private static readonly AbuseDetector Detector = new AbuseDetector(4);

    static PreventJumpAttackEffectSpam()
    {
        Detector.OnSuspicionRaised += OnSuspicionRaised;
        Detector.OnConfirmRaised += OnConfirmRaised;
    }

    private static void OnSuspicionRaised(object sender, Player player)
    {
        PerfectGuard.Logger.LogWarning($"Player {player.name} is suspicious: using too many jump attack particles.");
    }

    private static void OnConfirmRaised(object sender, Player player)
    {
        PerfectGuard.Logger.LogWarning($"Player {player.name} is malicious! Trying to crash the server using jump attack particles.");
    }

    [HarmonyPatch(typeof(PlayerVisual), nameof(PlayerVisual.UserCode_Rpc_JumpAttackEffect))]
    [HarmonyPrefix]
    static bool CheckClientTeleporterEffect(PlayerVisual __instance) => Detector.TrackIfClientAndCheckBehaviour(__instance._player);

    [HarmonyPatch(typeof(PlayerVisual), nameof(PlayerVisual.Rpc_JumpAttackEffect))]
    [HarmonyPrefix]
    static bool CheckServerTeleporterEffect(PlayerVisual __instance) => Detector.TrackIfServerAndCheckBehaviour(__instance._player);
}