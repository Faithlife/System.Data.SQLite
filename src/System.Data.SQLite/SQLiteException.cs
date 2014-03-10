using System.Data.Common;

namespace System.Data.SQLite
{
	public sealed class SQLiteException : DbException
	{
		public SQLiteException(SQLiteErrorCode errorCode) : this(errorCode, null)
		{
		}

		internal SQLiteException(SQLiteErrorCode errorCode, SqliteDatabaseHandle database)
			: base(GetErrorString(errorCode, database), (int) errorCode)
		{
		}

		private static string GetErrorString(SQLiteErrorCode errorCode, SqliteDatabaseHandle database)
		{
			return database != null ? "{0}: {1}".FormatInvariant(SQLiteConnection.FromUtf8(NativeMethods.sqlite3_errstr(errorCode)), SQLiteConnection.FromUtf8(NativeMethods.sqlite3_errmsg(database)))
				: SQLiteConnection.FromUtf8(NativeMethods.sqlite3_errstr(errorCode));
		}
	}
}
