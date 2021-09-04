using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace System.Data.SQLite
{
	public sealed class SQLiteConnection : DbConnection
	{
		public SQLiteConnection()
		{
			m_transactions = new Stack<SQLiteTransaction>();
		}

		public SQLiteConnection(string connectionString)
			: this()
		{
			ConnectionString = connectionString;
		}

		public SQLiteConnection(IntPtr db)
			: this()
		{
			m_db = new SqliteDatabaseHandle(db);
			SetState(ConnectionState.Open);
		}

		public new SQLiteTransaction BeginTransaction()
		{
			return (SQLiteTransaction) base.BeginTransaction();
		}

		protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
		{
			if (isolationLevel == IsolationLevel.Unspecified)
				isolationLevel = IsolationLevel.Serializable;
			if (isolationLevel != IsolationLevel.Serializable && isolationLevel != IsolationLevel.ReadCommitted)
				throw new ArgumentOutOfRangeException("isolationLevel", isolationLevel, "Specified IsolationLevel value is not supported.");

			if (m_transactions.Count == 0)
				this.ExecuteNonQuery(isolationLevel == IsolationLevel.Serializable ? "BEGIN IMMEDIATE" : "BEGIN");
			m_transactions.Push(new SQLiteTransaction(this, isolationLevel));
			return CurrentTransaction;
		}

		public override void Close()
		{
			Dispose();
		}

		public override void ChangeDatabase(string databaseName)
		{
			throw new NotSupportedException();
		}

		public override void Open()
		{
			VerifyNotDisposed();
			if (State != ConnectionState.Closed)
				throw new InvalidOperationException("Cannot Open when State is {0}.".FormatInvariant(State));

			var connectionStringBuilder = new SQLiteConnectionStringBuilder { ConnectionString = ConnectionString };
			m_dataSource = connectionStringBuilder.DataSource;
			if (string.IsNullOrEmpty(m_dataSource))
				throw new InvalidOperationException("Connection String Data Source must be set.");

			SQLiteOpenFlags openFlags = (connectionStringBuilder.ReadOnly ? SQLiteOpenFlags.ReadOnly : SQLiteOpenFlags.ReadWrite) | SQLiteOpenFlags.Uri;
			if (!connectionStringBuilder.FailIfMissing && !connectionStringBuilder.ReadOnly)
				openFlags |= SQLiteOpenFlags.Create;

			SetState(ConnectionState.Connecting);

			Match m = s_vfsRegex.Match(m_dataSource);
			string fileName = m.Groups["fileName"].Value;
			string vfsName = m.Groups["vfsName"].Value;
			var errorCode = NativeMethods.sqlite3_open_v2(ToNullTerminatedUtf8(fileName), out m_db, openFlags, string.IsNullOrEmpty(vfsName) ? null : ToNullTerminatedUtf8(vfsName));

			bool success = false;
			try
			{
				if (errorCode != SQLiteErrorCode.Ok)
				{
					SetState(ConnectionState.Broken);
					throw new SQLiteException(errorCode, m_db);
				}

				if (!string.IsNullOrEmpty(connectionStringBuilder.Password))
				{
					byte[] passwordBytes = Encoding.UTF8.GetBytes(connectionStringBuilder.Password);
					errorCode = NativeMethods.sqlite3_key(m_db, passwordBytes, passwordBytes.Length);
					if (errorCode != SQLiteErrorCode.Ok)
						throw new SQLiteException(errorCode, m_db);
				}

				bool allowOpenReadOnly = true;
#if MONOANDROID
				// opening read-only throws "EntryPointNotFoundException: sqlite3_db_readonly" on Android API 15 and below (JellyBean is API 16)
				allowOpenReadOnly = Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.JellyBean;
#endif
				if (allowOpenReadOnly)
				{
					int isReadOnly = NativeMethods.sqlite3_db_readonly(m_db, "main");
					if (isReadOnly == 1 && !connectionStringBuilder.ReadOnly)
						throw new SQLiteException(SQLiteErrorCode.ReadOnly);
				}

				// wait up to ten seconds (in native code) when there is DB contention, but still give managed code a
				// chance to respond to cancellation periodically
				NativeMethods.sqlite3_busy_timeout(m_db, 10000);

				if (connectionStringBuilder.CacheSize != 0)
					this.ExecuteNonQuery("pragma cache_size={0}".FormatInvariant(connectionStringBuilder.CacheSize));

				if (connectionStringBuilder.PageSize != 0)
					this.ExecuteNonQuery("pragma page_size={0}".FormatInvariant(connectionStringBuilder.PageSize));

				if (connectionStringBuilder.ContainsKey(SQLiteConnectionStringBuilder.MmapSizeKey))
					this.ExecuteNonQuery("pragma mmap_size={0}".FormatInvariant(connectionStringBuilder.MmapSize));

				if (connectionStringBuilder.ForeignKeys)
					this.ExecuteNonQuery("pragma foreign_keys = on");

				if (connectionStringBuilder.JournalMode != SQLiteJournalModeEnum.Default)
					this.ExecuteNonQuery("pragma journal_mode={0}".FormatInvariant(connectionStringBuilder.JournalMode));

				if (connectionStringBuilder.ContainsKey(SQLiteConnectionStringBuilder.JournalSizeLimitKey))
					this.ExecuteNonQuery("pragma journal_size_limit={0}".FormatInvariant(connectionStringBuilder.JournalSizeLimit));

				if (connectionStringBuilder.ContainsKey(SQLiteConnectionStringBuilder.PersistWalKey))
				{
					unsafe
					{
						const int sqliteFcntlPersistWal = 10;
						int enablePersistentWalMode = connectionStringBuilder.PersistWal ? 1 : 0;
						NativeMethods.sqlite3_file_control(m_db, "main", sqliteFcntlPersistWal, &enablePersistentWalMode);
					}
				}

				if (connectionStringBuilder.ContainsKey(SQLiteConnectionStringBuilder.SynchronousKey))
					this.ExecuteNonQuery("pragma synchronous={0}".FormatInvariant(connectionStringBuilder.SyncMode));

				if (connectionStringBuilder.TempStore != SQLiteTemporaryStore.Default)
					this.ExecuteNonQuery("pragma temp_store={0}".FormatInvariant(connectionStringBuilder.TempStore));

				if (m_statementCompleted != null)
					SetProfileCallback(s_profileCallback);

				SetState(ConnectionState.Open);
				success = true;
			}
			finally
			{
				if (!success)
					Utility.Dispose(ref m_db);
			}
		}

		public override string ConnectionString { get; set; }

		public override string Database
		{
			get { throw new NotSupportedException(); }
		}

		public override ConnectionState State
		{
			get { return m_connectionState; }
		}

		public override string DataSource
		{
			get { return m_dataSource; }
		}

		public override string ServerVersion
		{
			get { throw new NotSupportedException(); }
		}

		protected override DbCommand CreateDbCommand()
		{
			return new SQLiteCommand(this);
		}

#if !PORTABLE
		public override DataTable GetSchema()
		{
			throw new NotSupportedException();
		}

		public override DataTable GetSchema(string collectionName)
		{
			throw new NotSupportedException();
		}

		public override DataTable GetSchema(string collectionName, string[] restrictionValues)
		{
			throw new NotSupportedException();
		}

		public override Task OpenAsync(CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}
#endif

		public override int ConnectionTimeout
		{
			get { throw new NotSupportedException(); }
		}

		/// <summary>Backs up the database, using the specified database connection as the destination.</summary>
		/// <param name="destination">The destination database connection.</param>
		/// <param name="destinationName">The destination database name (usually <c>"main"</c>).</param>
		/// <param name="sourceName">The source database name (usually <c>"main"</c>).</param>
		/// <param name="pages">The number of pages to copy, or negative to copy all remaining pages.</param>
		/// <param name="callback">The method to invoke between each step of the backup process.  This
		/// parameter may be <c>null</c> (i.e., no callbacks will be performed).</param>
		/// <param name="retryMilliseconds">The number of milliseconds to sleep after encountering a locking error
		/// during the backup process.  A value less than zero means that no sleep should be performed.</param>
		public void BackupDatabase(SQLiteConnection destination, string destinationName, string sourceName, int pages, SQLiteBackupCallback callback, int retryMilliseconds)
		{
			VerifyNotDisposed();
			if (m_connectionState != ConnectionState.Open)
				throw new InvalidOperationException("Source database is not open.");
			if (destination == null)
				throw new ArgumentNullException("destination");
			if (destination.m_connectionState != ConnectionState.Open)
				throw new ArgumentException("Destination database is not open.", "destination");
			if (destinationName == null)
				throw new ArgumentNullException("destinationName");
			if (sourceName == null)
				throw new ArgumentNullException("sourceName");
			if (pages == 0)
				throw new ArgumentException("pages must not be 0.", "pages");

			using (SqliteBackupHandle backup = NativeMethods.sqlite3_backup_init(destination.m_db, ToNullTerminatedUtf8(destinationName), m_db, ToNullTerminatedUtf8(sourceName)))
			{
				if (backup == null)
					throw new SQLiteException(NativeMethods.sqlite3_errcode(m_db), m_db);

				while (true)
				{
					SQLiteErrorCode error = NativeMethods.sqlite3_backup_step(backup, pages);

					if (error == SQLiteErrorCode.Done)
					{
						break;
					}
					else if (error == SQLiteErrorCode.Ok || error == SQLiteErrorCode.Busy || error == SQLiteErrorCode.Locked)
					{
						bool retry = error != SQLiteErrorCode.Ok;
						if (callback != null && !callback(this, sourceName, destination, destinationName, pages, NativeMethods.sqlite3_backup_remaining(backup), NativeMethods.sqlite3_backup_pagecount(backup), retry))
							break;

						if (retry && retryMilliseconds > 0)
							Thread.Sleep(retryMilliseconds);
					}
					else
					{
						throw new SQLiteException(error, m_db);
					}
				}
			}
		}

		public event StatementCompletedEventHandler StatementCompleted
		{
			add
			{
				if (value == null)
					throw new ArgumentNullException("value");

				if (m_statementCompleted == null && m_db != null)
					SetProfileCallback(s_profileCallback);

				m_statementCompleted += value;
			}
			remove
			{
				if (value == null)
					throw new ArgumentNullException("value");

				m_statementCompleted -= value;
				if (m_statementCompleted == null && m_db != null)
					SetProfileCallback(null);
			}
		}

		protected override DbProviderFactory DbProviderFactory
		{
			get { throw new NotSupportedException(); }
		}

		protected override void Dispose(bool disposing)
		{
			try
			{
				if (disposing)
				{
					if (m_db != null)
					{
						while (m_transactions.Count > 0)
							m_transactions.Pop().Dispose();
						if (m_statementCompleted != null)
							SetProfileCallback(null);
						Utility.Dispose(ref m_db);
						SetState(ConnectionState.Closed);
					}
				}
				m_isDisposed = true;
			}
			finally
			{
				base.Dispose(disposing);
			}
		}

#if !PORTABLE
		protected override object GetService(Type service)
		{
			throw new NotSupportedException();
		}

		protected override bool CanRaiseEvents
		{
			get { return false; }
		}
#endif

		internal SQLiteTransaction CurrentTransaction
		{
			get { return m_transactions.FirstOrDefault(); }
		}

		internal bool IsOnlyTransaction(SQLiteTransaction transaction)
		{
			return m_transactions.Count == 1 && m_transactions.Peek() == transaction;
		}

		internal void PopTransaction()
		{
			m_transactions.Pop();
		}

		internal SqliteDatabaseHandle Handle
		{
			get
			{
				VerifyNotDisposed();
				return m_db;
			}
		}

		internal static byte[] ToUtf8(string value)
		{
			return Encoding.UTF8.GetBytes(value);
		}

		internal static byte[] ToNullTerminatedUtf8(string value)
		{
			var encoding = Encoding.UTF8;
			int len = encoding.GetByteCount(value);
			byte[] bytes = new byte[len + 1];
			encoding.GetBytes(value, 0, value.Length, bytes, 0);
			return bytes;
		}

		internal static string FromUtf8(IntPtr ptr)
		{
			int length = 0;
			unsafe
			{
				byte* p = (byte*) ptr.ToPointer();
				while (*p++ != 0)
					length++;
			}

			return FromUtf8(ptr, length);
		}

		internal static string FromUtf8(IntPtr ptr, int length)
		{
#if NET47
			unsafe
			{
				return Encoding.UTF8.GetString((byte*) ptr.ToPointer(), length);
			}
#else
			byte[] bytes = new byte[length];
			Marshal.Copy(ptr, bytes, 0, length);
			return Encoding.UTF8.GetString(bytes, 0, length);
#endif
		}

		private void SetProfileCallback(SQLiteTraceV2Callback callback)
		{
			if (callback != null && !m_handle.IsAllocated)
				m_handle = GCHandle.Alloc(this);
			else if (callback == null && m_handle.IsAllocated)
				m_handle.Free();

			NativeMethods.sqlite3_trace_v2(m_db, SQLiteTraceEvents.SQLITE_TRACE_PROFILE, callback, m_handle.IsAllocated ? GCHandle.ToIntPtr(m_handle) : IntPtr.Zero);
		}

		private void SetState(ConnectionState newState)
		{
			if (m_connectionState != newState)
			{
				var previousState = m_connectionState;
				m_connectionState = newState;
				OnStateChange(new StateChangeEventArgs(previousState, newState));
			}
		}

#if MONOTOUCH
		[MonoTouch.MonoPInvokeCallback(typeof(SQLiteTraceV2Callback))]
#elif XAMARIN_IOS
		[ObjCRuntime.MonoPInvokeCallback(typeof(SQLiteTraceV2Callback))]
#endif
		private static void ProfileCallback(SQLiteTraceEvents eventCode, IntPtr userData, IntPtr pStmt, IntPtr pDuration)
		{
			var handle = GCHandle.FromIntPtr(userData);
			var connection = (SQLiteConnection) handle.Target;
			StatementCompletedEventHandler handler = connection.m_statementCompleted;
			if (handler != null)
			{
				var sql = FromUtf8(NativeMethods.sqlite3_sql(pStmt));
				var nanoseconds = Marshal.ReadInt64(pDuration);
				handler(connection, new StatementCompletedEventArgs(sql, TimeSpan.FromMilliseconds(nanoseconds / 1000000.0)));
			}
		}

		private void VerifyNotDisposed()
		{
			if (m_isDisposed)
				throw new ObjectDisposedException(GetType().Name);
		}

		static readonly Regex s_vfsRegex = new Regex(@"^(?:\*(?'vfsName'.{0,16})\*)?(?'fileName'.*)$", RegexOptions.CultureInvariant);

		SqliteDatabaseHandle m_db;
		readonly Stack<SQLiteTransaction> m_transactions;
		static readonly SQLiteTraceV2Callback s_profileCallback = ProfileCallback;
		ConnectionState m_connectionState;
		GCHandle m_handle;
		bool m_isDisposed;
		StatementCompletedEventHandler m_statementCompleted;
		string m_dataSource;
	}

	/// <summary>
	/// Raised between each backup step.
	/// </summary>
	/// <param name="source">The source database connection.</param>
	/// <param name="sourceName">The source database name.</param>
	/// <param name="destination">The destination database connection.</param>
	/// <param name="destinationName">The destination database name.</param>
	/// <param name="pages">The number of pages copied with each step.</param>
	/// <param name="remainingPages">The number of pages remaining to be copied.</param>
	/// <param name="totalPages">The total number of pages in the source database.</param>
	/// <param name="retry">Set to true if the operation needs to be retried due to database locking issues; otherwise, set to false.</param>
	/// <returns><c>true</c> to continue with the backup process; otherwise  <c>false</c> to halt the backup process, rolling back any changes that have been made so far.</returns>
	public delegate bool SQLiteBackupCallback(SQLiteConnection source, string sourceName, SQLiteConnection destination, string destinationName, int pages, int remainingPages, int totalPages, bool retry);
}
