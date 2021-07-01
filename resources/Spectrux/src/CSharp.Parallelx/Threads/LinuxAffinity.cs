using System;
using System.Collections;
using System.Runtime.InteropServices;

using System.Runtime.InteropServices;
using System;
using System.Collections;
using System.Text;

namespace CSharp.Parallelx.Threads
{
   	public class LinuxAffinity
	{
		internal const string LIBC = "libc.so.6";
		private const long MYTID = 0L; 
		private static int _num_bytes=0;
		
		protected static int num_bytes {
			get {
				if (_num_bytes == 0) {
					_num_bytes = getSystemMaskSize ();
				}
				return _num_bytes;
			}
		}
		
		protected static byte [] toBytes (BitArray cset)
		{
			if (cset.Count > num_bytes)
				throw new ArgumentException ("BitArray too big", "cset");
			byte [] bytes = new byte[num_bytes];
			cset.CopyTo (bytes, 0);
			return bytes;
		}

		[DllImport (LIBC, CallingConvention=CallingConvention.Cdecl,
		            SetLastError=true, EntryPoint="sched_setaffinity")]
		protected static extern int sched_setaffinity (long pid, int num_bytes, byte[] affinity);
		
		private static void validate_tid (long tid)
		{
			if (tid < 0 ) 
				throw new ArgumentException ("Invalid tid");
		}
		
		private static void validate_cpu (int cpu)
		{
			if (cpu < 0 || cpu >= Environment.ProcessorCount)
				throw new ArgumentException ("Invalid cpu");
		}

		public static void SetAffinity (int cpu)
		{
			SetAffinity (MYTID, cpu);
		}
		
		public static void SetAffinity (long tid, int cpu)
		{
			validate_cpu (cpu);

			BitArray cset = new BitArray (Environment.ProcessorCount, false);
			cset[cpu] = true;

			SetAffinity (tid, cset);
		}
		
		public static void AddAffinity (int cpu)
		{
			SetAffinity (MYTID, cpu);
		}
		
		public static void AddAffinity (long tid, int cpu)
		{
			validate_cpu (cpu);

			BitArray cset = GetAffinity (tid);
			cset[cpu] = true;

			SetAffinity (tid, cset);
		}
		
		public static void SetAffinity (BitArray bitmask)
		{
			SetAffinity (MYTID, bitmask);
		}
		
		public static void SetAffinity (long tid, BitArray bitmask)
		{
			// Note: A passed BitArray that is too big, ie has more fields than there
			// are cpus in the system, will cause a throw.  If the passed BitArray is
			// smaller than the number of cpus, then we alter only the ones that are
			// passed.
			validate_tid (tid);
			
			if (bitmask.Count > Environment.ProcessorCount)
				throw new ArgumentException ("BitArray larger than number of CPUs on system", "bitmask");
			
			BitArray ba;
			if (bitmask.Count < Environment.ProcessorCount ) {
				ba = GetAffinity (tid);
				for (int ii=0; ii<bitmask.Count; ii++) ba[ii] = bitmask[ii];
			}
			else {
				ba = bitmask;
			}
		}

		public static void UnSetAffinity (int cpu)
		{
			UnSetAffinity (MYTID, cpu);
		}

		public static void UnSetAffinity(long tid, int cpu)
		{
			validate_tid(tid);
			validate_cpu(cpu);

			BitArray cset = GetAffinity(tid);
			cset[cpu] = false;

		}

		public static void UnAffinitize ()
		{
			UnAffinitize (MYTID);
		}
		
		public static void UnAffinitize (long tid)
		{
			validate_tid (tid);
			
			BitArray cset = new BitArray (Environment.ProcessorCount, true);
		}

		public static bool IsAffinitized (int cpu)
		{
			return (IsAffinitized (MYTID, cpu));
		}
		public static bool IsAffinitized (long tid, int cpu)
		{
			validate_tid (tid);
			validate_cpu (cpu);
			
			BitArray cset = GetAffinity (tid);
			return (cset[cpu]);
		}

		// ------------------------------------------------------------------
		// sched_getaffinity() will return the following 
		//
		//       EFAULT A supplied memory address was invalid.
		//
		//       EINVAL The  affinity  bitmask  mask  contains  
		//	 no  processors  that are physically on the system, or
		//       cpusetsize is smaller than the size of the affinity 
		//	 mask used by the kernel.
		//
		//       EPERM  The  calling  process  does  not  have   appropriate   
		//	 privileges.    The   process   calling sched_setaffinity()  
		//	 needs  an effective user ID equal to the user ID or effective 
		//	 user ID of the process identified by pid, or it must possess 
		//	 the CAP_SYS_NICE capability.
		//
		//       ESRCH  The process whose ID is pid could not be found.
		// ------------------------------------------------------------------
		[DllImport (LIBC, CallingConvention=CallingConvention.Cdecl,
		            SetLastError=true, EntryPoint="sched_getaffinity")]
		public static extern int sched_getaffinity (long pid, int mask_bytes, byte[] affinity);
		
		public static BitArray GetAffinity ()
		{
			return (GetAffinity (MYTID));
		}

		public static BitArray GetAffinity (long tid)
		{
			validate_tid (tid);
			byte [] bytes = new byte[num_bytes];
			
			BitArray cset = new BitArray (bytes);
			cset.Length = Environment.ProcessorCount;  // trim it
			
			return cset;
		}
		
		
		// Method to figure out how big the byte array needs to be is to
		// call the kernel with ever increasing array sizes.  The kernel system
		// call will return the correct number of bytes.  Note: don't use the 
		// libc call since that wrapper will destroy the number returned by the
		// kernel.  The following call will generally figure out the correct size
		// in one system call (with exception for very large machines...)
		[DllImport (LIBC, CallingConvention=CallingConvention.Cdecl, 
		            SetLastError=true, EntryPoint="syscall")]
		private static extern int os_get_cpumask_bytes(int scall, int pid, int numbytes,
		                                               long[] buffer);
		
		protected static int getSystemMaskSize ()
		{
			int cpus=512;
			int n;
			int scall=0;

			switch (LinuxUtil.Machine) {
			case "x86_64":
				scall = 204;
				break;
			case "i386":
				scall = 242;
				break;
			case "ia64":
				scall = 1232;
				break;
			case "powerpc":
				scall = 223;
				break;
			case "s390":
				scall = 240;
				break;
			}
			while (true) {
				long [] buf = new long[cpus];
				n = os_get_cpumask_bytes(scall, 0, (cpus+7)/8, buf);
				int errno = Marshal.GetLastWin32Error ();
				if (n < 0 && cpus < 1024*1024) {
					cpus *= 2;
					continue;
				}
				else if (cpus > 1024*1024) {
					throw new SystemException ("Cannot determine kernel cpu mask size");
				}

				return n;
			}
		}
	}
}