using System.Data.Common;
#if NET45 || MAC
using System.Runtime.Serialization;
#endif

namespace System.Data.SQLite
{
#if NET45 || MAC
	[Serializable]
#endif
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

#if NET45 || MAC
		private SQLiteException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}
#endif

		private static string GetErrorString(SQLiteErrorCode errorCode, SqliteDatabaseHandle database)
		{
#if NET45
			string errorString = SQLiteConnection.FromUtf8(NativeMethods.sqlite3_errstr(errorCode));
#else
			string errorString = errorCode.ToString();
#endif
			return database != null ? "{0}: {1}".FormatInvariant(errorString, SQLiteConnection.FromUtf8(NativeMethods.sqlite3_errmsg(database)))
				: errorString;
		}
	}
}
