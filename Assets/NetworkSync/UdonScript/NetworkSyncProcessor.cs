using Newtonsoft.Json.Linq;
using System;
using System.Runtime.CompilerServices;
using System.Text;
using UdonSharp;
using Unity.Burst.Intrinsics;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class NetworkSyncProcessor : UdonSharpBehaviour
{
    [SerializeField] private GameObject SyncPlayerObject = null;

    [UdonSynced, FieldChangeCallback(nameof(DataPackets))] private string dataPackets = "";
    private string receiveDataPacketsBuffer = "";
    private bool[] SyncObject_Enabled = new bool[0];
    private NetworkSyncObject[] syncObject_Object = new NetworkSyncObject[0];
    private int syncObjectListLength = 0;
    private bool isNetworkRegistered = false;
    private DataDictionary playerRegistered = new DataDictionary();
    private DataDictionary playerSuspended = new DataDictionary();
    private bool hasSuspendedPlayerJoined = false;
    private NetworkSyncPlayer[] syncPlayerList = null;
    private DataDictionary syncPlayerMap = new DataDictionary();
    private DataList syncPlayerAvailableList = new DataList();

    private string DataPackets
    {
        set
        {
            Receive(value);
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

    public void OnSyncPlayerDataReceived(string str)
    {
        Receive(str);
    }

    private void Receive(string str)
    {
        if(isNetworkRegistered)
        {
            foreach(var dataPacket in str.Split(";"))
            {
                if(dataPacket.Length > 0)
                {
                    var items = dataPacket.Split(",");
                    var id = int.Parse(items[0]);
                    var srcPlayerId = int.Parse(items[1]);
                    var srcPlayer = VRCPlayerApi.GetPlayerById(srcPlayerId);
                    var dstPlayerId = int.Parse(items[2]);

                    if((srcPlayer != null) && ((dstPlayerId < 0) || (dstPlayerId == Networking.LocalPlayer.playerId)))
                    {
                        if(playerRegistered.ContainsKey(srcPlayerId) && !playerSuspended.ContainsKey(srcPlayerId))
                        {
                            var cmd = items[3];
                            var arg = (items.Length > 4) ? ObjectSerializer.Deserialize<object>(items[4]) : null;
                            var prefix = cmd.Substring(0, 1);
                            var subcmd = cmd.Substring(1);

                            switch(prefix)
                            {
                                case "u":
                                    syncObject_Object[id].NetworkSync_OnDataReceived(srcPlayer, subcmd, arg);
                                    break;

                                case "s":
                                    switch(subcmd)
                                    {
                                        case "syncplayer":
                                        {
                                            var argList = (DataList)arg;
                                            syncPlayerMap = argList[0].DataDictionary;
                                            syncPlayerAvailableList = argList[1].DataList;
                                            break;
                                        }
                                    }

                                    break;
                            }
                        }
                        else
                        {
                            receiveDataPacketsBuffer += dataPacket + ";";
                            playerSuspended[srcPlayerId] = true;
                        }
                    }
                }
            }
        }
    }

    public void AddSyncObject(NetworkSyncObject syncObject)
    {
        if(syncObject.NetworkSync_GetId() < 0)
        {
            var id = syncObjectListLength++;
        
            if(SyncObject_Enabled.Length < syncObjectListLength)
            {
                {
                    var tmp = SyncObject_Enabled;
                    var newLength = (tmp.Length > 0) ? (tmp.Length * 2) : 1;
                    SyncObject_Enabled = new bool[newLength];

                    for(var i = 0;i < tmp.Length;i++)
                    {
                        SyncObject_Enabled[i] = tmp[i];
                    }

                    for(var i = tmp.Length;i < newLength;i++)
                    {
                        SyncObject_Enabled[i] = false;
                    }
                }

                {
                    var tmp = syncObject_Object;
                    var newLength = (tmp.Length > 0) ? (tmp.Length * 2) : 1;
                    syncObject_Object = new NetworkSyncObject[newLength];

                    for(var i = 0;i < tmp.Length;i++)
                    {
                        syncObject_Object[i] = tmp[i];
                    }

                    for(var i = tmp.Length;i < newLength;i++)
                    {
                        syncObject_Object[i] = null;
                    }
                }
            }

            SyncObject_Enabled[id] = true;
            syncObject_Object[id] = syncObject;
            syncObject.NetworkSync_SetId(id);
        }
    }

    public bool IsOwner()
    {
        return Networking.IsOwner(Networking.LocalPlayer, gameObject);
    }

    public void ChangeOwner(VRCPlayerApi player)
    {
        Networking.SetOwner(player, gameObject);
    }

    public void Send(NetworkSyncObject syncObject, VRCPlayerApi dstPlayer, string cmd, object arg)
    {
        Send_Internal(syncObject.NetworkSync_GetId(), dstPlayer, "u" + cmd, arg);
    }

    private void Send_System(VRCPlayerApi dstPlayer, string cmd, object arg)
    {
        Send_Internal(-1, dstPlayer, "s" + cmd, arg);
    }

    private void Send_Internal(int syncId, VRCPlayerApi dstPlayer, string cmd, object arg)
    {
        var dstPlayerId = (dstPlayer == null) ? -1 : dstPlayer.playerId;
        var result = "" + syncId + "," + Networking.LocalPlayer.playerId + "," + dstPlayerId + "," + cmd;

        if(arg != null)
        {
            result += "," + ObjectSerializer.Serialize(arg);
        }

        if(IsOwner())
        {
            dataPackets += result + ";";
            RequestSerialization();
        }
        else if(syncPlayerMap.ContainsKey(Networking.LocalPlayer.playerId))
        {
            var syncPlayerId = syncPlayerMap[Networking.LocalPlayer.playerId].Int;
            syncPlayerList[syncPlayerId].Send(result);
        }
    }

    private void AssignNewSyncPlayer(int playerId)
    {
        var newSyncPlayerId = syncPlayerAvailableList[0];
        syncPlayerAvailableList.RemoveAt(0);
        syncPlayerMap[playerId] = newSyncPlayerId;
        syncPlayerList[newSyncPlayerId.Int].AssignPlayer(VRCPlayerApi.GetPlayerById(playerId));
        SyncSyncPlayerInfo();
    }

    private void WithdrawSyncPlayer(int playerId)
    {
        if(syncPlayerMap.ContainsKey(playerId))
        {
            var syncPlayerId = syncPlayerMap[playerId].Int;
            syncPlayerAvailableList.Add(syncPlayerMap[playerId].Int);
            syncPlayerMap.Remove(playerId);
            syncPlayerList[syncPlayerId].AssignPlayer(Networking.LocalPlayer);
            SyncSyncPlayerInfo();
        }
    }

    private void RestoreSyncPlayerInfo()
    {
        WithdrawSyncPlayer(Networking.LocalPlayer.playerId);

        {
            var playerIds = playerRegistered.GetKeys();

            for(var i = 0;i < playerIds.Count;i++)
            {
                if(playerIds[i] != Networking.LocalPlayer.playerId)
                {
                    if(!syncPlayerMap.ContainsKey(playerIds[i]))
                    {
                        AssignNewSyncPlayer(playerIds[i].Int);
                    }
                }
            }
        }

        {
            var playerIds = syncPlayerMap.GetKeys();

            for(var i = 0;i < playerIds.Count;i++)
            {
                if(!playerRegistered.ContainsKey(playerIds[i]))
                {
                    WithdrawSyncPlayer(playerIds[i].Int);
                }
            }
        }
    }

    private void SyncSyncPlayerInfo()
    {
        var list = new DataList();
        list.Add(syncPlayerMap);
        list.Add(syncPlayerAvailableList);
        Send_System(null, "syncplayer", list);
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        base.OnOwnershipTransferred(player);

        if(player.isLocal)
        {
            if(SyncPlayerObject != null)
            {
                RestoreSyncPlayerInfo();
            }

            for(var i = 0;i < syncObjectListLength;i++)
            {
                syncObject_Object[i].NetworkSync_OnGotOwner();
            }
        }
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        base.OnPlayerJoined(player);

        if(player.isLocal)
        {
            isNetworkRegistered = true;
        }

        playerRegistered.Add(player.playerId, true);

        if(playerSuspended.ContainsKey(player.playerId))
        {
            hasSuspendedPlayerJoined = true;
        }

        for(var i = 0;i < syncObjectListLength;i++)
        {
            syncObject_Object[i].NetworkSync_OnPlayerJoined(player);
        }

        if((SyncPlayerObject != null) && !player.isLocal && IsOwner())
        {
            AssignNewSyncPlayer(player.playerId);
        }
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        base.OnPlayerLeft(player);
        
        if(player.isLocal)
        {
            isNetworkRegistered = false;
        }

        playerRegistered.Remove(player.playerId);
        playerSuspended.Remove(player.playerId);

        if((SyncPlayerObject != null) && !player.isLocal && IsOwner())
        {
            WithdrawSyncPlayer(player.playerId);
        }
    }

    public bool IsNetworkRegistered()
    {
        if(isNetworkRegistered)
        {
            if(IsOwner())
            {
                return true;
            }
            else if(syncPlayerMap.ContainsKey(Networking.LocalPlayer.playerId))
            {
                return true;
            }
        }

        return false;
    }

    void Update()
    {
        if(hasSuspendedPlayerJoined)
        {
            hasSuspendedPlayerJoined = false;

            if(receiveDataPacketsBuffer.Length > 0)
            {
                playerSuspended.Clear();
                var str = receiveDataPacketsBuffer;
                receiveDataPacketsBuffer = "";
                Receive(str);
            }
        }
    }

    void Start()
    {
        if(SyncPlayerObject != null)
        {
            syncPlayerList = SyncPlayerObject.GetComponentsInChildren<NetworkSyncPlayer>();
            
            for(var i = 0;i < syncPlayerList.Length;i++)
            {
                syncPlayerList[i].RegisterSyncProcessor(this);
                syncPlayerAvailableList.Add(i);
            }
        }
    }
}
