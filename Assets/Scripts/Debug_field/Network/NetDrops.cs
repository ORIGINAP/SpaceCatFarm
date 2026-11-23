using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// 버린 아이템 공유, 줍기는 먼저 주운 사람 (호스트 판정)
public static class NetDrops
{
    [Serializable]
    private class DropMsg
    {
        public int    kind;
        public long   id;
        public string item;
        public int    n;
        public float  x, y, vx, vy;
        public bool   thrown;
    }

    private const int Spawn = 0, Claim = 1, Remove = 2;

    private static readonly Dictionary<long, DroppedItem> drops = new();
    private static readonly List<NetPeerInfo> peers = new();
    private static int counter;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        drops.Clear();
        peers.Clear();
        counter = 0;
    }

    public static bool Active => NetSession.IsHost ? NetHost.Current != null : NetClient.CanSend;

    public static void Share(DroppedItem d)
    {
        if (d == null || d.NetId != 0 || !Active || NetworkManager.Singleton == null) return;
        long id = ((long)(NetworkManager.Singleton.LocalClientId + 1) << 32) | (uint)++counter;
        d.NetId = id;
        drops[id] = d;
        var msg = SpawnMsg(d);
        if (NetSession.IsHost) ToAll(msg);
        else NetWire.SendJson(NetWire.Drop, NetworkManager.ServerClientId, msg);
    }

    public static void Forget(DroppedItem d)
    {
        if (drops.TryGetValue(d.NetId, out var cur) && cur == d) drops.Remove(d.NetId);
    }

    // 내가 주움
    public static void PickedUp(DroppedItem d)
    {
        Forget(d);
        if (!Active) return;
        var msg = new DropMsg { id = d.NetId, item = d.Item != null ? d.Item.name : null, n = d.Amount };
        if (NetSession.IsHost)
        {
            msg.kind = Remove;
            ToAll(msg);
        }
        else
        {
            msg.kind = Claim;
            NetWire.SendJson(NetWire.Drop, NetworkManager.ServerClientId, msg);
        }
    }

    // 늦게 들어온 참가자에게
    public static void SendAll(ulong to)
    {
        foreach (var d in drops.Values)
            if (d != null) NetWire.SendJson(NetWire.Drop, to, SpawnMsg(d));
    }

    public static void Receive(ulong sender, byte[] data)
    {
        var msg = NetWire.Decode<DropMsg>(data);
        if (msg == null || msg.id == 0) return;

        if (NetSession.IsHost)
        {
            if (msg.kind == Spawn)
            {
                if (Create(msg)) ToAll(msg, except: sender);
            }
            else if (msg.kind == Claim)
            {
                if (drops.TryGetValue(msg.id, out var d) && d != null)
                {
                    Forget(d);
                    d.Vanish();
                    ToAll(new DropMsg { kind = Remove, id = msg.id }, except: sender);
                }
                // 남이 먼저 주웠으면 도로 가져감
                else NetHost.GiveTo(sender, NetWorld.Item(msg.item), -msg.n);
            }
            return;
        }

        if (!NetClient.CanSend) return;
        if (msg.kind == Spawn) Create(msg);
        else if (msg.kind == Remove && drops.TryGetValue(msg.id, out var gone))
        {
            drops.Remove(msg.id);
            if (gone != null) gone.Vanish();
        }
    }

    private static bool Create(DropMsg msg)
    {
        if (drops.ContainsKey(msg.id)) return false;
        var item = NetWorld.Item(msg.item);
        if (item == null || msg.n <= 0) return false;
        var d = DroppedItem.SpawnRemote(item, msg.n, new Vector3(msg.x, msg.y, 0f), new Vector2(msg.vx, msg.vy), msg.thrown);
        if (d == null) return false;
        d.NetId = msg.id;
        drops[msg.id] = d;
        return true;
    }

    private static DropMsg SpawnMsg(DroppedItem d)
    {
        Vector2 p = d.transform.position, v = d.Velocity;
        return new DropMsg
        {
            kind = Spawn, id = d.NetId, item = d.Item.name, n = d.Amount,
            x = p.x, y = p.y, vx = v.x, vy = v.y, thrown = d.Thrown,
        };
    }

    private static void ToAll(DropMsg msg, ulong except = ulong.MaxValue)
    {
        if (NetHost.Current == null) return;
        peers.Clear();
        NetHost.Current.CollectReadyPeers(peers);
        foreach (var p in peers)
            if (p.id != except) NetWire.SendJson(NetWire.Drop, p.id, msg);
    }
}
