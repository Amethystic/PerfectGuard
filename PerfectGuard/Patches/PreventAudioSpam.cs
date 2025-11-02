using HarmonyLib;
using System;
using UnityEngine;

namespace Marioalexsan.PerfectGuard.Patches
{
    [HarmonyPatch]
    internal static class AudioSpamPatches
    {
        [HarmonyPrefix, HarmonyPatch(typeof(AudioSource), nameof(AudioSource.Play), new Type[0])]
        public static bool AudioSource_Play_Prefix(AudioSource __instance) => PerfectGuard.CheckAudioCooldown(__instance);
        [HarmonyPrefix, HarmonyPatch(typeof(AudioSource), nameof(AudioSource.PlayOneShot), new[] { typeof(AudioClip), typeof(float) })]
        public static bool AudioSource_PlayOneShot_Prefix(AudioSource __instance) => PerfectGuard.CheckAudioCooldown(__instance);
    }
}