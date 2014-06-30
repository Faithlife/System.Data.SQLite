using Microsoft.Win32.SafeHandles;

namespace System.Data.SQLite
{
	internal sealed class SqliteBackupHandle : CriticalHandleZeroOrMinusOneIsInvalid
	{
		public SqliteBackupHandle()
		{
		}

		protected override bool ReleaseHandle()
		{
			return NativeMethods.sqlite3_backup_finish(handle) == SQLiteErrorCode.Ok;
		}
	}
}
