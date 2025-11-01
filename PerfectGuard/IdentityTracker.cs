using Mirror;

namespace Marioalexsan.PerfectGuard;
public static class NetworkIdentityManager
{
    public static readonly List<NetworkIdentity> AllIdentities = new List<NetworkIdentity>();

    public static int Count => AllIdentities.Count;
}
public class IdentityTracker : NetworkBehaviour
{
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!NetworkIdentityManager.AllIdentities.Contains(netIdentity))
        {
            NetworkIdentityManager.AllIdentities.Add(netIdentity);
        }
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        NetworkIdentityManager.AllIdentities.Remove(netIdentity);
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!NetworkIdentityManager.AllIdentities.Contains(netIdentity))
        {
            NetworkIdentityManager.AllIdentities.Add(netIdentity);
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        NetworkIdentityManager.AllIdentities.Remove(netIdentity);
    }
}