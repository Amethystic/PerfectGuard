using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Marioalexsan.PerfectGuard;

[BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
public class PerfectGuard : BaseUnityPlugin
{
    internal static new ManualLogSource Logger = null!;

    private readonly Harmony _harmony = new Harmony($"{ModInfo.GUID}");

    private readonly ConfigEntry<KeyCode> _windowKey;

    private TimeSpan _abuseDetectorCleanupAccumulator;

    public PerfectGuard()
    {
        Logger = base.Logger;
        _windowKey = Config.Bind("General", "WindowKey", KeyCode.F9, "Key to trigger PerfectGuard's configuration window with.");
    }

    public void Awake()
    {
        _harmony.PatchAll();
        UnityEngine.Debug.Log($"${Application.version} loaded.");
    }

    public void Update()
    {
        if (Input.GetKeyDown(_windowKey.Value))
            _windowShown = !_windowShown;

        _abuseDetectorCleanupAccumulator += TimeSpan.FromSeconds(Time.deltaTime);

        if (_abuseDetectorCleanupAccumulator >= TimeSpan.FromSeconds(60))
        {
            _abuseDetectorCleanupAccumulator -= TimeSpan.FromSeconds(60);
            AbuseDetector.RunActorCleanup();
        }
    }

    public void OnGUI()
    {
        if (!_windowShown)
            return;

        _windowRect = GUILayout.Window(GUIUtility.GetControlID(FocusType.Passive), new Rect(Screen.width * 0.1f, Screen.height * 0.1f, Screen.width * 0.5f, Screen.height * 0.5f), DrawWindow, "PerfectGuard");
    }

    private void DrawWindow(int windowID)
    {

    }

    private Rect _windowRect;
    private bool _windowShown;
}