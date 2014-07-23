﻿using System.Runtime.InteropServices;

namespace System.Data.SQLite
{
	internal static class NativeMethods
	{
		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern SqliteBackupHandle sqlite3_backup_init(SqliteDatabaseHandle pDest, byte[] zDestName, SqliteDatabaseHandle pSource, byte[] zSourceName);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern SQLiteErrorCode sqlite3_backup_step(SqliteBackupHandle p, int nPage);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern SQLiteErrorCode sqlite3_backup_finish(IntPtr p);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern int sqlite3_backup_remaining(SqliteBackupHandle p);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern int sqlite3_backup_pagecount(SqliteBackupHandle p);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern SQLiteErrorCode sqlite3_bind_blob(SqliteStatementHandle stmt, int ordinal, byte[] value, int count, IntPtr free);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern SQLiteErrorCode sqlite3_bind_double(SqliteStatementHandle stmt, int ordinal, double value);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern SQLiteErrorCode sqlite3_bind_int(SqliteStatementHandle stmt, int ordinal, int value);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern SQLiteErrorCode sqlite3_bind_int64(SqliteStatementHandle stmt, int ordinal, long value);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern SQLiteErrorCode sqlite3_bind_null(SqliteStatementHandle stmt, int ordinal);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern int sqlite3_bind_parameter_index(SqliteStatementHandle stmt, byte[] zName);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern SQLiteErrorCode sqlite3_bind_text(SqliteStatementHandle stmt, int ordinal, byte[] value, int count, IntPtr free);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern SQLiteErrorCode sqlite3_close(IntPtr db);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern SQLiteErrorCode sqlite3_close_v2(IntPtr db);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern IntPtr sqlite3_column_blob(SqliteStatementHandle stmt, int index);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern int sqlite3_column_bytes(SqliteStatementHandle stmt, int index);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern int sqlite3_column_count(SqliteStatementHandle stmt);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern IntPtr sqlite3_column_decltype(SqliteStatementHandle stmt, int index);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern double sqlite3_column_double(SqliteStatementHandle stmt, int index);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern int sqlite3_column_int(SqliteStatementHandle stmt, int index);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern long sqlite3_column_int64(SqliteStatementHandle stmt, int index);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern IntPtr sqlite3_column_name(SqliteStatementHandle stmt, int index);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern IntPtr sqlite3_column_text(SqliteStatementHandle stmt, int index);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern SQLiteColumnType sqlite3_column_type(SqliteStatementHandle statement, int ordinal);

		[DllImport(c_dllName, EntryPoint = "sqlite3_config", CallingConvention = c_callingConvention)]
		public static extern SQLiteErrorCode sqlite3_config_log(SQLiteConfigOpsEnum op, SQLiteLogCallback func, IntPtr pvUser);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern int sqlite3_db_readonly(SqliteDatabaseHandle db, [MarshalAs(UnmanagedType.LPStr)] string zDbName);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern SQLiteErrorCode sqlite3_errcode(SqliteDatabaseHandle db);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern IntPtr sqlite3_errmsg(SqliteDatabaseHandle db);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern IntPtr sqlite3_errstr(SQLiteErrorCode rc);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern SQLiteErrorCode sqlite3_finalize(IntPtr stmt);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern SQLiteErrorCode sqlite3_key(SqliteDatabaseHandle db, byte[] key, int keylen);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern SQLiteErrorCode sqlite3_open_v2(byte[] utf8Filename, out SqliteDatabaseHandle db, SQLiteOpenFlags flags, byte[] vfs);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public unsafe static extern SQLiteErrorCode sqlite3_prepare_v2(SqliteDatabaseHandle db, byte* pSql, int nBytes, out SqliteStatementHandle stmt, out byte* pzTail);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern void sqlite3_progress_handler(SqliteDatabaseHandle db, int virtualMachineInstructions, SQLiteProgressCallback callback, IntPtr userData);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern SQLiteErrorCode sqlite3_reset(SqliteStatementHandle stmt);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern SQLiteErrorCode sqlite3_step(SqliteStatementHandle stmt);

		[DllImport(c_dllName, CallingConvention = c_callingConvention)]
		public static extern int sqlite3_total_changes(SqliteDatabaseHandle db);

#if NET45
		const string c_dllName = "SQLite.Interop.dll";
#else
		const string c_dllName = "sqlite3";
#endif
		const CallingConvention c_callingConvention = CallingConvention.Cdecl;
	}

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void SQLiteLogCallback(IntPtr pUserData, int errorCode, IntPtr pMessage);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate int SQLiteProgressCallback(IntPtr pUserData);

	// These are the options to the internal sqlite3_config call.
	internal enum SQLiteConfigOpsEnum
	{
		SQLITE_CONFIG_NONE = 0, // nil
		SQLITE_CONFIG_SINGLETHREAD = 1, // nil
		SQLITE_CONFIG_MULTITHREAD = 2, // nil
		SQLITE_CONFIG_SERIALIZED = 3, // nil
		SQLITE_CONFIG_MALLOC = 4, // sqlite3_mem_methods*
		SQLITE_CONFIG_GETMALLOC = 5, // sqlite3_mem_methods*
		SQLITE_CONFIG_SCRATCH = 6, // void*, int sz, int N
		SQLITE_CONFIG_PAGECACHE = 7, // void*, int sz, int N
		SQLITE_CONFIG_HEAP = 8, // void*, int nByte, int min
		SQLITE_CONFIG_MEMSTATUS = 9, // boolean
		SQLITE_CONFIG_MUTEX = 10, // sqlite3_mutex_methods*
		SQLITE_CONFIG_GETMUTEX = 11, // sqlite3_mutex_methods*
		// previously SQLITE_CONFIG_CHUNKALLOC 12 which is now unused
		SQLITE_CONFIG_LOOKASIDE = 13, // int int
		SQLITE_CONFIG_PCACHE = 14, // sqlite3_pcache_methods*
		SQLITE_CONFIG_GETPCACHE = 15, // sqlite3_pcache_methods*
		SQLITE_CONFIG_LOG = 16, // xFunc, void*
		SQLITE_CONFIG_URI = 17, // int
		SQLITE_CONFIG_PCACHE2 = 18, // sqlite3_pcache_methods2*
		SQLITE_CONFIG_GETPCACHE2 = 19, // sqlite3_pcache_methods2*
		SQLITE_CONFIG_COVERING_INDEX_SCAN = 20, // int
		SQLITE_CONFIG_SQLLOG = 21, // xSqllog, void*
		SQLITE_CONFIG_MMAP_SIZE = 22, // sqlite3_int64, sqlite3_int64
		SQLITE_CONFIG_WIN32_HEAPSIZE = 23 // int nByte
	}
}
