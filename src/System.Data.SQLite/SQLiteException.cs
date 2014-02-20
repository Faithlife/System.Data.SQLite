using System.Data.Common;

namespace System.Data.SQLite
{
	public sealed class SQLiteException : DbException
	{
		public SQLiteException(SQLiteErrorCode errorCode)
			: base(GetErrorString(errorCode), (int) errorCode)
		{
		}

		private static string GetErrorString(SQLiteErrorCode errorCode)
		{
			return SQLiteConnection.FromUtf8(NativeMethods.sqlite3_errstr(errorCode));
		}
	}
}
