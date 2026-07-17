using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MemoryScanner;

internal sealed class ProcessMemoryService : IDisposable
{
    private const int ChunkSize = 1024 * 1024;
    private const int MaxMatches = 250_000_000;
    private nint _handle;

    internal int ProcessId { get; private set; }
    internal bool IsAttached => _handle != 0;

    internal void Attach(int processId)
    {
        Detach();
        var access = NativeMethods.ProcessAccessFlags.QueryInformation |
                     NativeMethods.ProcessAccessFlags.VirtualMemoryRead |
                     NativeMethods.ProcessAccessFlags.VirtualMemoryWrite |
                     NativeMethods.ProcessAccessFlags.VirtualMemoryOperation;

        _handle = NativeMethods.OpenProcess(access, false, processId);
        if (_handle == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the selected process.");

        ProcessId = processId;
    }

    internal List<long> FirstScan(byte[] target, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        EnsureAttached();
        var matches = new List<long>();
        var regions = new List<(long BaseAddress, long Size)>();
        var address = 0L;
        var infoSize = (nuint)Marshal.SizeOf<NativeMethods.MemoryBasicInformation>();

        while (address >= 0 && NativeMethods.VirtualQueryEx(_handle, (nint)address, out var info, infoSize) != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var baseAddress = info.BaseAddress.ToInt64();
            var regionSize = checked((long)info.RegionSize);

            if (IsReadable(info))
                regions.Add((baseAddress, regionSize));

            var nextAddress = baseAddress + regionSize;
            if (nextAddress <= address)
                break;
            address = nextAddress;
        }

        var totalBytes = regions.Sum(region => region.Size);
        long scannedBytes = 0;
        var reportTimer = Stopwatch.StartNew();
        progress?.Report(new ScanProgress(0, 0));

        foreach (var region in regions)
        {
            ScanRegion(region.BaseAddress, region.Size, target, matches, cancellationToken, bytesProcessed =>
            {
                scannedBytes += bytesProcessed;
                if (reportTimer.ElapsedMilliseconds >= 100)
                {
                    progress?.Report(new ScanProgress(matches.Count, GetPercentage(scannedBytes, totalBytes)));
                    reportTimer.Restart();
                }
            });

            if (matches.Count >= MaxMatches)
                break;
        }

        progress?.Report(new ScanProgress(matches.Count, GetPercentage(scannedBytes, totalBytes)));

        return matches;
    }

    internal List<long> NextScan(IEnumerable<long> candidates, byte[] target, CancellationToken cancellationToken)
    {
        EnsureAttached();
        var matches = new List<long>();
        foreach (var address in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryRead(address, target.Length, out var bytes) && bytes.AsSpan().SequenceEqual(target))
                matches.Add(address);
        }
        return matches;
    }

    internal bool TryRead(long address, int size, out byte[] bytes)
    {
        EnsureAttached();
        bytes = new byte[size];
        return NativeMethods.ReadProcessMemory(_handle, (nint)address, bytes, (nuint)size, out var read) && read == (nuint)size;
    }

    internal void Write(long address, byte[] bytes)
    {
        EnsureAttached();
        if (!NativeMethods.WriteProcessMemory(_handle, (nint)address, bytes, (nuint)bytes.Length, out var written) || written != (nuint)bytes.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not write to address 0x{address:X}.");
    }

    private void ScanRegion(long baseAddress, long regionSize, byte[] target, List<long> matches, CancellationToken token, Action<int> reportBytesProcessed)
    {
        var overlap = Math.Max(0, target.Length - 1);
        for (long offset = 0; offset < regionSize; offset += ChunkSize - overlap)
        {
            token.ThrowIfCancellationRequested();
            var requested = (int)Math.Min(ChunkSize, regionSize - offset);
            var newlyCovered = (int)Math.Min(ChunkSize - overlap, regionSize - offset);
            var buffer = new byte[requested];
            if (!NativeMethods.ReadProcessMemory(_handle, (nint)(baseAddress + offset), buffer, (nuint)requested, out var bytesRead))
            {
                reportBytesProcessed(newlyCovered);
                continue;
            }

            var usable = checked((int)bytesRead);
            for (var index = 0; index <= usable - target.Length; index++)
            {
                if (buffer.AsSpan(index, target.Length).SequenceEqual(target))
                {
                    matches.Add(baseAddress + offset + index);
                    if (matches.Count >= MaxMatches)
                    {
                        reportBytesProcessed(newlyCovered);
                        return;
                    }
                }
            }
            reportBytesProcessed(newlyCovered);
        }
    }

    private static double GetPercentage(long completed, long total) =>
        total == 0 ? 100 : Math.Min(100, completed * 100d / total);

    private static bool IsReadable(NativeMethods.MemoryBasicInformation info)
    {
        const NativeMethods.MemoryProtection readable =
            NativeMethods.MemoryProtection.ReadOnly |
            NativeMethods.MemoryProtection.ReadWrite |
            NativeMethods.MemoryProtection.WriteCopy |
            NativeMethods.MemoryProtection.ExecuteRead |
            NativeMethods.MemoryProtection.ExecuteReadWrite |
            NativeMethods.MemoryProtection.ExecuteWriteCopy;

        return info.State == NativeMethods.MemoryState.Commit &&
               (info.Protect & readable) != 0 &&
               (info.Protect & (NativeMethods.MemoryProtection.Guard | NativeMethods.MemoryProtection.NoAccess)) == 0;
    }

    private void EnsureAttached()
    {
        if (!IsAttached)
            throw new InvalidOperationException("Attach to a process first.");
    }

    private void Detach()
    {
        if (_handle != 0)
            NativeMethods.CloseHandle(_handle);
        _handle = 0;
        ProcessId = 0;
    }

    public void Dispose()
    {
        Detach();
        GC.SuppressFinalize(this);
    }
}
