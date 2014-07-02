using Microsoft.Win32.SafeHandles;

namespace System.Data.SQLite
{
	internal sealed class SqliteBackupHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		public SqliteBackupHandle()
			: base(ownsHandle: true)
		{
		}

		protected override bool ReleaseHandle()
		{
			return NativeMethods.sqlite3_backup_finish(handle) == SQLiteErrorCode.Ok;
		}
	}
}
