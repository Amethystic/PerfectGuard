using HarmonyLib;
using Marioalexsan.PerfectGuard;

[HarmonyPatch]
static class PreventTeleportSpamRpc
{
    static readonly Dictionary<Player, DateTime> TimeSinceLastEffect = [];
    static readonly Dictionary<Player, int> Warns = [];

    static bool IsAbusingEffect(Player player)
    {
        return TimeSinceLastEffect.TryGetValue(player, out DateTime lastTrigger) && lastTrigger + TimeSpan.FromSeconds(1) >= DateTime.Now;
    }

    [HarmonyPatch(typeof(PlayerVisual), nameof(PlayerVisual.Init_TeleportEffect))]
    [HarmonyPrefix]
    static bool CheckLocalTeleporterEffect(PlayerVisual __instance) => !IsAbusingEffect(__instance._player);

    [HarmonyPatch(typeof(PlayerVisual), nameof(PlayerVisual.Rpc_PlayTeleportEffect))]
    [HarmonyPrefix]
    static bool CheckRpcTeleporterEffect(PlayerVisual __instance)
    {
        foreach (var item in TimeSinceLastEffect.Keys.ToList())
        {
            if (!item)
            {
                TimeSinceLastEffect.Remove(item);
                Warns.Remove(item);
            }
        }

        if (IsAbusingEffect(__instance._player))
        {
            if (Warns[__instance._player] == 0)
                PerfectGuard.Logger.LogWarning($"Player {__instance._player.name} is potentially trying to crash the server using portal spam.");

            Warns[__instance._player]++;
        }
        else
        {
            TimeSinceLastEffect[__instance._player] = DateTime.Now;
            Warns[__instance._player] = 0;
        }

        return Warns[__instance._player] == 0;
    }
}