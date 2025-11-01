using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Marioalexsan.PerfectGuard
{
    [BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
    public class PerfectGuard : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger = null!;
        internal static ManualLogSource Log = null!;
        private readonly Harmony _harmony = new(ModInfo.GUID);

        #region Configuration
        private ConfigEntry<KeyCode> _windowKey;
        public static ConfigEntry<bool> EnableMasterSwitch { get; private set; }
        public static ConfigEntry<bool> EnableGlobalRpcShield { get; private set; }
        public static ConfigEntry<bool> EnableObjectSpikeProtection { get; private set; }
        public static ConfigEntry<bool> EnableAudioSpamProtection { get; private set; }
        public static ConfigEntry<bool> EnableServerHealthCheck { get; private set; }
        public static ConfigEntry<bool> EnableDetailedLogging { get; private set; }
        #endregion

        #region GUI
        private Rect _windowRect;
        private bool _windowShown;
        #endregion

        #region AbuseDetector Management
        private TimeSpan _abuseDetectorCleanupAccumulator;
        #endregion

        #region Audio Spam Fields
        private const float AudioSpamCooldownSeconds = 0.1f;
        private static readonly Dictionary<AudioSource, float> AudioCooldowns = new();
        private static readonly List<AudioSource> DeadAudioSources = new();
        #endregion

        #region Global RPC Shield Fields
        private static bool _isShieldActive;
        private static Coroutine _shieldResetCoroutine;
        private const int RpcSpamThreshold = 40; // Max RPC calls of one type per reset interval
        private const float ShieldResetInterval = 0.25f;
        private static readonly Dictionary<ushort, int> RpcMessageCounts = new();
        private static readonly Dictionary<ushort, NetworkMessageDelegate> OriginalHandlers = new();
        // Whitelist for high-frequency, safe RPCs if needed. Use function hash.
        private static readonly HashSet<ushort> RpcWhitelist = new() { 63450, 15647, 28859 };
        #endregion

        #region Object Spike & Health Check Fields
        private Coroutine _periodicChecksCoroutine;
        private const int NetworkObjectSpikeThreshold = 250;
        private static int _lastNetworkObjectCount;
        private static bool _isPanicModeActive;
        
        private const float StuckThresholdSeconds = 25f;
        private const float HighLatencyThreshold = 300f;
        private static float _previousLatency;
        private static float _stuckDuration;
        private static bool _serverStuckReportSent;
        public static string DeadServerStatus { get; private set; } = "N/A";
        #endregion

        public PerfectGuard()
        {
            Log = Logger;

            // --- Configuration Setup ---
            _windowKey = Config.Bind("1. General", "WindowKey", KeyCode.F9, "Key to open the configuration window.");
            EnableMasterSwitch = Config.Bind("1. General", "MasterSwitch", true, "Enable or disable all protections globally.");
            EnableDetailedLogging = Config.Bind("1. General", "DetailedLogging", true, "Log detailed warnings for detected malicious activities.");

            EnableGlobalRpcShield = Config.Bind("2. Protections", "GlobalRpcShield", true, "Enables a low-level network shield to block all types of excessive RPC spam.");
            EnableObjectSpikeProtection = Config.Bind("2. Protections", "ObjectSpikeProtection", true, "Automatically detects and cleans up mass item drop spam.");
            EnableAudioSpamProtection = Config.Bind("2. Protections", "AudioSpamProtection", true, "Prevents crashes from excessive audio source plays.");
            EnableServerHealthCheck = Config.Bind("2. Protections", "ServerHealthCheck", true, "Monitors server latency to detect high lag or server freezes.");
        }

        public void Awake()
        {
            _harmony.PatchAll();
            Log.LogMessage($"{ModInfo.NAME} v{ModInfo.VERSION} has loaded!");

            EnableMasterSwitch.SettingChanged += (s, e) => ToggleSystems();
            ToggleSystems();
        }

        private void ToggleSystems()
        {
            if (EnableMasterSwitch.Value)
            {
                Log.LogMessage("PerfectGuard is now ACTIVE.");
                if (_periodicChecksCoroutine == null)
                    _periodicChecksCoroutine = StartCoroutine(PeriodicChecksCoroutine());
                
                if (EnableGlobalRpcShield.Value) InitializeShield();
            }
            else
            {
                Log.LogWarning("PerfectGuard is now INACTIVE.");
                if (_periodicChecksCoroutine != null)
                {
                    StopCoroutine(_periodicChecksCoroutine);
                    _periodicChecksCoroutine = null;
                }
                
                ShutdownShield();
            }
        }

        public void Update()
        {
            if (Input.GetKeyDown(_windowKey.Value))
                _windowShown = !_windowShown;

            // Cleanup for AbuseDetector class
            _abuseDetectorCleanupAccumulator += TimeSpan.FromSeconds(Time.deltaTime);
            if (_abuseDetectorCleanupAccumulator >= TimeSpan.FromSeconds(60))
            {
                _abuseDetectorCleanupAccumulator = TimeSpan.Zero;
                AbuseDetector.RunActorCleanup();
            }
        }

        public void OnGUI()
        {
            if (!_windowShown) return;
            _windowRect = GUILayout.Window(GUIUtility.GetControlID(FocusType.Passive), 
                new Rect(Screen.width * 0.1f, Screen.height * 0.1f, Screen.width * 0.3f, Screen.height * 0.5f), 
                DrawWindow, $"{ModInfo.NAME} v{ModInfo.VERSION}");
        }

        private void DrawWindow(int windowID)
        {
            // Simple GUI to toggle settings at runtime
            EnableMasterSwitch.Value = GUILayout.Toggle(EnableMasterSwitch.Value, "Enable All Protections (Master Switch)");
            GUILayout.Space(10);
            EnableGlobalRpcShield.Value = GUILayout.Toggle(EnableGlobalRpcShield.Value, "Global RPC Spam Shield");
            EnableObjectSpikeProtection.Value = GUILayout.Toggle(EnableObjectSpikeProtection.Value, "Item Drop Spike Protection");
            EnableAudioSpamProtection.Value = GUILayout.Toggle(EnableAudioSpamProtection.Value, "Audio Spam Protection");
            EnableServerHealthCheck.Value = GUILayout.Toggle(EnableServerHealthCheck.Value, "Server Health/Lag Check");
            GUILayout.Space(10);
            EnableDetailedLogging.Value = GUILayout.Toggle(EnableDetailedLogging.Value, "Enable Detailed Logging");
            
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Server Status: {DeadServerStatus}");
            GUI.DragWindow();
        }

        #region Core Periodic Coroutine
        private IEnumerator PeriodicChecksCoroutine()
        {
            yield return new WaitForSeconds(3.0f); // Initial delay
            Log.LogInfo("Periodic checks started.");

            if (NetworkClient.isConnected)
                _lastNetworkObjectCount = CountNetworkObjects();

            while (true)
            {
                if (!EnableMasterSwitch.Value)
                {
                    yield return new WaitForSeconds(5.0f); // Sleep longer when disabled
                    continue;
                }

                if (EnableObjectSpikeProtection.Value && !_isPanicModeActive && NetworkClient.isConnected)
                {
                    CheckForObjectSpikes();
                }

                if (EnableServerHealthCheck.Value && NetworkClient.isConnected)
                {
                    PerformLatencyCheck();
                }
                
                // Cleanup for audio spam checker
                CleanupDeadAudioSources();

                yield return new WaitForSeconds(1.0f);
            }
        }
        #endregion

        #region Ported: Audio Spam Protection
        internal static bool CheckAudioCooldown(AudioSource instance)
        {
            if (!EnableMasterSwitch.Value || !EnableAudioSpamProtection.Value || instance == null) return true;

            // Silently block calls on disabled GameObjects to prevent Unity warnings.
            if (!instance.gameObject.activeInHierarchy) return false;

            lock (AudioCooldowns)
            {
                if (AudioCooldowns.TryGetValue(instance, out float cooldownEndTime) && Time.time < cooldownEndTime)
                {
                    return false; // Silently block.
                }
                AudioCooldowns[instance] = Time.time + AudioSpamCooldownSeconds;
            }
            return true;
        }
        
        private void CleanupDeadAudioSources()
        {
            lock (AudioCooldowns)
            {
                DeadAudioSources.Clear();
                foreach (var audioSource in AudioCooldowns.Keys)
                {
                    if (audioSource == null) // Check if destroyed
                    {
                        DeadAudioSources.Add(audioSource);
                    }
                }

                foreach (var deadSource in DeadAudioSources)
                {
                    AudioCooldowns.Remove(deadSource);
                }
            }
        }
        #endregion
        
        #region Ported: Global RPC Shield
        private void InitializeShield()
        {
            if (_isShieldActive || !NetworkClient.active) return;
            try
            {
                var handlersField = typeof(NetworkClient).GetField("handlers", BindingFlags.NonPublic | BindingFlags.Static);
                if (handlersField == null)
                {
                    Log.LogError("[Shield] Could not find NetworkClient.handlers field!");
                    return;
                }

                var handlers = handlersField.GetValue(null) as Dictionary<ushort, NetworkMessageDelegate>;
                ushort rpcMsgId = NetworkMessageId<RpcMessage>.Id;

                if (handlers != null && handlers.TryGetValue(rpcMsgId, out var originalHandler))
                {
                    OriginalHandlers[rpcMsgId] = originalHandler;
                    handlers[rpcMsgId] = ThrottledMessageHandler;
                    if(_shieldResetCoroutine != null) StopCoroutine(_shieldResetCoroutine);
                    _shieldResetCoroutine = StartCoroutine(ResetMessageCountersCoroutine());
                    _isShieldActive = true;
                    Log.LogMessage("[Shield] Global RPC Network Shield is ACTIVE.");
                }
            }
            catch (Exception e)
            {
                Log.LogError($"[Shield] Failed to initialize Network Shield: {e}");
            }
        }

        private void ShutdownShield()
        {
            if (!_isShieldActive) return;
            try
            {
                var handlersField = typeof(NetworkClient).GetField("handlers", BindingFlags.NonPublic | BindingFlags.Static);
                var handlers = handlersField?.GetValue(null) as Dictionary<ushort, NetworkMessageDelegate>;
                ushort rpcMsgId = NetworkMessageId<RpcMessage>.Id;

                if (handlers != null && OriginalHandlers.TryGetValue(rpcMsgId, out var originalHandler))
                {
                    handlers[rpcMsgId] = originalHandler; // Restore original handler
                }

                if (_shieldResetCoroutine != null) StopCoroutine(_shieldResetCoroutine);
                _shieldResetCoroutine = null;

                OriginalHandlers.Clear();
                RpcMessageCounts.Clear();
                _isShieldActive = false;
                Log.LogMessage("[Shield] Global RPC Network Shield is INACTIVE.");
            }
            catch (Exception e)
            {
                Log.LogError($"[Shield] Failed to shutdown Network Shield: {e}");
            }
        }
        
        private static void ThrottledMessageHandler(NetworkConnection conn, NetworkReader reader, int channelId)
        {
            if (!OriginalHandlers.TryGetValue(NetworkMessageId<RpcMessage>.Id, out var originalHandler)) return;

            int initialPosition = reader.Position;

            // Read message details to identify the RPC
            if (reader.Remaining < 7) { originalHandler(conn, reader, channelId); return; }
            uint netId = reader.ReadUInt();
            reader.ReadByte(); // componentIndex
            ushort funcHash = reader.ReadUShort();

            // Reset reader for original handler
            reader.Position = initialPosition;

            if (RpcWhitelist.Contains(funcHash))
            {
                originalHandler(conn, reader, channelId);
                return;
            }

            RpcMessageCounts.TryGetValue(funcHash, out int currentCount);
            currentCount++;
            RpcMessageCounts[funcHash] = currentCount;

            if (currentCount > RpcSpamThreshold)
            {
                if (currentCount == RpcSpamThreshold + 1 && EnableDetailedLogging.Value) // Log only once
                {
                    string senderName = $"netId:{netId}";
                    if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity) && identity != null)
                    {
                        var player = identity.GetComponentInParent<Player>();
                        senderName = player != null ? player._nickname : identity.name;
                    }
                    Log.LogError($"[Shield] RPC SPAM DETECTED! Blocking excessive calls (Hash: {funcHash}) from sender: {senderName}.");
                }
                return; // Block the RPC by not calling the original handler
            }

            originalHandler(conn, reader, channelId);
        }

        private static IEnumerator ResetMessageCountersCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(ShieldResetInterval);
                RpcMessageCounts.Clear();
            }
        }
        #endregion

        #region Ported: Object Spike & Health Checks
        private void CheckForObjectSpikes()
        {
            int currentObjectCount = CountNetworkObjects();
            int delta = currentObjectCount - _lastNetworkObjectCount;

            if (delta > NetworkObjectSpikeThreshold)
            {
                Log.LogError($"[Panic] Object Spike Detected! {delta} new objects. Engaging Panic Cleanup.");
                StartCoroutine(EngagePanicCleanup());
            }

            _lastNetworkObjectCount = currentObjectCount;
        }
        
        private static IEnumerator EngagePanicCleanup()
        {
            _isPanicModeActive = true;
            var objectsToDestroy = new List<GameObject>();
            try
            {
                // Find all active, networked objects that aren't players
                var allNetIDs = FindObjectsOfType<NetworkIdentity>();
                if (EnableDetailedLogging.Value)
                    Log.LogMessage($"[Panic] Scanning {allNetIDs.Length} network objects.");

                foreach (var netId in allNetIDs)
                {
                    if (netId == null || !netId.gameObject.activeInHierarchy) continue;
                    if (netId.GetComponent<Player>() == null && netId.GetComponent<Collider>() != null)
                    {
                        objectsToDestroy.Add(netId.gameObject);
                    }
                }
            }
            catch (Exception e)
            {
                Log.LogError($"[Panic] Error during neutralization phase: {e}");
                _isPanicModeActive = false;
                yield break;
            }

            Log.LogMessage($"[Panic] Neutralizing {objectsToDestroy.Count} objects.");
            foreach (var go in objectsToDestroy)
            {
                if (go != null)
                {
                    // Disable immediately, destroy over time
                    if (go.TryGetComponent<Renderer>(out var renderer)) renderer.enabled = false;
                    if (go.TryGetComponent<Collider>(out var collider)) collider.enabled = false;
                }
            }

            yield return null; // Wait a frame

            for (int i = 0; i < objectsToDestroy.Count; i++)
            {
                if (objectsToDestroy[i] != null)
                    Destroy(objectsToDestroy[i]);
                if (i % 50 == 0) yield return null; // Stagger destruction
            }

            Log.LogMessage("[Panic] Cleanup complete.");
            _isPanicModeActive = false;
            _lastNetworkObjectCount = CountNetworkObjects();
        }

        private static void PerformLatencyCheck()
        {
            Player localPlayer = Player._mainPlayer;
            if (localPlayer == null) return;
            
            if (localPlayer.Network_isHostPlayer)
            {
                DeadServerStatus = "<color=cyan>Host</color>";
                return;
            }
            
            float currentLatency = localPlayer.Network_latency;

            if (Mathf.Approximately(currentLatency, _previousLatency))
            {
                _stuckDuration += 1.0f; // Interval of the coroutine
                if (_stuckDuration >= StuckThresholdSeconds && !_serverStuckReportSent)
                {
                    _serverStuckReportSent = true;
                    DeadServerStatus = "<color=red>Frozen</color>";
                    Log.LogError("[HealthCheck] Server is unresponsive (latency is frozen).");
                }
            }
            else
            {
                if (_serverStuckReportSent)
                {
                     Log.LogMessage("[HealthCheck] Server has recovered and is responsive again.");
                }
                _stuckDuration = 0f;
                _serverStuckReportSent = false;
            }

            if (!_serverStuckReportSent)
            {
                DeadServerStatus = currentLatency > HighLatencyThreshold
                    ? $"<color=orange>Lagging ({currentLatency}ms)</color>"
                    : $"<color=#2bff00>Stable ({currentLatency}ms)</color>";
            }

            _previousLatency = currentLatency;
        }

        private static int CountNetworkObjects()
        {
            try { return FindObjectsOfType<NetworkIdentity>().Length; }
            catch { return 0; }
        }
        #endregion
    }
}