using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
// using Mono.Unix.Native;

namespace CSharp.Parallelx.Threads
{

    public class LinuxUtil
    {
        private const string LIBC = "libc.so.6";
        private static string _machine = String.Empty;

        [DllImport(LIBC, CallingConvention = CallingConvention.Cdecl,
            SetLastError = true, EntryPoint = "syscall")]
        private static extern int os_syscall_gettid(int num);

        public static int Tid
        {
            get
            {
                // syscall for gettid is:
                //   arch     scall#   machine_str
                //   ------   ------   -----------
                //   x86_64   186      x86_64
                //   i.86     224      i386
                //   ia64     1105     ia64
                //   s390[x]  236      s390
                //   ppc.*    207      powerpc

                switch (Machine)
                {
                    case "x86_64":
                        return os_syscall_gettid(186);
                    case "i386":
                        return os_syscall_gettid(224);
                    case "ia64":
                        return os_syscall_gettid(1105);
                    case "powerpc":
                        return os_syscall_gettid(207);
                    case "s390":
                        return os_syscall_gettid(236);
                }

                // return default id if syscall does not exist
                return Thread.CurrentThread.ManagedThreadId;
            }
        }
        
        // System.Environment.Is64BitOperatingSystem;;

        public static string Machine
        {
            get
            {
                if (_machine == String.Empty)
                {
//                    Utsname uts = new Utsname();
//                    int ret = Syscall.uname(out uts);
//
//                    if (ret != 0)
//                        _machine = "unknown";
//                    else
//                        _machine = uts.machine;
                }

                return _machine;
            }
        }
        
        [DllImport("kernel32.dll")]
        private static extern void GetNativeSystemInfo(ref SYSTEM_INFO lpSystemInfo);

        private const int PROCESSOR_ARCHITECTURE_AMD64 = 9;
        private const int PROCESSOR_ARCHITECTURE_IA64 = 6;
        private const int PROCESSOR_ARCHITECTURE_INTEL = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_INFO
        {
            public short wProcessorArchitecture;
            public short wReserved;
            public int dwPageSize;
            public IntPtr lpMinimumApplicationAddress;
            public IntPtr lpMaximumApplicationAddress;
            public IntPtr dwActiveProcessorMask;
            public int dwNumberOfProcessors;
            public int dwProcessorType;
            public int dwAllocationGranularity;
            public short wProcessorLevel;
            public short wProcessorRevision;
        }
        
        public static ProcessorArchitecture GetProcessorArchitecture()
        {
            SYSTEM_INFO si = new SYSTEM_INFO();
            GetNativeSystemInfo(ref si);
            switch (si.wProcessorArchitecture)
            {
                case PROCESSOR_ARCHITECTURE_AMD64:
                    return ProcessorArchitecture.Amd64;

                case PROCESSOR_ARCHITECTURE_IA64:
                    return ProcessorArchitecture.IA64;

                case PROCESSOR_ARCHITECTURE_INTEL:
                    return ProcessorArchitecture.X86;

                default:
                    return ProcessorArchitecture.None; // that's weird :-)
            }
        }
    }
}