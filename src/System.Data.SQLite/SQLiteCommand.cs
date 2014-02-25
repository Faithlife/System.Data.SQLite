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
			return ExecuteDbDataReaderAsync(behavior, CancellationToken.None).Result;
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
			VerifyNotDisposed();
			if (DbConnection == null)
				throw new InvalidOperationException("Connection property must be non-null.");
			if (DbTransaction != ((SQLiteConnection) DbConnection).CurrentTransaction)
				throw new InvalidOperationException("The transaction associated with this command is not the connection's active transaction.");
			return Task.FromResult<DbDataReader>(new SQLiteDataReader(this, behavior, cancellationToken));
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

		private void VerifyNotDisposed()
		{
			if (m_parameterCollection == null)
				throw new ObjectDisposedException(GetType().Name);
		}

		SQLiteParameterCollection m_parameterCollection;
	}
}
