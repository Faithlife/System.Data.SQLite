#if NET5_0
using System.Buffers;
#endif
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace System.Data.SQLite
{
	internal sealed class SqliteStatementPreparer : IDisposable
	{
		public SqliteStatementPreparer(SqliteDatabaseHandle database, string commandText)
		{
			m_database = database;
#if NET5_0
			m_commandTextBytesLength = Encoding.UTF8.GetByteCount(commandText);
			m_commandTextBytes = ArrayPool<byte>.Shared.Rent(m_commandTextBytesLength);
			Encoding.UTF8.GetBytes(commandText.AsSpan(), m_commandTextBytes.AsSpan());
#else
			m_commandTextBytes = SQLiteConnection.ToUtf8(commandText);
			m_commandTextBytesLength = m_commandTextBytes.Length;
#endif
			m_statements = new List<SqliteStatementHandle>();
			m_refCount = 1;
		}

		public SqliteStatementHandle Get(int index, CancellationToken cancellationToken)
		{
			if (m_statements is null)
				throw new ObjectDisposedException(GetType().Name);
			if (index < 0 || index > m_statements.Count)
				throw new ArgumentOutOfRangeException("index");
			if (index < m_statements.Count)
				return m_statements[index];
			if (m_bytesUsed == m_commandTextBytesLength)
				return null;

			SQLiteErrorCode errorCode;
			do
			{
				unsafe
				{
					fixed (byte* sqlBytes = &m_commandTextBytes[m_bytesUsed])
					{
						byte* remainingSqlBytes;
						SqliteStatementHandle statement;
						errorCode = NativeMethods.sqlite3_prepare_v2(m_database, sqlBytes, m_commandTextBytesLength - m_bytesUsed, out statement, out remainingSqlBytes);
						switch (errorCode)
						{
						case SQLiteErrorCode.Ok:
							m_bytesUsed += (int) (remainingSqlBytes - sqlBytes);
							m_statements.Add(statement);
							break;

						case SQLiteErrorCode.Busy:
						case SQLiteErrorCode.Locked:
						case SQLiteErrorCode.CantOpen:
							if (cancellationToken.IsCancellationRequested)
								return null;
							Thread.Sleep(20);
							break;

						case SQLiteErrorCode.Interrupt:
							cancellationToken.ThrowIfCancellationRequested();
							return null;

						default:
							throw new SQLiteException(errorCode, m_database);
						}
					}
				}
			} while (errorCode != SQLiteErrorCode.Ok);

			return m_statements[index];
		}

		public void AddRef()
		{
			if (m_refCount == 0)
				throw new ObjectDisposedException(GetType().Name);
			m_refCount++;
		}

		public void Dispose()
		{
			m_refCount--;
			if (m_refCount == 0)
			{
				foreach (var statement in m_statements)
					statement.Dispose();
				m_statements = null;
#if NET5_0
				ArrayPool<byte>.Shared.Return(m_commandTextBytes);
#endif
			}
			else if (m_refCount < 0)
			{
				throw new InvalidOperationException("SqliteStatementList ref count decremented below zero.");
			}
		}

		readonly SqliteDatabaseHandle m_database;
		readonly byte[] m_commandTextBytes;
		readonly int m_commandTextBytesLength;
		List<SqliteStatementHandle> m_statements;
		int m_bytesUsed;
		int m_refCount;
	}
}
