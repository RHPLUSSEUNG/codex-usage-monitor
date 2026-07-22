using System.Runtime.InteropServices;
using CodexUsageMonitor.Models;

namespace CodexUsageMonitor.Services;

public sealed class SystemUsageMonitor
{
    private ulong? _previousIdle;
    private ulong? _previousKernel;
    private ulong? _previousUser;

    public SystemUsageSnapshot Sample()
    {
        double? cpu = SampleCpu();
        double? memory = SampleMemory();
        return new SystemUsageSnapshot(cpu, memory);
    }

    private double? SampleCpu()
    {
        if (!GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime))
            return null;

        ulong idle = idleTime.ToUInt64();
        ulong kernel = kernelTime.ToUInt64();
        ulong user = userTime.ToUInt64();
        double? result = null;

        if (_previousIdle is not null && _previousKernel is not null && _previousUser is not null)
        {
            ulong idleDelta = idle - _previousIdle.Value;
            ulong totalDelta = (kernel - _previousKernel.Value) + (user - _previousUser.Value);
            if (totalDelta > 0 && totalDelta >= idleDelta)
                result = Math.Clamp((totalDelta - idleDelta) * 100d / totalDelta, 0d, 100d);
        }

        _previousIdle = idle;
        _previousKernel = kernel;
        _previousUser = user;
        return result;
    }

    private static double? SampleMemory()
    {
        var status = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhysical == 0)
            return null;

        return Math.Clamp(
            (status.TotalPhysical - status.AvailablePhysical) * 100d / status.TotalPhysical,
            0d,
            100d);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out FileTime idleTime,
        out FileTime kernelTime,
        out FileTime userTime);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;

        public readonly ulong ToUInt64() => ((ulong)HighDateTime << 32) | LowDateTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;

        public MemoryStatusEx()
        {
            this = default;
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        }
    }
}
