using System.Collections.Generic;
using System.Linq;

namespace System.Data.SQLite
{
	internal sealed class SqliteStatementList : IDisposable
	{
		public SqliteStatementList(IEnumerable<SqliteStatementHandle> statements)
		{
			m_statements = statements.ToArray();
			m_refCount = 1;
		}

		public int Count
		{
			get { return m_statements.Length; }
		}

		public SqliteStatementHandle this[int index]
		{
			get { return m_statements[index]; }
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
			}
			else if (m_refCount < 0)
			{
				throw new InvalidOperationException("SqliteStatementList ref count decremented below zero.");
			}
		}

		int m_refCount;
		SqliteStatementHandle[] m_statements;
	}
}
