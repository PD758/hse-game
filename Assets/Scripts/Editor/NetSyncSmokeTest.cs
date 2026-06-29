#if UNITY_EDITOR
using NetSync.Unity;
using UnityEditor;
using UnityEngine;

public static class NetSyncSmokeTest
{
    [MenuItem("Rogue/Run NetSync Smoke Test")]
    public static void Run()
    {
        using var encoder = new NetSyncEncoder();
        encoder.Begin(NetSyncPacketType.Snapshot, 12, 34);
        encoder.Spawn(1, 2, 3, 4, 0);
        encoder.Cell(1, 5, 6, 0);
        encoder.StatI32(1, 1, 10);

        byte[] packet = encoder.ToArray();

        using var decoder = new NetSyncDecoder();
        decoder.Begin(packet, packet.Length);

        int events = 0;
        while (decoder.Next(out _))
            events++;

        if (decoder.PacketType != NetSyncPacketType.Snapshot || decoder.Tick != 12 || decoder.Sequence != 34 || events != 3)
            throw new System.InvalidOperationException("NetSync smoke test decoded unexpected packet data.");

        Debug.Log($"NetSync smoke test passed with {events} events and {packet.Length} bytes.");
    }
}
#endif
