using Mirror;

namespace Marioalexsan.PerfectGuard;


public class NetItemObjectTracker : NetworkBehaviour
{
    private Net_ItemObject _itemObject;

    public void Awake()
    {
        // Get the component we need to track.
        _itemObject = GetComponent<Net_ItemObject>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (_itemObject != null && !NetItemObjectManager.AllItemObjects.Contains(_itemObject))
        {
            NetItemObjectManager.AllItemObjects.Add(_itemObject);
        }
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (_itemObject != null)
        {
            NetItemObjectManager.AllItemObjects.Remove(_itemObject);
        }
    }
}