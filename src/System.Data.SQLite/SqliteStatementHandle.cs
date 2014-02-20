using Microsoft.Win32.SafeHandles;

namespace System.Data.SQLite
{
	internal sealed class SqliteStatementHandle : CriticalHandleZeroOrMinusOneIsInvalid
	{
		public SqliteStatementHandle()
		{
		}

		protected override bool ReleaseHandle()
		{
			return NativeMethods.sqlite3_finalize(handle) == SQLiteErrorCode.Ok;
		}
	}
}
