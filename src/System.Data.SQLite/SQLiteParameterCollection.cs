using System.Collections;
using System.Collections.Generic;
using System.Data.Common;

namespace System.Data.SQLite
{
	public sealed class SQLiteParameterCollection : DbParameterCollection
	{
		internal SQLiteParameterCollection()
		{
			m_parameters = new List<SQLiteParameter>();
		}

		public SQLiteParameter Add(string parameterName, DbType dbType)
		{
			SQLiteParameter parameter = new SQLiteParameter
			{
				ParameterName = parameterName,
				DbType = dbType,
			};
			m_parameters.Add(parameter);
			return parameter;
		}

		public override int Add(object value)
		{
			m_parameters.Add((SQLiteParameter) value);
			return m_parameters.Count - 1;
		}

		public override void AddRange(Array values)
		{
			foreach (var obj in values)
				Add(obj);
		}

		public override bool Contains(object value)
		{
			return m_parameters.Contains((SQLiteParameter) value);
		}

		public override bool Contains(string value)
		{
			return IndexOf(value) != -1;
		}

		public override void CopyTo(Array array, int index)
		{
			throw new NotSupportedException();
		}

		public override void Clear()
		{
			m_parameters.Clear();
		}

		public override IEnumerator GetEnumerator()
		{
			return m_parameters.GetEnumerator();
		}

		protected override DbParameter GetParameter(int index)
		{
			return m_parameters[index];
		}

		protected override DbParameter GetParameter(string parameterName)
		{
			return m_parameters[IndexOf(parameterName)];
		}

		public override int IndexOf(object value)
		{
			return m_parameters.IndexOf((SQLiteParameter) value);
		}

		public override int IndexOf(string parameterName)
		{
			return m_parameters.FindIndex(x => x.ParameterName == parameterName);
		}

		public override void Insert(int index, object value)
		{
			m_parameters.Insert(index, (SQLiteParameter) value);
		}

		public override void Remove(object value)
		{
			m_parameters.Remove((SQLiteParameter) value);
		}

		public override void RemoveAt(int index)
		{
			m_parameters.RemoveAt(index);
		}

		public override void RemoveAt(string parameterName)
		{
			RemoveAt(IndexOf(parameterName));
		}

		protected override void SetParameter(int index, DbParameter value)
		{
			m_parameters[index] = (SQLiteParameter) value;
		}

		protected override void SetParameter(string parameterName, DbParameter value)
		{
			SetParameter(IndexOf(parameterName), value);
		}

		public override int Count
		{
			get { return m_parameters.Count; }
		}

		public override bool IsFixedSize
		{
			get { return false; }
		}

		public override bool IsReadOnly
		{
			get { return false; }
		}

		public override bool IsSynchronized
		{
			get { return false; }
		}

		public override object SyncRoot
		{
			get { throw new NotSupportedException(); }
		}

		public new SQLiteParameter this[int index]
		{
			get { return m_parameters[index]; }
			set { m_parameters[index] = value; }
		}

		readonly List<SQLiteParameter> m_parameters;
	}
}
