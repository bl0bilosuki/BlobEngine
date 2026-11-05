using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BlobEngine.Memory;

[Flags]
public enum ProcessAccess : uint { VmOperation = 0x0008, VmRead = 0x0010, VmWrite = 0x0020, QueryInformation = 0x0400 }

public sealed class ProcessMemory : IDisposable
{
    private IntPtr _handle;
    public int ProcessId { get; }
    public IntPtr Handle => _handle;
    public bool IsAttached => _handle != IntPtr.Zero;

    private ProcessMemory(int pid, IntPtr handle) { ProcessId = pid; _handle = handle; }

    public static ProcessMemory Attach(int pid, bool allowWrite = true)
    {
        if (pid <= 0) throw new ArgumentOutOfRangeException(nameof(pid));
        using var process = Process.GetProcessById(pid);
        if (process.HasExited) throw new InvalidOperationException("The target process has exited.");
        var access = ProcessAccess.QueryInformation | ProcessAccess.VmRead |
                     (allowWrite ? ProcessAccess.VmWrite | ProcessAccess.VmOperation : 0);
        var handle = OpenProcess((uint)access, false, pid);
        if (handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenProcess failed.");
        return new ProcessMemory(pid, handle);
    }

    public bool TryRead(long address, byte[] buffer, out int bytesRead)
    {
        EnsureAttached();
        if (address < 0) throw new ArgumentOutOfRangeException(nameof(address));
        var ok = ReadProcessMemory(_handle, new IntPtr(address), buffer, (nuint)buffer.Length, out var read);
        bytesRead = checked((int)Math.Min((ulong)buffer.Length, read.ToUInt64()));
        return ok && bytesRead > 0;
    }

    public void Write(long address, byte[] data)
    {
        EnsureAttached();
        if (address < 0) throw new ArgumentOutOfRangeException(nameof(address));
        if (!WriteProcessMemory(_handle, new IntPtr(address), data, (nuint)data.Length, out var written) || written.ToUInt64() != (ulong)data.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "WriteProcessMemory failed or wrote fewer bytes than requested.");
    }

    public IEnumerable<MemoryRegion> EnumerateReadableRegions(CancellationToken cancellationToken = default)
    {
        EnsureAttached();
        ulong address = 0;
        var size = (nuint)Marshal.SizeOf<MemoryBasicInformation>();
        while (!cancellationToken.IsCancellationRequested)
        {
            var result = VirtualQueryEx(_handle, new IntPtr(unchecked((long)address)), out var info, size);
            if (result == 0) yield break;
            var start = unchecked((ulong)info.BaseAddress.ToInt64());
            var length = info.RegionSize.ToUInt64();
            var next = start + length;
            if (next <= address) yield break;
            address = next;
            if (length == 0 || info.State != 0x1000 || (info.Protect & (0x100 | 0x01)) != 0) continue;
            var protection = info.Protect & 0xFF;
            if (protection is 0x02 or 0x04 or 0x08 or 0x20 or 0x40 or 0x80)
                yield return new MemoryRegion(unchecked((long)start), length);
        }
    }

    private void EnsureAttached()
    {
        if (_handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(ProcessMemory));
    }

    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle != IntPtr.Zero) CloseHandle(handle);
        GC.SuppressFinalize(this);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryBasicInformation
    {
        public IntPtr BaseAddress; public IntPtr AllocationBase; public uint AllocationProtect;
        public ushort PartitionId; public UIntPtr RegionSize; public uint State; public uint Protect; public uint Type;
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nuint VirtualQueryEx(IntPtr process, IntPtr address, out MemoryBasicInformation info, nuint length);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadProcessMemory(IntPtr process, IntPtr address, [Out] byte[] buffer, nuint size, out nuint read);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] buffer, nuint size, out nuint written);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);
}

public sealed record MemoryRegion(long BaseAddress, ulong Size);
