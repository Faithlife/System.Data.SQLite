#if !PORTABLE
using Microsoft.Win32.SafeHandles;
#else
using System.Runtime.InteropServices;
#endif

namespace System.Data.SQLite
{
	internal sealed class SqliteBackupHandle
#if PORTABLE
		: CriticalHandle
#else
		: SafeHandleZeroOrMinusOneIsInvalid
#endif
	{
		public SqliteBackupHandle()
#if PORTABLE
			: base((IntPtr) 0)
#else
			: base(ownsHandle: true)
#endif
		{
		}

#if PORTABLE
		public override bool IsInvalid => handle == new IntPtr(-1) || handle == (IntPtr) 0;
#endif

		protected override bool ReleaseHandle() => NativeMethods.sqlite3_backup_finish(handle) == SQLiteErrorCode.Ok;
	}
}
