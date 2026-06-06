using System.Runtime.InteropServices;

namespace WGS.Services;

/// <summary>
/// Hakee TCP-yhteyksien PID-omistajuustiedot iphlpapi.dll:stä.
/// Ei vaadi korotettuja oikeuksia (admin), toimii Windows 7+.
/// </summary>
public static class ProcessNetworkTracker
{
    // TCP_TABLE_OWNER_PID_ALL = 5
    private const int AfInet       = 2;
    private const int TableOwnerPidAll = 5;

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable, ref int pdwSize, bool bOrder,
        int ulAf, int tableClass, uint reserved);

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public uint State;
        public uint LocalAddr;
        public uint LocalPort;   // network byte order → big-endian
        public uint RemoteAddr;
        public uint RemotePort;
        public uint OwningPid;
    }

    /// <summary>
    /// Palauttaa TCP-yhteyksien lukumäärän per PID.
    /// Kutsutaan taustaketjulta — ei UI-threadia.
    /// </summary>
    public static Dictionary<int, int> GetConnectionCountsByPid()
    {
        var result = new Dictionary<int, int>();

        int bufSize = 0;
        GetExtendedTcpTable(IntPtr.Zero, ref bufSize, false, AfInet, TableOwnerPidAll, 0);

        var buf = Marshal.AllocHGlobal(bufSize);
        try
        {
            uint ret = GetExtendedTcpTable(buf, ref bufSize, false, AfInet, TableOwnerPidAll, 0);
            if (ret != 0) return result;

            int rowCount = Marshal.ReadInt32(buf);
            int rowSize  = Marshal.SizeOf<MibTcpRowOwnerPid>();
            int offset   = 4; // skip dwNumEntries

            for (int i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(buf + offset);
                offset += rowSize;

                int pid = (int)row.OwningPid;
                result[pid] = result.TryGetValue(pid, out int c) ? c + 1 : 1;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }

        return result;
    }

    /// <summary>
    /// Palauttaa prosessin kuuntelevat portit (LISTEN-tila = 2).
    /// </summary>
    public static List<int> GetListeningPortsForPid(int pid)
    {
        var ports = new List<int>();

        int bufSize = 0;
        GetExtendedTcpTable(IntPtr.Zero, ref bufSize, false, AfInet, TableOwnerPidAll, 0);

        var buf = Marshal.AllocHGlobal(bufSize);
        try
        {
            uint ret = GetExtendedTcpTable(buf, ref bufSize, false, AfInet, TableOwnerPidAll, 0);
            if (ret != 0) return ports;

            int rowCount = Marshal.ReadInt32(buf);
            int rowSize  = Marshal.SizeOf<MibTcpRowOwnerPid>();
            int offset   = 4;

            for (int i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(buf + offset);
                offset += rowSize;

                // State 2 = MIB_TCP_STATE_LISTEN
                if ((int)row.OwningPid == pid && row.State == 2)
                    ports.Add((int)(((row.LocalPort & 0xFF) << 8) | (row.LocalPort >> 8 & 0xFF)));
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }

        return ports;
    }
}
