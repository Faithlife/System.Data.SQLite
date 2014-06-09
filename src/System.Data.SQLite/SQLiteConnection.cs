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
			SQLiteLog.Initialize();
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
			var dataSource = connectionStringBuilder.DataSource;
			if (string.IsNullOrEmpty(dataSource))
				throw new InvalidOperationException("Connection String Data Source must be set.");

			SQLiteOpenFlags openFlags = connectionStringBuilder.ReadOnly ? SQLiteOpenFlags.ReadOnly : SQLiteOpenFlags.ReadWrite;
			if (!connectionStringBuilder.FailIfMissing && !connectionStringBuilder.ReadOnly)
				openFlags |= SQLiteOpenFlags.Create;

			SetState(ConnectionState.Connecting);

			Match m = s_vfsRegex.Match(dataSource);
			string fileName = m.Groups["fileName"].Value;
			string vfsName = m.Groups["vfsName"].Value;
			var errorCode = NativeMethods.sqlite3_open_v2(ToNullTerminatedUtf8(fileName), out m_db, openFlags, string.IsNullOrEmpty(vfsName) ? null : ToNullTerminatedUtf8(vfsName));

			bool success = false;
			try
			{
				if (errorCode != SQLiteErrorCode.Ok)
				{
					SetState(ConnectionState.Broken);
					errorCode.ThrowOnError();
				}

				if (!string.IsNullOrEmpty(connectionStringBuilder.Password))
				{
					byte[] passwordBytes = Encoding.UTF8.GetBytes(connectionStringBuilder.Password);
					NativeMethods.sqlite3_key(m_db, passwordBytes, passwordBytes.Length).ThrowOnError();
				}

				int isReadOnly = NativeMethods.sqlite3_db_readonly(m_db, "main");
				if (isReadOnly == 1 && !connectionStringBuilder.ReadOnly)
					throw new SQLiteException(SQLiteErrorCode.ReadOnly);

				if (connectionStringBuilder.CacheSize != 0)
					this.ExecuteNonQuery("pragma cache_size={0}".FormatInvariant(connectionStringBuilder.CacheSize));

				if (connectionStringBuilder.PageSize != 0)
					this.ExecuteNonQuery("pragma page_size={0}".FormatInvariant(connectionStringBuilder.PageSize));

				if (connectionStringBuilder.ForeignKeys)
					this.ExecuteNonQuery("pragma foreign_keys = on");

				if (connectionStringBuilder.JournalMode != SQLiteJournalModeEnum.Default)
					this.ExecuteNonQuery("pragma journal_mode={0}".FormatInvariant(connectionStringBuilder.JournalMode));

				if (connectionStringBuilder.ContainsKey(SQLiteConnectionStringBuilder.SynchronousKey))
					this.ExecuteNonQuery("pragma synchronous={0}".FormatInvariant(connectionStringBuilder.SyncMode));

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
			get { throw new NotSupportedException(); }
		}

		public override string ServerVersion
		{
			get { throw new NotSupportedException(); }
		}

		protected override DbCommand CreateDbCommand()
		{
			return new SQLiteCommand(this);
		}

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

		public override int ConnectionTimeout
		{
			get { throw new NotSupportedException(); }
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

		protected override object GetService(Type service)
		{
			throw new NotSupportedException();
		}

		protected override bool CanRaiseEvents
		{
			get { return false; }
		}

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

			byte[] bytes = new byte[length];
			Marshal.Copy(ptr, bytes, 0, length);
			return Encoding.UTF8.GetString(bytes);
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

		private void VerifyNotDisposed()
		{
			if (m_isDisposed)
				throw new ObjectDisposedException(GetType().Name);
		}

		static readonly Regex s_vfsRegex = new Regex(@"^(?:\*(?'vfsName'.{0,16})\*)?(?'fileName'.*)$", RegexOptions.CultureInvariant);

		SqliteDatabaseHandle m_db;
		readonly Stack<SQLiteTransaction> m_transactions;
		ConnectionState m_connectionState;
		bool m_isDisposed;
	}
}
