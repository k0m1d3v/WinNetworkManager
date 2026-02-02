using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;

class NetworkConnections
{
    public enum TcpTableClass
    {
        TCP_TABLE_OWNER_PID_ALL = 5
    }

    public enum UdpTableClass
    {
        UDP_TABLE_OWNER_PID = 1
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable,
        ref int dwOutBufLen,
        bool sort,
        int ipVersion,
        TcpTableClass tblClass,
        uint reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    static extern uint GetExtendedUdpTable(
        IntPtr pUdpTable,
        ref int dwOutBufLen,
        bool sort,
        int ipVersion,
        UdpTableClass tblClass,
        uint reserved);

    [StructLayout(LayoutKind.Sequential)]
    struct MIB_TCPROW_OWNER_PID
    {
        public uint state;
        public uint localAddr;
        public uint localPort;
        public uint remoteAddr;
        public uint remotePort;
        public uint owningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MIB_UDPROW_OWNER_PID
    {
        public uint localAddr;
        public uint localPort;
        public uint owningPid;
    }

    // ---------------- TCP ----------------
    public static IEnumerable<(int Pid, IPAddress RemoteIp, int RemotePort, string Proto)> GetTcpConnections()
    {
        int bufferSize = 0;
        GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, 2, TcpTableClass.TCP_TABLE_OWNER_PID_ALL, 0);

        IntPtr buffer = Marshal.AllocHGlobal(bufferSize);

        try
        {
            uint result = GetExtendedTcpTable(buffer, ref bufferSize, true, 2, TcpTableClass.TCP_TABLE_OWNER_PID_ALL, 0);
            if (result != 0)
                throw new Exception("GetExtendedTcpTable failed: " + result);

            int rowCount = Marshal.ReadInt32(buffer);
            IntPtr rowPtr = buffer + 4;

            var list = new List<(int, IPAddress, int, string)>();

            for (int i = 0; i < rowCount; i++)
            {
                try
                {
                    var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);

                    IPAddress remoteIp = new IPAddress(row.remoteAddr);
                    int remotePort = (int)IPAddress.NetworkToHostOrder((short)row.remotePort);

                    list.Add(((int)row.owningPid, remoteIp, remotePort, "TCP"));

                    rowPtr += Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
                }
                catch
                {

                }
            }

            return list;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // ---------------- UDP ----------------
    public static IEnumerable<(int Pid, IPAddress LocalIp, int LocalPort, string Proto)> GetUdpConnections()
    {
        int bufferSize = 0;
        GetExtendedUdpTable(IntPtr.Zero, ref bufferSize, true, 2, UdpTableClass.UDP_TABLE_OWNER_PID, 0);

        IntPtr buffer = Marshal.AllocHGlobal(bufferSize);

        try
        {
            uint result = GetExtendedUdpTable(buffer, ref bufferSize, true, 2, UdpTableClass.UDP_TABLE_OWNER_PID, 0);
            if (result != 0)
                throw new Exception("GetExtendedUdpTable failed: " + result);

            int rowCount = Marshal.ReadInt32(buffer);
            IntPtr rowPtr = buffer + 4;

            var list = new List<(int, IPAddress, int, string)>();

            for (int i = 0; i < rowCount; i++)
            {
                try
                {
                    var row = Marshal.PtrToStructure<MIB_UDPROW_OWNER_PID>(rowPtr);

                    IPAddress localIp = new IPAddress(row.localAddr);
                    int localPort = (int)IPAddress.NetworkToHostOrder((short)row.localPort);

                    list.Add(((int)row.owningPid, localIp, localPort, "UDP"));

                    rowPtr += Marshal.SizeOf<MIB_UDPROW_OWNER_PID>();
                }
                catch
                {

                }
            }

            return list;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // ---------------- All connections ----------------
    public static IEnumerable<(int Pid, IPAddress LocalIp, int LocalPort, string Proto)> GetAllConnections()
    {
        var all = new List<(int Pid, IPAddress LocalIp, int LocalPort, string Proto)>();

        all.AddRange(GetTcpConnections());
        all.AddRange(GetUdpConnections());

        return all;
    }
}