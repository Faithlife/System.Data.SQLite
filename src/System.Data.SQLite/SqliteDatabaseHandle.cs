#if !PORTABLE
using Microsoft.Win32.SafeHandles;
#else
using System.Runtime.InteropServices;
#endif

namespace System.Data.SQLite
{
	internal sealed class SqliteDatabaseHandle
#if PORTABLE
		: CriticalHandle
#else
		: SafeHandleZeroOrMinusOneIsInvalid
#endif
	{
		public SqliteDatabaseHandle()
#if PORTABLE
			: base((IntPtr) 0)
#else
			: base(ownsHandle: true)
#endif
		{
		}

		public SqliteDatabaseHandle(IntPtr handle)
#if PORTABLE
			: base((IntPtr) 0)
#else
			: base(ownsHandle: true)
#endif
		{
			SetHandle(handle);
		}

#if PORTABLE
		public override bool IsInvalid
		{
			get
			{
				return handle == new IntPtr(-1) || handle == (IntPtr) 0;
			}
		}
#endif

		protected override bool ReleaseHandle()
		{
#if NET45
			return NativeMethods.sqlite3_close_v2(handle) == SQLiteErrorCode.Ok;
#else
			return NativeMethods.sqlite3_close(handle) == SQLiteErrorCode.Ok;
#endif
		}
	}
}
