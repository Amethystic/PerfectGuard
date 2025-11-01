using HarmonyLib;

namespace Marioalexsan.PerfectGuard.Patches;

[HarmonyPatch]
public class NetItemObjectTrackerInjector
{
    [HarmonyPatch(typeof(Net_ItemObject), nameof(Net_ItemObject.Awake))]
    [HarmonyPostfix] // Run this patch *after* the original Awake method has finished.
    static void InjectTracker(Net_ItemObject __instance)
    {
        // Check if the component already exists to prevent duplicates.
        if (__instance.GetComponent<NetItemObjectTracker>() == null)
        {
            // Add our tracker component.
            __instance.gameObject.AddComponent<NetItemObjectTracker>();
        }
    }
}