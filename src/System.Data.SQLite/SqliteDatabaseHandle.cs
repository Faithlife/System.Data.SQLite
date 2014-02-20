using Microsoft.Win32.SafeHandles;

namespace System.Data.SQLite
{
	internal sealed class SqliteDatabaseHandle : CriticalHandleZeroOrMinusOneIsInvalid
	{
		public SqliteDatabaseHandle()
		{
		}

		public SqliteDatabaseHandle(IntPtr handle)
		{
			SetHandle(handle);
		}

		protected override bool ReleaseHandle()
		{
			return NativeMethods.sqlite3_close_v2(handle) == SQLiteErrorCode.Ok;
		}
	}
}
