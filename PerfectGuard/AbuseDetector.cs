using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Marioalexsan.PerfectGuard
{
    public enum SuspicionLevel { Normal, Suspicious, Confirmed }
    internal class AbuseDetector
    {
        public static List<AbuseDetector> AllDetectors { get; } = new();
        public static void RunActorCleanup()
        {
            foreach (var detector in AllDetectors)
            {
                var playerData = detector.PlayerData;
                foreach (var pair in playerData.ToArray()) { if (!pair.Key) playerData.Remove(pair.Key); }
            }
        }
        public AbuseDetector(double suspicionRate) : this(suspicionRate, suspicionRate * 4) { }
        public AbuseDetector(double suspicionRate, double confirmRate)
        {
            SupicionRate = suspicionRate;
            ConfirmRate = Math.Max(confirmRate, suspicionRate);
            SuspicionTimeBetweenEvents = TimeSpan.FromSeconds(1 / SupicionRate);
            ConfirmTimeBetweenEvents = TimeSpan.FromSeconds(1 / ConfirmRate);
            AllDetectors.Add(this);
        }
        public double SupicionRate { get; }
        public double ConfirmRate { get; }
        public double Factor { get; } = 0.3;
        public TimeSpan SuspicionTimeBetweenEvents { get; }
        public TimeSpan ConfirmTimeBetweenEvents { get; }
        public event EventHandler<Player>? OnSuspicionRaised;
        public event EventHandler<Player>? OnConfirmRaised;
        private struct TrackedData { public DateTime TimeSinceLastEvent; public TimeSpan TimeBetweenEventsEMA; public SuspicionLevel Suspicion; }
        private readonly Dictionary<Player, TrackedData> PlayerData = new();
        public bool TrackIfServerAndCheckBehaviour(Player player) => NetworkServer.active ? TrackEventAndCheckBehaviour(player) : CheckBehaviour(player);
        public bool CheckBehaviour(Player player) => !PlayerData.TryGetValue(player, out var playerData) || playerData.Suspicion == SuspicionLevel.Normal;
        public bool TrackEventAndCheckBehaviour(Player player)
        {
            var currentTime = DateTime.Now;
            if (!PlayerData.TryGetValue(player, out var playerData))
            {
                PlayerData[player] = new TrackedData() { TimeSinceLastEvent = currentTime, TimeBetweenEventsEMA = SuspicionTimeBetweenEvents * 2, Suspicion = SuspicionLevel.Normal };
                return true;
            }
            var timeSinceLastEvent = currentTime - playerData.TimeSinceLastEvent;
            var currentEMA = Factor * timeSinceLastEvent + (1 - Factor) * playerData.TimeBetweenEventsEMA;
            playerData.TimeSinceLastEvent = currentTime;
            playerData.TimeBetweenEventsEMA = currentEMA;
            var newSuspicionLevel = SuspicionLevel.Normal;
            if (currentEMA <= ConfirmTimeBetweenEvents)
            {
                newSuspicionLevel = SuspicionLevel.Confirmed;
                if (newSuspicionLevel > playerData.Suspicion) OnConfirmRaised?.Invoke(this, player);
            }
            else if (currentEMA <= SuspicionTimeBetweenEvents)
            {
                newSuspicionLevel = SuspicionLevel.Suspicious;
                if (newSuspicionLevel > playerData.Suspicion) OnSuspicionRaised?.Invoke(this, player);
            }
            PlayerData[player] = playerData with { Suspicion = newSuspicionLevel };
            return newSuspicionLevel == SuspicionLevel.Normal;
        }
    }
}