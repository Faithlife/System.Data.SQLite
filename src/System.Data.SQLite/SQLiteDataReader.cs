using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Data.SQLite
{
	public sealed class SQLiteDataReader : DbDataReader
	{
		internal SQLiteDataReader(SQLiteCommand command, CommandBehavior behavior)
		{
			m_command = command;
			m_behavior = behavior;

			if (string.IsNullOrWhiteSpace(command.CommandText))
				throw new InvalidOperationException("CommandText must be specified");

			m_startingChanges = NativeMethods.sqlite3_total_changes(DatabaseHandle);
			m_commandBytes = SQLiteConnection.ToUtf8(command.CommandText.Trim());
			NextResult();
		}

		public override void Close()
		{
			// NOTE: DbDataReader.Dispose calls Close, so we can't put our logic in Dispose(bool) and call Dispose() from this method.
			Utility.Dispose(ref m_statement);

			if (m_behavior.HasFlag(CommandBehavior.CloseConnection))
			{
				var dbConnection = m_command.Connection;
				m_command.Dispose();
				dbConnection.Dispose();
			}

			m_hasRead = false;
			m_command = null;
		}

		public override bool NextResult()
		{
			if (m_bytesUsed == m_commandBytes.Length)
				return false;

			Utility.Dispose(ref m_statement);

			Random random = null;
			SQLiteErrorCode errorCode;
			do
			{
				unsafe
				{
					fixed (byte* sqlBytes = &m_commandBytes[m_bytesUsed])
					{
						byte* remainingSqlBytes;
						errorCode = NativeMethods.sqlite3_prepare_v2(DatabaseHandle, sqlBytes, m_commandBytes.Length - m_bytesUsed, out m_statement, out remainingSqlBytes);
						switch (errorCode)
						{
						case SQLiteErrorCode.Ok:
							m_bytesUsed += (int) (remainingSqlBytes - sqlBytes);
							break;

						case SQLiteErrorCode.Busy:
						case SQLiteErrorCode.Locked:
						case SQLiteErrorCode.CantOpen:
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

			bool success = false;
			try
			{
				foreach (SQLiteParameter parameter in m_command.Parameters)
				{
					string parameterName = parameter.ParameterName;
					if (parameterName[0] != '@')
						parameterName = "@" + parameterName;
					int index = NativeMethods.sqlite3_bind_parameter_index(m_statement, SQLiteConnection.ToUtf8(parameterName));
					if (index > 0)
					{
						object value = parameter.Value;
						if (value == null || value.Equals(DBNull.Value))
							NativeMethods.sqlite3_bind_null(m_statement, index).ThrowOnError();
						else if (value is int || (value is Enum && Enum.GetUnderlyingType(value.GetType()) == typeof(int)))
							NativeMethods.sqlite3_bind_int(m_statement, index, (int) value).ThrowOnError();
						else if (value is bool)
							NativeMethods.sqlite3_bind_int(m_statement, index, ((bool) value) ? 1 : 0).ThrowOnError();
						else if (value is string)
							BindText(index, (string) value);
						else if (value is byte[])
							BindBlob(index, (byte[]) value);
						else if (value is long)
							NativeMethods.sqlite3_bind_int64(m_statement, index, (long) value).ThrowOnError();
						else if (value is float)
							NativeMethods.sqlite3_bind_double(m_statement, index, (float) value).ThrowOnError();
						else if (value is double)
							NativeMethods.sqlite3_bind_double(m_statement, index, (double) value).ThrowOnError();
						else if (value is DateTime)
							BindText(index, ToString((DateTime) value));
						else if (value is Guid)
							BindBlob(index, ((Guid) value).ToByteArray());
						else if (value is byte)
							NativeMethods.sqlite3_bind_int(m_statement, index, (byte) value).ThrowOnError();
						else if (value is short)
							NativeMethods.sqlite3_bind_int(m_statement, index, (short) value).ThrowOnError();
						else
							BindText(index, Convert.ToString(value, CultureInfo.InvariantCulture));
					}
				}

				Reset();
				success = true;
			}
			finally
			{
				if (!success)
					Utility.Dispose(ref m_statement);
			}

			return true;
		}

		public override bool Read()
		{
			VerifyNotDisposed();
			Random random = null;

			while (true)
			{
				SQLiteErrorCode errorCode = NativeMethods.sqlite3_step(m_statement);

				switch (errorCode)
				{
				case SQLiteErrorCode.Done:
					Utility.Dispose(ref m_statement);
					Reset();
					return false;

				case SQLiteErrorCode.Row:
					m_hasRead = true;
					if (m_columnType == null)
						m_columnType = new DbType?[NativeMethods.sqlite3_column_count(m_statement)];
					return true;

				case SQLiteErrorCode.Busy:
				case SQLiteErrorCode.Locked:
				case SQLiteErrorCode.CantOpen:
					if (random == null)
						random = new Random();
					Thread.Sleep(random.Next(1, 150));
					break;

				default:
					throw new SQLiteException(errorCode);
				}
			}
		}

		public override bool IsClosed
		{
			get { return m_command == null; }
		}

		public override int RecordsAffected
		{
			get { return NativeMethods.sqlite3_total_changes(DatabaseHandle) - m_startingChanges; }
		}

		public override bool GetBoolean(int ordinal)
		{
			return (bool) GetValue(ordinal);
		}

		public override byte GetByte(int ordinal)
		{
			return (byte) GetValue(ordinal);
		}

		public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
		{
			var sqliteType = NativeMethods.sqlite3_column_type(m_statement, ordinal);
			if (sqliteType == SQLiteColumnType.Null)
				return 0;
			else if (sqliteType != SQLiteColumnType.Blob)
				throw new InvalidCastException("Cannot convert '{0}' to bytes.".FormatInvariant(sqliteType));

			int availableLength = NativeMethods.sqlite3_column_bytes(m_statement, ordinal);
			if (buffer == null)
			{
				// this isn't required by the DbDataReader.GetBytes API documentation, but is what System.Data.SQLite does
				// (as does SqlDataReader: http://msdn.microsoft.com/en-us/library/system.data.sqlclient.sqldatareader.getbytes.aspx)
				return availableLength;
			}

			if (bufferOffset + length > buffer.Length)
				throw new ArgumentException("bufferOffset + length cannot exceed buffer.Length", "length");

			IntPtr ptr = NativeMethods.sqlite3_column_blob(m_statement, ordinal);
			int lengthToCopy = Math.Min(availableLength - (int) dataOffset, length);
			Marshal.Copy(new IntPtr(ptr.ToInt64() + dataOffset), buffer, bufferOffset, lengthToCopy);
			return lengthToCopy;
		}

		public override char GetChar(int ordinal)
		{
			return (char) GetValue(ordinal);
		}

		public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
		{
			throw new NotImplementedException();
		}

		public override Guid GetGuid(int ordinal)
		{
			return (Guid) GetValue(ordinal);
		}

		public override short GetInt16(int ordinal)
		{
			return (short) GetValue(ordinal);
		}

		public override int GetInt32(int ordinal)
		{
			object value = GetValue(ordinal);
			if (value is short)
				return (short) value;
			else if (value is long)
				return checked((int) (long) value);
			return (int) value;
		}

		public override long GetInt64(int ordinal)
		{
			object value = GetValue(ordinal);
			if (value is short)
				return (short) value;
			if (value is int)
				return (int) value;
			return (long) value;
		}

		public override DateTime GetDateTime(int ordinal)
		{
			return (DateTime) GetValue(ordinal);
		}

		public override string GetString(int ordinal)
		{
			return (string) GetValue(ordinal);
		}

		public override decimal GetDecimal(int ordinal)
		{
			throw new NotImplementedException();
		}

		public override double GetDouble(int ordinal)
		{
			object value = GetValue(ordinal);
			if (value is float)
				return (float) value;
			return (double) value;
		}

		public override float GetFloat(int ordinal)
		{
			return (float) GetValue(ordinal);
		}

		public override string GetName(int ordinal)
		{
			throw new NotSupportedException();
		}

		public override int GetValues(object[] values)
		{
			VerifyRead();
			int count = Math.Min(values.Length, FieldCount);
			for (int i = 0; i < count; i++)
				values[i] = GetValue(i);
			return count;
		}

		public override bool IsDBNull(int ordinal)
		{
			VerifyRead();
			return NativeMethods.sqlite3_column_type(m_statement, ordinal) == SQLiteColumnType.Null;
		}

		public override int FieldCount
		{
			get
			{
				VerifyNotDisposed();
				return m_hasRead ? m_columnType.Length : NativeMethods.sqlite3_column_count(m_statement);
			}
		}

		public override object this[int ordinal]
		{
			get { return GetValue(ordinal); }
		}

		public override object this[string name]
		{
			get { return GetValue(GetOrdinal(name)); }
		}

		public override bool HasRows
		{
			get
			{
				VerifyNotDisposed();
				return m_hasRead;
			}
		}

		public override int GetOrdinal(string name)
		{
			throw new NotImplementedException();
		}

		public override string GetDataTypeName(int ordinal)
		{
			throw new NotSupportedException();
		}

		public override Type GetFieldType(int ordinal)
		{
			throw new NotSupportedException();
		}

		public override object GetValue(int ordinal)
		{
			VerifyRead();
			if (ordinal < 0 || ordinal > FieldCount)
				throw new ArgumentOutOfRangeException("ordinal", "value must be between 0 and {0}.".FormatInvariant(FieldCount - 1));

			// determine (and cache) the declared type of the column (e.g., from the SQL schema)
			DbType dbType;
			if (m_columnType[ordinal].HasValue)
			{
				dbType = m_columnType[ordinal].Value;
			}
			else
			{
				IntPtr declType = NativeMethods.sqlite3_column_decltype(m_statement, ordinal);
				if (declType != IntPtr.Zero)
				{
					string type = SQLiteConnection.FromUtf8(declType);
					dbType = s_sqlTypeToDbType[type];
				}
				else
				{
					dbType = DbType.Object;
				}
				m_columnType[ordinal] = dbType;
			}

			var sqliteType = NativeMethods.sqlite3_column_type(m_statement, ordinal);
			if (dbType == DbType.Object)
				dbType = s_sqliteTypeToDbType[sqliteType];

			switch (sqliteType)
			{
			case SQLiteColumnType.Null:
				return DBNull.Value;

			case SQLiteColumnType.Blob:
				int byteCount = NativeMethods.sqlite3_column_bytes(m_statement, ordinal);
				byte[] bytes = new byte[byteCount];
				if (byteCount > 0)
				{
					IntPtr bytePointer = NativeMethods.sqlite3_column_blob(m_statement, ordinal);
					Marshal.Copy(bytePointer, bytes, 0, byteCount);
				}
				return dbType == DbType.Guid && byteCount == 16 ? (object) new Guid(bytes) : (object) bytes;

			case SQLiteColumnType.Double:
				double doubleValue = NativeMethods.sqlite3_column_double(m_statement, ordinal);
				return dbType == DbType.Single ? (object) (float) doubleValue : (object) doubleValue;

			case SQLiteColumnType.Integer:
				long integerValue = NativeMethods.sqlite3_column_int64(m_statement, ordinal);
				return dbType == DbType.Int32 ? (object) (int) integerValue :
					dbType == DbType.Boolean ? (object) (integerValue != 0) :
					dbType == DbType.Int16 ? (object) (short) integerValue :
					dbType == DbType.Byte ? (object) (byte) integerValue :
					dbType == DbType.Single ? (object) (float) integerValue :
					dbType == DbType.Double ? (object) (double) integerValue :
					(object) integerValue;

			case SQLiteColumnType.Text:
				string stringValue = SQLiteConnection.FromUtf8(NativeMethods.sqlite3_column_text(m_statement, ordinal));
				return dbType == DbType.DateTime ? (object) DateTime.ParseExact(stringValue, s_dateTimeFormats, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.None) :
					(object) stringValue;

			default:
				throw new InvalidOperationException();
			}
		}

		public override IEnumerator GetEnumerator()
		{
			throw new NotSupportedException();
		}

		public override DataTable GetSchemaTable()
		{
			throw new NotSupportedException();
		}

		public override int Depth
		{
			get { throw new NotSupportedException(); }
		}

		protected override DbDataReader GetDbDataReader(int ordinal)
		{
			throw new NotSupportedException();
		}

		public override Type GetProviderSpecificFieldType(int ordinal)
		{
			throw new NotSupportedException();
		}

		public override object GetProviderSpecificValue(int ordinal)
		{
			throw new NotSupportedException();
		}

		public override int GetProviderSpecificValues(object[] values)
		{
			throw new NotSupportedException();
		}

		public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		public override Task<bool> ReadAsync(CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		public override Task<bool> NextResultAsync(CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		public override int VisibleFieldCount
		{
			get { return FieldCount; }
		}

		private SqliteDatabaseHandle DatabaseHandle
		{
			get { return ((SQLiteConnection) m_command.Connection).Handle; }
		}

		private void Reset()
		{
			m_columnType = null;
			m_hasRead = false;
		}

		private void VerifyRead()
		{
			VerifyNotDisposed();
			if (!m_hasRead)
				throw new InvalidOperationException("Read must be called first.");
		}

		private void VerifyNotDisposed()
		{
			if (m_command == null)
				throw new ObjectDisposedException(GetType().Name);
		}

		private void BindBlob(int ordinal, byte[] blob)
		{
			NativeMethods.sqlite3_bind_blob(m_statement, ordinal, blob, blob.Length, s_sqliteTransient).ThrowOnError();
		}

		private void BindText(int ordinal, string text)
		{
			byte[] bytes = SQLiteConnection.ToUtf8(text);
			NativeMethods.sqlite3_bind_text(m_statement, ordinal, bytes, bytes.Length, s_sqliteTransient).ThrowOnError();
		}

		private static string ToString(DateTime dateTime)
		{
			// these are the System.Data.SQLite default format strings (from SQLiteConvert.cs)
			string formatString = dateTime.Kind == DateTimeKind.Utc ? "yyyy-MM-dd HH:mm:ss.FFFFFFFK" : "yyyy-MM-dd HH:mm:ss.FFFFFFF";
			return dateTime.ToString(formatString, CultureInfo.InvariantCulture);
		}

		sealed class ColumnInfo
		{
			public ColumnInfo(DbType dbType)
			{
				m_dbType = dbType;
			}

			public DbType DbType
			{
				get { return m_dbType; }
			}

			public SQLiteColumnType SqliteType { get; set; }

			readonly DbType m_dbType;
		}

		static readonly Dictionary<string, DbType> s_sqlTypeToDbType = new Dictionary<string, DbType>(StringComparer.OrdinalIgnoreCase)
		{
			{ "bigint", DbType.Int64 },
			{ "bit", DbType.Boolean },
			{ "blob", DbType.Binary },
			{ "bool", DbType.Boolean },
			{ "boolean", DbType.Boolean },
			{ "datetime", DbType.DateTime },
			{ "double", DbType.Double },
			{ "float", DbType.Double },
			{ "guid", DbType.Guid },
			{ "int", DbType.Int32 },
			{ "integer", DbType.Int64 },
			{ "long", DbType.Int64 },
			{ "single", DbType.Single},
			{ "string", DbType.String },
			{ "text", DbType.String },
		};

		static readonly Dictionary<SQLiteColumnType, DbType> s_sqliteTypeToDbType = new Dictionary<SQLiteColumnType, DbType>()
		{
			{ SQLiteColumnType.Integer, DbType.Int64 },
			{ SQLiteColumnType.Blob, DbType.Binary },
			{ SQLiteColumnType.Text, DbType.String },
			{ SQLiteColumnType.Double, DbType.Double },
			{ SQLiteColumnType.Null, DbType.Object }
		};

		static readonly string[] s_dateTimeFormats =
		{
			"THHmmssK",
			"THHmmK",
			"HH:mm:ss.FFFFFFFK",
			"HH:mm:ssK",
			"HH:mmK",
			"yyyy-MM-dd HH:mm:ss.FFFFFFFK",
			"yyyy-MM-dd HH:mm:ssK",
			"yyyy-MM-dd HH:mmK",
			"yyyy-MM-ddTHH:mm:ss.FFFFFFFK",
			"yyyy-MM-ddTHH:mmK",
			"yyyy-MM-ddTHH:mm:ssK",
			"yyyyMMddHHmmssK",
			"yyyyMMddHHmmK",
			"yyyyMMddTHHmmssFFFFFFFK",
			"THHmmss",
			"THHmm",
			"HH:mm:ss.FFFFFFF",
			"HH:mm:ss",
			"HH:mm",
			"yyyy-MM-dd HH:mm:ss.FFFFFFF",
			"yyyy-MM-dd HH:mm:ss",
			"yyyy-MM-dd HH:mm",
			"yyyy-MM-ddTHH:mm:ss.FFFFFFF",
			"yyyy-MM-ddTHH:mm",
			"yyyy-MM-ddTHH:mm:ss",
			"yyyyMMddHHmmss",
			"yyyyMMddHHmm",
			"yyyyMMddTHHmmssFFFFFFF",
			"yyyy-MM-dd",
			"yyyyMMdd",
			"yy-MM-dd"
		};

		static readonly IntPtr s_sqliteTransient = new IntPtr(-1);

		SQLiteCommand m_command;
		readonly CommandBehavior m_behavior;
		readonly int m_startingChanges;
		readonly byte[] m_commandBytes;
		SqliteStatementHandle m_statement;
		int m_bytesUsed;
		bool m_hasRead;
		DbType?[] m_columnType;
	}
}
