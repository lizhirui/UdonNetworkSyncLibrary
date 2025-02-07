
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class NetworkSyncPlayer : UdonSharpBehaviour
{
    private NetworkSyncProcessor syncProcessor = null;
    [UdonSynced, FieldChangeCallback(nameof(DataPackets))] private string dataPackets = "";

    private string DataPackets
    {
        set
        {
            if(syncProcessor != null)
            {
                syncProcessor.OnSyncPlayerDataReceived(value);
            }
        }
    }

    public override void OnPostSerialization(SerializationResult result)
    {
        base.OnPostSerialization(result);

        if(dataPackets.Length > 0)
        {
            dataPackets = "";
            RequestSerialization();
        }
    }

    public void Send(string str)
    {
        if(syncProcessor != null)
        {
            dataPackets += str + ";";
            RequestSerialization();
        }
    }

    public void AssignPlayer(VRCPlayerApi player)
    {
        Networking.SetOwner(player, gameObject);
    }

    public void RegisterSyncProcessor(NetworkSyncProcessor syncProcessor)
    {
        this.syncProcessor = syncProcessor;
    }

    void Start()
    {
        
    }
}
