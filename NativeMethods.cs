using System.Runtime.InteropServices;

namespace MemoryScanner;

internal static class NativeMethods
{
    [Flags]
    internal enum ProcessAccessFlags : uint
    {
        VirtualMemoryOperation = 0x0008,
        VirtualMemoryRead = 0x0010,
        VirtualMemoryWrite = 0x0020,
        QueryInformation = 0x0400,
    }

    internal enum MemoryState : uint
    {
        Commit = 0x1000,
    }

    [Flags]
    internal enum MemoryProtection : uint
    {
        NoAccess = 0x01,
        ReadOnly = 0x02,
        ReadWrite = 0x04,
        WriteCopy = 0x08,
        Execute = 0x10,
        ExecuteRead = 0x20,
        ExecuteReadWrite = 0x40,
        ExecuteWriteCopy = 0x80,
        Guard = 0x100,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MemoryBasicInformation
    {
        internal nint BaseAddress;
        internal nint AllocationBase;
        internal MemoryProtection AllocationProtect;
        internal nuint RegionSize;
        internal MemoryState State;
        internal MemoryProtection Protect;
        internal uint Type;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern nint OpenProcess(
        ProcessAccessFlags processAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReadProcessMemory(
        nint processHandle,
        nint baseAddress,
        [Out] byte[] buffer,
        nuint size,
        out nuint bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WriteProcessMemory(
        nint processHandle,
        nint baseAddress,
        byte[] buffer,
        nuint size,
        out nuint bytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern nuint VirtualQueryEx(
        nint processHandle,
        nint address,
        out MemoryBasicInformation buffer,
        nuint length);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(nint handle);
}
