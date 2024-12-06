using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ASE.Utils {
    class SCSMemoryReader {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        // Access rights
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_VM_OPERATION = 0x0008;

        static int GetProcessId(string processName) {
            Process[] processes = Process.GetProcessesByName(processName);
            return processes.Length > 0 ? processes[0].Id : 0;
        }

        static byte[] ReadBuffer(IntPtr processHandle, IntPtr address, int size) {
            byte[] buffer = new byte[size];
            if (ReadProcessMemory(processHandle, address, buffer, (uint)buffer.Length, out _))
                return buffer;
            throw new InvalidOperationException("Failed to read memory.");
        }

        static IntPtr ReadPointer(IntPtr processHandle, IntPtr address) {
            // Throw if the system's not 64-bit. We only support 64-bit processes.
            if (IntPtr.Size != 8)
                throw new NotSupportedException("Only 64-bit systems are supported.");

            byte[] buffer = ReadBuffer(processHandle, address, IntPtr.Size);
            return (IntPtr)BitConverter.ToInt64(buffer, 0); // Assume 64-bit process
        }

        private readonly string ProcessName;
        private readonly int ProcessId;
        private readonly IntPtr ProcessHandle;

        // ProcessName without extension
        public SCSMemoryReader(string processName) {
            ProcessName = processName;
            ProcessId = GetProcessId(processName);
            ProcessHandle = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, ProcessId);
        }

        ~SCSMemoryReader() {
            CloseHandle(ProcessHandle);
        }

        public byte[] Read(IntPtr address, int size) {
            return ReadBuffer(ProcessHandle, address, size);
        }

        public IntPtr GetBaseAddress(string? moduleName) {
            if (moduleName == null) {
                moduleName = ProcessName + ".exe";
            }
            IntPtr baseAddress = GetModuleBaseAddress(ProcessName, moduleName);
            if (baseAddress == IntPtr.Zero)
                throw new InvalidOperationException("Failed to get module base address.");
            return baseAddress;
        }

        static IntPtr GetModuleBaseAddress(string processName, string moduleName) {
            Process[] processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
                return IntPtr.Zero;

            foreach (ProcessModule module in processes[0].Modules) {
                if (module.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
                    return module.BaseAddress;
            }

            return IntPtr.Zero;
        }

        public IntPtr ReadPointer(IntPtr address) {
            return ReadPointer(ProcessHandle, address);
        }

        public byte[] ReadPath(IntPtr address, int[] offsets, int size) {
            for (int i = 0; i < offsets.Length; i++) {
                address = ReadPointer(ProcessHandle, address);
                if (address == IntPtr.Zero)
                    throw new InvalidOperationException("Failed to read memory.");
                address += offsets[i];
            }
            return ReadBuffer(ProcessHandle, address, size);
        }
        public void Write(IntPtr address, byte[] buffer) {
            if (!WriteProcessMemory(ProcessHandle, (IntPtr)address, buffer, (uint)buffer.Length, out _))
                throw new InvalidOperationException($"Failed to write memory. Error: {Marshal.GetLastWin32Error()}");
        }

        public void WritePointer(IntPtr address, IntPtr value) {
            byte[] buffer = BitConverter.GetBytes((long)value);
            Write(address, buffer);
        }

        public void WritePath(IntPtr address, int[] offsets, byte[] buffer) {
            for (int i = 0; i < offsets.Length; i++) {
                address = ReadPointer(ProcessHandle, address);
                if (address == IntPtr.Zero)
                    throw new InvalidOperationException("Failed to read memory.");
                address += offsets[i];
            }
            Write(address, buffer);
        }
    }
}
