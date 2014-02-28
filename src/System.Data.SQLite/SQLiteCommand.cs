using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace System.Data.SQLite
{
	public sealed class SQLiteCommand : DbCommand
	{
		public SQLiteCommand()
			: this(null, null, null)
		{
		}

		public SQLiteCommand(string commandText)
			: this(commandText, null, null)
		{
		}

		public SQLiteCommand(SQLiteConnection connection)
			: this(null, connection, null)
		{
		}

		public SQLiteCommand(string commandText, SQLiteConnection connection)
			: this(commandText, connection, null)
		{
		}

		public SQLiteCommand(string commandText, SQLiteConnection connection, SQLiteTransaction transaction)
		{
			CommandText = commandText;
			DbConnection = connection;
			DbTransaction = transaction;
			m_parameterCollection = new SQLiteParameterCollection();
		}

		public override void Prepare()
		{
			Prepare(CancellationToken.None);
		}

		public override string CommandText { get; set; }

		public override int CommandTimeout { get; set; }

		public override CommandType CommandType
		{
			get
			{
				return CommandType.Text;
			}
			set
			{
				if (value != CommandType.Text)
					throw new ArgumentException("CommandType must be Text.", "value");
			}
		}

		public override UpdateRowSource UpdatedRowSource
		{
			get { throw new NotSupportedException(); }
			set { throw new NotSupportedException(); }
		}

		protected override DbConnection DbConnection { get; set; }

		public new SQLiteParameterCollection Parameters
		{
			get
			{
				VerifyNotDisposed();
				return m_parameterCollection;
			}
		}

		protected override DbParameterCollection DbParameterCollection
		{
			get { return Parameters; }
		}

		protected override DbTransaction DbTransaction { get; set; }

		public override bool DesignTimeVisible { get; set; }

		public override void Cancel()
		{
			throw new NotImplementedException();
		}

		protected override DbParameter CreateDbParameter()
		{
			VerifyNotDisposed();
			return new SQLiteParameter();
		}

		protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
		{
			VerifyValid();
			Prepare();
			return SQLiteDataReader.Create(this, behavior);
		}

		public new SQLiteDataReader ExecuteReader()
		{
			return (SQLiteDataReader) base.ExecuteReader();
		}

		public override int ExecuteNonQuery()
		{
			using (var reader = ExecuteReader())
			{
				do
				{
					while (reader.Read())
					{
					}
				} while (reader.NextResult());
				return reader.RecordsAffected;
			}
		}

		public override object ExecuteScalar()
		{
			using (var reader = ExecuteReader(CommandBehavior.SingleResult | CommandBehavior.SingleRow))
			{
				do
				{
					if (reader.Read())
						return reader.GetValue(0);
				} while (reader.NextResult());
			}
			return null;
		}

		public override async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
		{
			using (var reader = await ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
			{
				do
				{
					while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
					{
					}
				} while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false));
				return reader.RecordsAffected;
			}
		}

		protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
		{
			VerifyValid();
			if (!Prepare(cancellationToken))
				throw new OperationCanceledException(cancellationToken);
			return SQLiteDataReader.CreateAsync(this, behavior, cancellationToken);
		}

		public override async Task<object> ExecuteScalarAsync(CancellationToken cancellationToken)
		{
			using (var reader = await ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false))
			{
				do
				{
					if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
						return reader.GetValue(0);
				} while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false));
			}
			return null;
		}

		protected override void Dispose(bool disposing)
		{
			try
			{
				m_parameterCollection = null;
				Utility.Dispose(ref m_statements);
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

		internal SqliteStatementList GetStatements()
		{
			m_statements.AddRef();
			return m_statements;
		}

		private bool Prepare(CancellationToken cancellationToken)
		{
			if (m_statements == null)
			{
				var commandTextBytes = SQLiteConnection.ToUtf8(CommandText.Trim());
				var statements = new List<SqliteStatementHandle>();

				Random random = null;
				int bytesUsed = 0;
				while (bytesUsed < commandTextBytes.Length)
				{
					SQLiteErrorCode errorCode;
					do
					{
						unsafe
						{
							fixed (byte* sqlBytes = &commandTextBytes[bytesUsed])
							{
								byte* remainingSqlBytes;
								SqliteStatementHandle statement;
								errorCode = NativeMethods.sqlite3_prepare_v2(DatabaseHandle, sqlBytes, commandTextBytes.Length - bytesUsed, out statement, out remainingSqlBytes);
								switch (errorCode)
								{
								case SQLiteErrorCode.Ok:
									bytesUsed += (int) (remainingSqlBytes - sqlBytes);
									statements.Add(statement);
									break;

								case SQLiteErrorCode.Busy:
								case SQLiteErrorCode.Locked:
								case SQLiteErrorCode.CantOpen:
									if (cancellationToken.IsCancellationRequested)
										return false;
									if (random == null)
										random = new Random();
									Thread.Sleep(random.Next(1, 150));
									break;

								default:
									throw new SQLiteException(errorCode);
								}
							}
						}
					} while (errorCode != SQLiteErrorCode.Ok);
				}

				m_statements = new SqliteStatementList(statements);
			}

			return true;
		}

		private SqliteDatabaseHandle DatabaseHandle
		{
			get { return ((SQLiteConnection) Connection).Handle; }
		}

		private void VerifyNotDisposed()
		{
			if (m_parameterCollection == null)
				throw new ObjectDisposedException(GetType().Name);
		}

		private void VerifyValid()
		{
			VerifyNotDisposed();
			if (DbConnection == null)
				throw new InvalidOperationException("Connection property must be non-null.");
			if (DbConnection.State != ConnectionState.Open && DbConnection.State != ConnectionState.Connecting)
				throw new InvalidOperationException("Connection must be Open; current state is {0}.".FormatInvariant(DbConnection.State));
			if (DbTransaction != ((SQLiteConnection) DbConnection).CurrentTransaction)
				throw new InvalidOperationException("The transaction associated with this command is not the connection's active transaction.");
			if (string.IsNullOrWhiteSpace(CommandText))
				throw new InvalidOperationException("CommandText must be specified");
		}

		SQLiteParameterCollection m_parameterCollection;
		SqliteStatementList m_statements;
	}
}
