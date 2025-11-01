using HarmonyLib;
using Mirror;

namespace Marioalexsan.PerfectGuard.Patches;

public class IdentityTrack
{
    [HarmonyPatch]
    internal static class IdentityTrackerInjectorPatch
    {
        [HarmonyPostfix, HarmonyPatch(typeof(NetworkIdentity), "Awake")]
        static void InjectTracker(NetworkIdentity __instance)
        {
            if (__instance.GetComponent<IdentityTracker>() == null)
            {
                __instance.gameObject.AddComponent<IdentityTracker>();
            }
        }
        [HarmonyPrefix, HarmonyPatch(typeof(NetworkClient), nameof(NetworkClient.Disconnect))]
        static void ClearOnDisconnect()
        {
            NetworkIdentityManager.AllIdentities.Clear();
        }
    }
}