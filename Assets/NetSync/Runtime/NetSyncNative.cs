using System;
using System.Runtime.InteropServices;

namespace NetSync.Unity
{
    public enum NetSyncStatus
    {
        Ok = 0,
        Error = -1,
        InvalidArgument = -2,
        BufferTooSmall = -3,
        End = 1,
    }

    public enum NetSyncPacketType : byte
    {
        Snapshot = 1,
        Delta = 2,
        Commands = 3,
    }

    public enum NetSyncEventType : byte
    {
        Spawn = 1,
        Despawn = 2,
        Cell = 3,
        StatI32 = 4,
        Flags = 5,
        Bytes = 6,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NetSyncEvent
    {
        public byte type;
        public uint entityId;
        public ushort kind;
        public int x;
        public int y;
        public int z;
        public int value;
        public uint byteSize;

        public NetSyncEventType Type => (NetSyncEventType)type;
    }

    public sealed class NetSyncEncoder : IDisposable
    {
        private const string Library = "netsync_unity";
        private IntPtr handle;
        private byte[] packetBuffer;

        public NetSyncEncoder(int initialCapacity = 1200)
        {
            handle = netsync_unity_encoder_create((UIntPtr)Math.Max(initialCapacity, 0));
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException("netsync_unity_encoder_create failed");
            packetBuffer = new byte[Math.Max(initialCapacity, 1200)];
        }

        public void Begin(NetSyncPacketType packetType, uint tick, uint sequence)
        {
            Check(netsync_unity_encoder_begin(handle, (byte)packetType, tick, sequence));
        }

        public void Spawn(uint entityId, ushort prefabId, int x, int y, int z)
        {
            Check(netsync_unity_encoder_spawn(handle, entityId, prefabId, x, y, z));
        }

        public void Despawn(uint entityId)
        {
            Check(netsync_unity_encoder_despawn(handle, entityId));
        }

        public void Cell(uint entityId, int x, int y, int z)
        {
            Check(netsync_unity_encoder_cell(handle, entityId, x, y, z));
        }

        public void StatI32(uint entityId, ushort statId, int value)
        {
            Check(netsync_unity_encoder_stat_i32(handle, entityId, statId, value));
        }

        public void Flags(uint entityId, uint flags)
        {
            Check(netsync_unity_encoder_flags(handle, entityId, flags));
        }

        public void Bytes(uint entityId, ushort channel, byte[] data, int count)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if ((uint)count > (uint)data.Length) throw new ArgumentOutOfRangeException(nameof(count));
            Check(netsync_unity_encoder_bytes(handle, entityId, channel, data, (UIntPtr)count));
        }

        public ArraySegment<byte> ToArraySegment()
        {
            Check(netsync_unity_encoder_size(handle, out var nativeSize));
            int size = checked((int)nativeSize);
            if (packetBuffer.Length < size)
                Array.Resize(ref packetBuffer, size);
            Check(netsync_unity_encoder_copy(handle, packetBuffer, (UIntPtr)packetBuffer.Length, out _));
            return new ArraySegment<byte>(packetBuffer, 0, size);
        }

        public byte[] ToArray()
        {
            var segment = ToArraySegment();
            var copy = new byte[segment.Count];
            Buffer.BlockCopy(segment.Array, segment.Offset, copy, 0, segment.Count);
            return copy;
        }

        public void Dispose()
        {
            var h = handle;
            handle = IntPtr.Zero;
            if (h != IntPtr.Zero)
                netsync_unity_encoder_destroy(h);
            GC.SuppressFinalize(this);
        }

        ~NetSyncEncoder()
        {
            Dispose();
        }

        private static void Check(int status)
        {
            if (status < 0)
                throw new InvalidOperationException($"NetSync encoder error: {(NetSyncStatus)status}");
        }

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr netsync_unity_encoder_create(UIntPtr initialCapacity);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern void netsync_unity_encoder_destroy(IntPtr encoder);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int netsync_unity_encoder_begin(IntPtr encoder, byte packetType, uint tick, uint sequence);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int netsync_unity_encoder_spawn(IntPtr encoder, uint entityId, ushort prefabId, int x, int y, int z);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int netsync_unity_encoder_despawn(IntPtr encoder, uint entityId);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int netsync_unity_encoder_cell(IntPtr encoder, uint entityId, int x, int y, int z);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int netsync_unity_encoder_stat_i32(IntPtr encoder, uint entityId, ushort statId, int value);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int netsync_unity_encoder_flags(IntPtr encoder, uint entityId, uint flags);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int netsync_unity_encoder_bytes(IntPtr encoder, uint entityId, ushort channel, byte[] data, UIntPtr size);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int netsync_unity_encoder_size(IntPtr encoder, out UIntPtr size);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int netsync_unity_encoder_copy(IntPtr encoder, byte[] data, UIntPtr capacity, out UIntPtr size);
    }

    public sealed class NetSyncDecoder : IDisposable
    {
        private const string Library = "netsync_unity";
        private IntPtr handle;
        private byte[] bytesBuffer = new byte[256];

        public NetSyncDecoder()
        {
            handle = netsync_unity_decoder_create();
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException("netsync_unity_decoder_create failed");
        }

        public NetSyncPacketType PacketType
        {
            get
            {
                Check(netsync_unity_decoder_packet_type(handle, out var packetType));
                return (NetSyncPacketType)packetType;
            }
        }

        public uint Tick
        {
            get
            {
                Check(netsync_unity_decoder_tick(handle, out var tick));
                return tick;
            }
        }

        public uint Sequence
        {
            get
            {
                Check(netsync_unity_decoder_sequence(handle, out var sequence));
                return sequence;
            }
        }

        public void Begin(byte[] packet, int count)
        {
            if (packet == null) throw new ArgumentNullException(nameof(packet));
            if ((uint)count > (uint)packet.Length) throw new ArgumentOutOfRangeException(nameof(count));
            Check(netsync_unity_decoder_begin(handle, packet, (UIntPtr)count));
        }

        public bool Next(out NetSyncEvent evt)
        {
            int status = netsync_unity_decoder_next(handle, out evt);
            if (status == (int)NetSyncStatus.End) return false;
            Check(status);
            return true;
        }

        public ArraySegment<byte> CopyBytes()
        {
            int status = netsync_unity_decoder_copy_bytes(handle, bytesBuffer, (UIntPtr)bytesBuffer.Length, out var nativeSize);
            int size = checked((int)nativeSize);
            if (status == (int)NetSyncStatus.BufferTooSmall)
            {
                Array.Resize(ref bytesBuffer, size);
                Check(netsync_unity_decoder_copy_bytes(handle, bytesBuffer, (UIntPtr)bytesBuffer.Length, out nativeSize));
                size = checked((int)nativeSize);
            }
            else
            {
                Check(status);
            }
            return new ArraySegment<byte>(bytesBuffer, 0, size);
        }

        public void Dispose()
        {
            var h = handle;
            handle = IntPtr.Zero;
            if (h != IntPtr.Zero)
                netsync_unity_decoder_destroy(h);
            GC.SuppressFinalize(this);
        }

        ~NetSyncDecoder()
        {
            Dispose();
        }

        private static void Check(int status)
        {
            if (status < 0)
                throw new InvalidOperationException($"NetSync decoder error: {(NetSyncStatus)status}");
        }

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr netsync_unity_decoder_create();

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern void netsync_unity_decoder_destroy(IntPtr decoder);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int netsync_unity_decoder_begin(IntPtr decoder, byte[] data, UIntPtr size);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int netsync_unity_decoder_packet_type(IntPtr decoder, out byte packetType);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int netsync_unity_decoder_tick(IntPtr decoder, out uint tick);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int netsync_unity_decoder_sequence(IntPtr decoder, out uint sequence);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int netsync_unity_decoder_next(IntPtr decoder, out NetSyncEvent evt);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int netsync_unity_decoder_copy_bytes(IntPtr decoder, byte[] data, UIntPtr capacity, out UIntPtr size);
    }
}
