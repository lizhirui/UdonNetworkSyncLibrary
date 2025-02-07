using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;
using static Unity.Burst.Intrinsics.X86.Avx;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]

public class NetworkSyncObject : UdonSharpBehaviour
{
    [SerializeField] public NetworkSyncProcessor SyncProcessor = null;
    [SerializeField, FieldChangeCallback(nameof(GlobalMode))] private bool _globalMode = false;
    [SerializeField] public UdonSharpBehaviour EventObject = null;
    [SerializeField] public string OnRPCReceivedName = "NetworkSync_OnRPCReceived";
    [SerializeField] public string OnPongReceivedName = "NetworkSync_OnPongReceived";
    [SerializeField] public string OnGetValueEventName = "NetworkSync_OnGetValue";
    [SerializeField] public string OnSetValueEventName = "NetworkSync_OnSetValue";
    [SerializeField] public string OnGotOwnerEventName = "NetworkSync_OnGotOwner";

    private int id = -1;
    private DataDictionary propertyMap = new DataDictionary();
    private VRCPlayerApi argSrcPlayer = null;
    private string argCmd = "";
    private object argArg = null;
    private object argReturnValue = null;

    public bool GlobalMode
    {
        get => _globalMode;

        set
        {
            _globalMode = value;

            if(NetworkSync_IsReady())
            {
                if(NetworkSync_IsOwner())
                {
                    SendObjectCmd(null, "g", value);

                    if(value)
                    {
                        var propertyList = propertyMap.GetKeys();

                        for(var i = 0;i < propertyList.Count;i++)
                        {
                            if(EventObject != null)
                            {
                                argCmd = propertyList[i].String;
                                argArg = null;
                                argReturnValue = null;
                                EventObject.SendCustomEvent(OnGetValueEventName);
                                SendSetCmd(null, propertyList[i].String, argReturnValue);
                            }
                            else
                            {
                                SendSetCmd(null, propertyList[i].String, NetworkSync_OnGetValue(propertyList[i].String));
                            }
                        }
                    }
                }
            }
        }
    }

    public void NetworkSync_OnDataReceived(VRCPlayerApi srcPlayer, string cmd, object arg)
    {
        var prefix = cmd.Substring(0, 1);
        var subcmd = cmd.Substring(1);

        switch(prefix)
        {
            case "s":
                if(EventObject != null)
                {
                    argCmd = subcmd;
                    argArg = arg;
                    argReturnValue = null;
                    EventObject.SendCustomEvent(OnSetValueEventName);
                }
                else
                {
                    NetworkSync_OnSetValue(srcPlayer, subcmd, arg);
                }

                break;

            case "o":
                switch(subcmd)
                {
                    case "g":
                        GlobalMode = (bool)arg;
                        break;

                    case "ping":
                        SendPong(srcPlayer, arg);
                        break;

                    case "pong":
                        if(EventObject != null)
                        {
                            argSrcPlayer = srcPlayer;
                            argCmd = null;
                            var args = new object[2]{arg, (int)((Time.unscaledTime - ((double)arg)) * 1000.0 + 0.5)};;
                            argArg = args;
                            argReturnValue = null;
                            EventObject.SendCustomEvent(OnPongReceivedName);
                        }
                        else
                        {
                            NetworkSync_OnPongReceived(srcPlayer, (double)arg, (int)((Time.unscaledTime - ((double)arg)) * 1000.0 + 0.5));
                        }

                        break;
                }

                break;

            case "r":
                if(EventObject != null)
                {
                    argSrcPlayer = srcPlayer;
                    argCmd = subcmd;
                    argArg = arg;
                    argReturnValue = null;
                    EventObject.SendCustomEvent(OnRPCReceivedName);
                }
                else
                {
                    NetworkSync_OnRPCReceived(srcPlayer, subcmd, arg);
                }

                break;
        }
    }

    public bool NetworkSync_IsOwner()
    {
        return Networking.IsOwner(gameObject);
    }

    public VRCPlayerApi NetworkSync_GetOwner()
    {
        return Networking.GetOwner(gameObject);
    }

    private void SendSetCmd(VRCPlayerApi dstPlayer, string cmd, object value)
    {
        if(SyncProcessor != null)
        {
            SyncProcessor.Send(this, dstPlayer, "s" + cmd, value);
        }
    }

    private void SendObjectCmd(VRCPlayerApi dstPlayer, string cmd, object arg)
    {
        if(SyncProcessor != null)
        {
            SyncProcessor.Send(this, dstPlayer, "o" + cmd, arg);
        }
    }

    protected virtual void NetworkSync_OnRPCReceived(VRCPlayerApi srcPlayer, string cmd, object args)
    {
        
    }

    protected virtual void NetworkSync_OnPongReceived(VRCPlayerApi srcPlayer, double startTime, int rtt)
    {
        
    }

    protected virtual object NetworkSync_OnGetValue(string cmd)
    {
        return null;
    }

    protected virtual void NetworkSync_OnSetValue(VRCPlayerApi srcPlayer, string cmd, object value)
    {
        
    }

    public virtual void NetworkSync_OnGotOwner()
    {
        if(EventObject != null)
        {
            EventObject.SendCustomEvent(OnGotOwnerEventName);
        }
    }

    public void NetworkSync_RegisterProperty(string cmd)
    {
        if(cmd.Contains(",") || cmd.Contains(":") || cmd.Contains(";"))
        {
            return;
        }

        if(propertyMap.ContainsKey(cmd))
        {
            return;
        }

        propertyMap.Add(cmd, true);
    }

    public void NetworkSync_SyncValue(string cmd)
    {
        if(GlobalMode)
        {
            if(NetworkSync_IsReady())
            {
                if(NetworkSync_IsOwner())
                {
                    if(propertyMap.ContainsKey(cmd))
                    {
                        if(EventObject != null)
                        {
                            argCmd = cmd;
                            argArg = null;
                            argReturnValue = null;
                            EventObject.SendCustomEvent(OnGetValueEventName);
                            SendSetCmd(null, cmd, argReturnValue);
                        }
                        else
                        {
                            SendSetCmd(null, cmd, NetworkSync_OnGetValue(cmd));
                        }
                    }
                }
            }
        }
    }

    public void NetworkSync_Init()
    {
        if(SyncProcessor != null)
        {
            SyncProcessor.AddSyncObject(this);
        }
    }

    public void NetworkSync_SetId(int _id)
    {
        if(id < 0)
        {
            id = _id;
        }
    }

    public int NetworkSync_GetId()
    {
        return id;
    }

    public bool NetworkSync_IsReady()
    {
        return (id >= 0) && (SyncProcessor != null);
    }

    public bool NetworkSync_IsNetworkRegistered()
    {
        return NetworkSync_IsReady() && SyncProcessor.IsNetworkRegistered();
    }

    public void NetworkSync_SendRPC(VRCPlayerApi dstPlayer, string cmd, object arg)
    {
        if(NetworkSync_IsReady())
        {
            SyncProcessor.Send(this, dstPlayer, "r" + cmd, arg);
        }
    }

    public void NetworkSync_SendPing(VRCPlayerApi dstPlayer)
    {
        SendObjectCmd(dstPlayer, "ping", Time.unscaledTimeAsDouble);
    }

    private void SendPong(VRCPlayerApi dstPlayer, object arg)
    {
        SendObjectCmd(dstPlayer, "pong", arg);
    }

    public VRCPlayerApi NetworkSync_Arg_GetSrcPlayer()
    {
        return argSrcPlayer;
    }

    public string NetworkSync_Arg_GetCmd()
    {
        return argCmd;
    }

    public object NetworkSync_Arg_GetArg()
    {
        return argArg;
    }

    public void NetworkSync_SetReturnValue(object returnValue)
    {
        argReturnValue = returnValue;
    }

    public virtual void NetworkSync_OnPlayerJoined(VRCPlayerApi player)
    {
        if(player.isLocal)
        {
            GlobalMode = GlobalMode;
        }
        else
        {
            if(GlobalMode)
            {
                if(NetworkSync_IsReady())
                {
                    if(NetworkSync_IsOwner())
                    {
                        var propertyList = propertyMap.GetKeys();

                        for(var i = 0;i < propertyList.Count;i++)
                        {
                            if(EventObject != null)
                            {
                                argCmd = propertyList[i].String;
                                argArg = null;
                                argReturnValue = null;
                                EventObject.SendCustomEvent(OnGetValueEventName);
                                SendSetCmd(player, propertyList[i].String, argReturnValue);
                            }
                            else
                            {
                                SendSetCmd(player, propertyList[i].String, NetworkSync_OnGetValue(propertyList[i].String));
                            }
                        }
                    }
                }
            }
        }
    }
    
    public virtual void Start()
    {
        if(SyncProcessor == null)
        {
            SyncProcessor = gameObject.GetComponent<NetworkSyncProcessor>();
        }
        
        NetworkSync_Init();
    }
}
