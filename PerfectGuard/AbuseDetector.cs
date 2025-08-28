using HarmonyLib;
using Mirror;

namespace Marioalexsan.PerfectGuard;

public enum SuspicionLevel
{
    Normal,
    Suspicious,
    Confirmed
}

internal class AbuseDetector
{
    public static List<AbuseDetector> AllDetectors { get; } = [];

    public static void RunActorCleanup()
    {
        for (int i = 0; i < AllDetectors.Count; i++)
        {
            var playerData = AllDetectors[i].PlayerData;

            foreach (var pair in playerData.ToArray())
            {
                if (!pair.Key)
                    playerData.Remove(pair.Key);
            }
        }
    }

    public AbuseDetector(double suspicionRate) : this(suspicionRate, suspicionRate * 4) { }

    public AbuseDetector(double suspicionRate, double confirmRate)
    {
        // Enforce that confirmRate >= suspicionRate
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

    struct TrackedData
    {
        public DateTime TimeSinceLastEvent;
        public TimeSpan TimeBetweenEventsEMA;
        public SuspicionLevel Suspicion;
    }

    private readonly Dictionary<Player, TrackedData> PlayerData = [];

    public bool TrackIfServerAndCheckBehaviour(Player player) => NetworkServer.active ? TrackEventAndCheckBehaviour(player) : CheckBehaviour(player);
    public bool TrackIfClientAndCheckBehaviour(Player player) => NetworkClient.active && !NetworkServer.active ? TrackEventAndCheckBehaviour(player) : CheckBehaviour(player);

    /// <summary>
    /// Returns true if the current player behaviour seems normal.
    /// </summary>
    /// <returns>True if current behaviour should be considered as normal, false otherwise</returns>
    public bool CheckBehaviour(Player player)
    {
        if (PlayerData.TryGetValue(player, out var playerData))
            return playerData.Suspicion == SuspicionLevel.Normal;

        return true;
    }

    /// <summary>
    /// Tracks event and returns true if the current player behaviour seems normal.
    /// </summary>
    /// <param name="player">The player to track event for</param>
    /// <returns>True if current behaviour should be considered as normal, false otherwise</returns>
    public bool TrackEventAndCheckBehaviour(Player player)
    {
        var currentTime = DateTime.Now;

        if (!PlayerData.TryGetValue(player, out var playerData))
        {
            PlayerData[player] = new TrackedData()
            {
                TimeSinceLastEvent = currentTime,
                TimeBetweenEventsEMA = SuspicionTimeBetweenEvents * 2,
                Suspicion = SuspicionLevel.Normal
            };
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

            if (newSuspicionLevel > playerData.Suspicion)
                OnConfirmRaised?.Invoke(this, player);
        }

        else if (currentEMA <= SuspicionTimeBetweenEvents)
        {
            newSuspicionLevel = SuspicionLevel.Suspicious;

            if (newSuspicionLevel > playerData.Suspicion)
                OnSuspicionRaised?.Invoke(this, player);
        }

        PlayerData[player] = playerData with { Suspicion = newSuspicionLevel };

        return newSuspicionLevel == SuspicionLevel.Normal;
    }
}
