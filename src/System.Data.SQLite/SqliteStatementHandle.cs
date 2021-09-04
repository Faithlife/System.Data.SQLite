using Microsoft.Win32.SafeHandles;

namespace System.Data.SQLite
{
	internal sealed class SqliteStatementHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		public SqliteStatementHandle()
			: base(ownsHandle: true)
		{
		}

		protected override bool ReleaseHandle() => NativeMethods.sqlite3_finalize(handle) == SQLiteErrorCode.Ok;
	}
}
