using Microsoft.Win32.SafeHandles;

namespace System.Data.SQLite
{
	internal sealed class SqliteDatabaseHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		public SqliteDatabaseHandle()
			: base(ownsHandle: true)
		{
		}

		public SqliteDatabaseHandle(IntPtr handle)
			: base(ownsHandle: true)
		{
			SetHandle(handle);
		}

		protected override bool ReleaseHandle() => NativeMethods.sqlite3_close_v2(handle) == SQLiteErrorCode.Ok;
	}
}
