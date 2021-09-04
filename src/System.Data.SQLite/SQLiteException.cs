using System.Data.Common;
using System.Runtime.Serialization;

namespace System.Data.SQLite
{
	[Serializable]
	public sealed class SQLiteException : DbException
	{
		public SQLiteException(SQLiteErrorCode errorCode)
			: this(errorCode, null)
		{
		}

		internal SQLiteException(SQLiteErrorCode errorCode, SqliteDatabaseHandle database)
			: base(GetErrorString(errorCode, database), (int) errorCode)
		{
		}

		private SQLiteException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}

		private static string GetErrorString(SQLiteErrorCode errorCode, SqliteDatabaseHandle database)
		{
			var errorString = SQLiteConnection.FromUtf8(NativeMethods.sqlite3_errstr(errorCode));
			return database != null ? "{0}: {1}".FormatInvariant(errorString, SQLiteConnection.FromUtf8(NativeMethods.sqlite3_errmsg(database)))
				: errorString;
		}
	}
}
