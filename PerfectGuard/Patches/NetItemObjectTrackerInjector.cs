using HarmonyLib;

namespace Marioalexsan.PerfectGuard.Patches;

[HarmonyPatch]
public class NetItemObjectTrackerInjector
{
    [HarmonyPatch(typeof(Net_ItemObject), nameof(Net_ItemObject.Awake))]
    [HarmonyPostfix]
    static void InjectTracker(Net_ItemObject __instance)
    {
        if (__instance.GetComponent<NetItemObjectTracker>() == null)
            __instance.gameObject.AddComponent<NetItemObjectTracker>();
    }
}