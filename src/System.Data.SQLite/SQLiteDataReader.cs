#if NET5_0
using System.Buffers;
#endif
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Data.SQLite
{
	public sealed class SQLiteDataReader : DbDataReader
	{
		public override void Close()
		{
			// NOTE: DbDataReader.Dispose calls Close, so we can't put our logic in Dispose(bool) and call Dispose() from this method.
			Reset();
			Utility.Dispose(ref m_statementPreparer);

			if (m_behavior.HasFlag(CommandBehavior.CloseConnection))
			{
				var dbConnection = m_command.Connection;
				m_command.Dispose();
				dbConnection.Dispose();
			}

			m_command = null;
		}

		public override bool NextResult() => NextResultAsyncCore(CancellationToken.None);

		public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => NextResultAsyncCore(cancellationToken) ? s_trueTask : s_falseTask;

		private bool NextResultAsyncCore(CancellationToken cancellationToken)
		{
			VerifyNotDisposed();

			Reset();
			m_currentStatementIndex++;
			m_currentStatement = m_statementPreparer.Get(m_currentStatementIndex, cancellationToken);
			if (m_currentStatement is null)
				return false;

			var success = false;
			try
			{
				for (int i = 0; i < m_command.Parameters.Count; i++)
				{
					var parameter = m_command.Parameters[i];
					var parameterName = parameter.ParameterName;
					int index;
					if (parameterName is not null)
					{
#if NET5_0
						static unsafe int GetParameterIndex(SqliteStatementHandle currentStatement, string parameterName)
						{
							var utf8Length = Encoding.UTF8.GetByteCount(parameterName) + 2;

							const int c_stackAllocationThreshold = 1024;
							byte[] pooled = null;
							Span<byte> span = utf8Length <= c_stackAllocationThreshold ?
								stackalloc byte[utf8Length] :
								(pooled = ArrayPool<byte>.Shared.Rent(utf8Length));

							var parameterNameBytes = span;
							if (parameterName[0] != '@')
							{
								parameterNameBytes[0] = (byte) '@';
								parameterNameBytes = parameterNameBytes.Slice(1);
							}
							var byteCount = Encoding.UTF8.GetBytes(parameterName.AsSpan(), parameterNameBytes);
							parameterNameBytes[byteCount] = 0;

							int index;
							fixed (byte* spanBytes = span)
							{
								index = NativeMethods.sqlite3_bind_parameter_index(currentStatement, spanBytes);
							}

							if (pooled is not null)
								ArrayPool<byte>.Shared.Return(pooled);

							return index;
						}
						index = GetParameterIndex(m_currentStatement, parameterName);
#else
						if (parameterName[0] != '@')
							parameterName = "@" + parameterName;
						index = NativeMethods.sqlite3_bind_parameter_index(m_currentStatement, SQLiteConnection.ToNullTerminatedUtf8(parameterName));
#endif
					}
					else
					{
						index = i + 1;
					}
					if (index > 0)
					{
						object value = parameter.Value;
						if (value is null || value.Equals(DBNull.Value))
							ThrowOnError(NativeMethods.sqlite3_bind_null(m_currentStatement, index));
						else if (value is int || (value is Enum && Enum.GetUnderlyingType(value.GetType()) == typeof(int)))
							ThrowOnError(NativeMethods.sqlite3_bind_int(m_currentStatement, index, (int) value));
						else if (value is bool boolValue)
							ThrowOnError(NativeMethods.sqlite3_bind_int(m_currentStatement, index, boolValue ? 1 : 0));
						else if (value is string stringValue)
							BindText(index, stringValue);
#if NET5_0
						else if (value is Memory<byte> memory)
							BindBlob(index, memory.Span);
						else if (value is ReadOnlyMemory<byte> readOnlyMemory)
							BindBlob(index, readOnlyMemory.Span);
						else if (value is byte[] byteArrayValue)
							BindBlob(index, byteArrayValue.AsSpan());
						else if (value is ArraySegment<byte> arraySegment)
							BindBlob(index, arraySegment.AsSpan());
						else if (value is Guid guidValue)
							BindBlob(index, guidValue.ToByteArray().AsSpan());
#else
						else if (value is byte[] byteArrayValue)
							BindBlob(index, byteArrayValue, 0, byteArrayValue.Length);
						else if (value is ArraySegment<byte> arraySegment)
							BindBlob(index, arraySegment.Array, arraySegment.Offset, arraySegment.Count);
						else if (value is Guid guidValue)
							BindBlob(index, guidValue.ToByteArray(), 0, 16);
#endif
						else if (value is long longValue)
							ThrowOnError(NativeMethods.sqlite3_bind_int64(m_currentStatement, index, longValue));
						else if (value is float floatValue)
							ThrowOnError(NativeMethods.sqlite3_bind_double(m_currentStatement, index, floatValue));
						else if (value is double doubleValue)
							ThrowOnError(NativeMethods.sqlite3_bind_double(m_currentStatement, index, doubleValue));
						else if (value is DateTime dateTimeValue)
							BindText(index, ToString(dateTimeValue));
						else if (value is byte byteValue)
							ThrowOnError(NativeMethods.sqlite3_bind_int(m_currentStatement, index, byteValue));
						else if (value is short shortValue)
							ThrowOnError(NativeMethods.sqlite3_bind_int(m_currentStatement, index, shortValue));
						else
							BindText(index, Convert.ToString(value, CultureInfo.InvariantCulture));
					}
				}

				success = true;
			}
			finally
			{
				if (!success)
					ThrowOnError(NativeMethods.sqlite3_reset(m_currentStatement));
			}

			return true;
		}

		public override bool Read()
		{
			VerifyNotDisposed();
			return ReadAsyncCore(CancellationToken.None);
		}

		internal static DbDataReader Create(SQLiteCommand command, CommandBehavior behavior)
		{
			DbDataReader dataReader = new SQLiteDataReader(command, behavior);
			dataReader.NextResult();
			return dataReader;
		}

		internal static async Task<DbDataReader> CreateAsync(SQLiteCommand command, CommandBehavior behavior, CancellationToken cancellationToken)
		{
			DbDataReader dataReader = new SQLiteDataReader(command, behavior);
			await dataReader.NextResultAsync(cancellationToken);
			return dataReader;
		}

		private SQLiteDataReader(SQLiteCommand command, CommandBehavior behavior)
		{
			m_command = command;
			m_behavior = behavior;
			m_statementPreparer = command.GetStatementPreparer();

			m_startingChanges = NativeMethods.sqlite3_total_changes(DatabaseHandle);
			m_currentStatementIndex = -1;
		}

		private bool ReadAsyncCore(CancellationToken cancellationToken)
		{
			using (cancellationToken.Register(s_interrupt, DatabaseHandle, useSynchronizationContext: false))
			{
				while (!cancellationToken.IsCancellationRequested)
				{
					var errorCode = NativeMethods.sqlite3_step(m_currentStatement);

					switch (errorCode)
					{
					case SQLiteErrorCode.Done:
						Reset();
						return false;

					case SQLiteErrorCode.Row:
						m_hasRead = true;
						if (m_columnType is null)
						{
							var columnCount = NativeMethods.sqlite3_column_count(m_currentStatement);
							m_columnType = new DbType?[columnCount];
							m_sqliteColumnType = new SQLiteColumnType[columnCount];
						}
						else
						{
#if NET5_0
							m_sqliteColumnType.AsSpan().Clear();
#else
							Array.Clear(m_sqliteColumnType, 0, m_sqliteColumnType.Length);
#endif
						}
						return true;

					case SQLiteErrorCode.Busy:
					case SQLiteErrorCode.Locked:
					case SQLiteErrorCode.CantOpen:
						cancellationToken.ThrowIfCancellationRequested();
						Thread.Sleep(20);
						break;

					case SQLiteErrorCode.Interrupt:
						// should always throw because s_interrupt will have been invoked already
						cancellationToken.ThrowIfCancellationRequested();
						return false;

					default:
						throw new SQLiteException(errorCode, DatabaseHandle);
					}
				}
			}

			cancellationToken.ThrowIfCancellationRequested();
			return true;
		}

		public override bool IsClosed => m_command is null;

		public override int RecordsAffected => NativeMethods.sqlite3_total_changes(DatabaseHandle) - m_startingChanges;

		public override bool GetBoolean(int ordinal)
		{
			if (ordinal < 0 || ordinal > FieldCount)
				throw new ArgumentOutOfRangeException(nameof(ordinal), "value must be between 0 and {0}.".FormatInvariant(FieldCount - 1));
			var sqliteType = GetSqliteColumnType(ordinal);
			if (sqliteType != SQLiteColumnType.Integer)
				throw new InvalidCastException("Cannot convert {0} to bool.".FormatInvariant(sqliteType));
			var dbType = GetDbType(ordinal);
			if (dbType is not (DbType.Boolean or DbType.Object))
				throw new InvalidCastException("Cannot convert {0} to bool.".FormatInvariant(dbType));
			return NativeMethods.sqlite3_column_int64(m_currentStatement, ordinal) != 0;
		}

		public override byte GetByte(int ordinal)
		{
			if (ordinal < 0 || ordinal > FieldCount)
				throw new ArgumentOutOfRangeException(nameof(ordinal), "value must be between 0 and {0}.".FormatInvariant(FieldCount - 1));
			var sqliteType = GetSqliteColumnType(ordinal);
			if (sqliteType != SQLiteColumnType.Integer)
				throw new InvalidCastException("Cannot convert {0} to byte.".FormatInvariant(sqliteType));
			var dbType = GetDbType(ordinal);
			if (dbType is not (DbType.Byte or DbType.Object))
				throw new InvalidCastException("Cannot convert {0} to byte.".FormatInvariant(dbType));
			return checked((byte) NativeMethods.sqlite3_column_int64(m_currentStatement, ordinal));
		}

		public override unsafe long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
		{
			if (ordinal < 0 || ordinal > FieldCount)
				throw new ArgumentOutOfRangeException(nameof(ordinal), "value must be between 0 and {0}.".FormatInvariant(FieldCount - 1));
			var sqliteType = GetSqliteColumnType(ordinal);
			if (sqliteType == SQLiteColumnType.Null)
				return 0;
			else if (sqliteType != SQLiteColumnType.Blob)
				throw new InvalidCastException("Cannot convert {0} to bytes.".FormatInvariant(sqliteType));

			IntPtr ptr = NativeMethods.sqlite3_column_blob(m_currentStatement, ordinal);
			int columnLength = NativeMethods.sqlite3_column_bytes(m_currentStatement, ordinal);
			if (buffer is null)
			{
				// this isn't required by the DbDataReader.GetBytes API documentation, but is what System.Data.SQLite does
				// (as does SqlDataReader: http://msdn.microsoft.com/en-us/library/system.data.sqlclient.sqldatareader.getbytes.aspx)
				return columnLength;
			}
			if (bufferOffset + length > buffer.Length)
				throw new ArgumentException("bufferOffset + length cannot exceed buffer.Length", "length");

			int lengthToCopy = Math.Min(columnLength - (int) dataOffset, length);
#if NET5_0
			new ReadOnlySpan<byte>(ptr.ToPointer(), columnLength).CopyTo(buffer.AsSpan(bufferOffset, lengthToCopy));
#else
			Marshal.Copy(new IntPtr(ptr.ToInt64() + dataOffset), buffer, bufferOffset, lengthToCopy);
#endif
			return lengthToCopy;
		}

		public override char GetChar(int ordinal) => (char) GetValue(ordinal);

		public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length) => throw new NotImplementedException();

		public override unsafe Guid GetGuid(int ordinal)
		{
#if NET5_0
			if (ordinal < 0 || ordinal > FieldCount)
				throw new ArgumentOutOfRangeException(nameof(ordinal), "value must be between 0 and {0}.".FormatInvariant(FieldCount - 1));
			var sqliteType = GetSqliteColumnType(ordinal);
			if (sqliteType != SQLiteColumnType.Blob)
				throw new InvalidCastException("Cannot convert {0} to Guid.".FormatInvariant(sqliteType));
			var dbType = GetDbType(ordinal);
			if (dbType is not (DbType.Guid or DbType.Object))
				throw new InvalidCastException("Cannot convert {0} to Guid.".FormatInvariant(dbType));
			var ptr = NativeMethods.sqlite3_column_blob(m_currentStatement, ordinal);
			var length = NativeMethods.sqlite3_column_bytes(m_currentStatement, ordinal);
			if (length != 16)
				throw new InvalidCastException("Cannot convert BLOB with length {0} to Guid.".FormatInvariant(length));
			return new Guid(new ReadOnlySpan<byte>(ptr.ToPointer(), length));
#else
			return (Guid) GetValue(ordinal);
#endif
		}

		public override short GetInt16(int ordinal)
		{
			if (ordinal < 0 || ordinal > FieldCount)
				throw new ArgumentOutOfRangeException(nameof(ordinal), "value must be between 0 and {0}.".FormatInvariant(FieldCount - 1));
			var sqliteType = GetSqliteColumnType(ordinal);
			if (sqliteType != SQLiteColumnType.Integer)
				throw new InvalidCastException("Cannot convert {0} to short.".FormatInvariant(sqliteType));
			var dbType = GetDbType(ordinal);
			if (dbType is not (DbType.Int16 or DbType.Object))
				throw new InvalidCastException("Cannot convert {0} to short.".FormatInvariant(dbType));
			return checked((short) NativeMethods.sqlite3_column_int64(m_currentStatement, ordinal));
		}

		public override int GetInt32(int ordinal)
		{
			if (ordinal < 0 || ordinal > FieldCount)
				throw new ArgumentOutOfRangeException(nameof(ordinal), "value must be between 0 and {0}.".FormatInvariant(FieldCount - 1));
			var sqliteType = GetSqliteColumnType(ordinal);
			if (sqliteType != SQLiteColumnType.Integer)
				throw new InvalidCastException("Cannot convert {0} to int.".FormatInvariant(sqliteType));
			var dbType = GetDbType(ordinal);
			if (dbType is not (DbType.Int32 or DbType.Int64 or DbType.Int16 or DbType.Object))
				throw new InvalidCastException("Cannot convert {0} to int.".FormatInvariant(dbType));
			return checked((int) NativeMethods.sqlite3_column_int64(m_currentStatement, ordinal));
		}

		public override long GetInt64(int ordinal)
		{
			if (ordinal < 0 || ordinal > FieldCount)
				throw new ArgumentOutOfRangeException(nameof(ordinal), "value must be between 0 and {0}.".FormatInvariant(FieldCount - 1));
			var sqliteType = GetSqliteColumnType(ordinal);
			if (sqliteType != SQLiteColumnType.Integer)
				throw new InvalidCastException("Cannot convert {0} to long.".FormatInvariant(sqliteType));
			var dbType = GetDbType(ordinal);
			if (dbType is not (DbType.Int64 or DbType.Int32 or DbType.Int16 or DbType.Object))
				throw new InvalidCastException("Cannot convert {0} to long.".FormatInvariant(dbType));
			return NativeMethods.sqlite3_column_int64(m_currentStatement, ordinal);
		}

		public override DateTime GetDateTime(int ordinal) => (DateTime) GetValue(ordinal);

		public override string GetString(int ordinal) => (string) GetValue(ordinal);

		public override decimal GetDecimal(int ordinal) => throw new NotImplementedException();

		public override double GetDouble(int ordinal)
		{
			if (ordinal < 0 || ordinal > FieldCount)
				throw new ArgumentOutOfRangeException(nameof(ordinal), "value must be between 0 and {0}.".FormatInvariant(FieldCount - 1));
			var sqliteType = GetSqliteColumnType(ordinal);
			if (sqliteType is not (SQLiteColumnType.Double or SQLiteColumnType.Integer))
				throw new InvalidCastException("Cannot convert {0} to double.".FormatInvariant(sqliteType));
			var dbType = GetDbType(ordinal);
			if (dbType is not (DbType.Double or DbType.Single or DbType.Object))
				throw new InvalidCastException("Cannot convert {0} to double.".FormatInvariant(dbType));
			return NativeMethods.sqlite3_column_double(m_currentStatement, ordinal);
		}

		public override float GetFloat(int ordinal)
		{
			if (ordinal < 0 || ordinal > FieldCount)
				throw new ArgumentOutOfRangeException(nameof(ordinal), "value must be between 0 and {0}.".FormatInvariant(FieldCount - 1));
			var sqliteType = GetSqliteColumnType(ordinal);
			if (sqliteType is not (SQLiteColumnType.Double or SQLiteColumnType.Integer))
				throw new InvalidCastException("Cannot convert {0} to single.".FormatInvariant(sqliteType));
			var dbType = GetDbType(ordinal);
			if (dbType is not (DbType.Single or DbType.Object))
				throw new InvalidCastException("Cannot convert {0} to single.".FormatInvariant(dbType));
			return checked((float) NativeMethods.sqlite3_column_double(m_currentStatement, ordinal));
		}

		public override string GetName(int ordinal)
		{
			VerifyHasResult();
			if (ordinal < 0 || ordinal > FieldCount)
				throw new ArgumentOutOfRangeException(nameof(ordinal), "value must be between 0 and {0}.".FormatInvariant(FieldCount - 1));

			return SQLiteConnection.FromUtf8(NativeMethods.sqlite3_column_name(m_currentStatement, ordinal)); 
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
			return GetSqliteColumnType(ordinal) == SQLiteColumnType.Null;
		}

		public override int FieldCount
		{
			get
			{
				VerifyNotDisposed();
				return m_hasRead ? m_columnType.Length : NativeMethods.sqlite3_column_count(m_currentStatement);
			}
		}

		public override object this[int ordinal] => GetValue(ordinal);

		public override object this[string name] => GetValue(GetOrdinal(name));

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
			VerifyHasResult();

			if (m_columnNames is null)
			{
				var columnNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
				for (int i = 0; i < FieldCount; i++)
				{
					string columnName = SQLiteConnection.FromUtf8(NativeMethods.sqlite3_column_name(m_currentStatement, i));
					columnNames[columnName] = i;
				}
				m_columnNames = columnNames;
			}

			int ordinal;
			if (!m_columnNames.TryGetValue(name, out ordinal))
				throw new IndexOutOfRangeException("The column name '{0}' does not exist in the result set.".FormatInvariant(name));
			return ordinal;
		}

		public override string GetDataTypeName(int ordinal) => throw new NotSupportedException();

		public override Type GetFieldType(int ordinal) => throw new NotSupportedException();

#if NET5_0
		/// <summary>
		/// Returns a <see cref="ReadOnlySpan{T}"/> for the BLOB data in the specified column.
		/// </summary>
		/// <param name="ordinal">The column to read.</param>
		/// <returns>A <see cref="ReadOnlySpan{T}"/> wrapping native memory for the column data. This value is only valid
		/// until the next row is read.</returns>
		public unsafe ReadOnlySpan<byte> GetReadOnlySpan(int ordinal)
		{
			var sqliteType = GetSqliteColumnType(ordinal);
			if (sqliteType != SQLiteColumnType.Blob && sqliteType != SQLiteColumnType.Text)
				throw new InvalidCastException("Cannot convert {0} to bytes.".FormatInvariant(sqliteType));

			var ptr = NativeMethods.sqlite3_column_blob(m_currentStatement, ordinal);
			var length = NativeMethods.sqlite3_column_bytes(m_currentStatement, ordinal);
			return new ReadOnlySpan<byte>(ptr.ToPointer(), length);
		}
#endif

		public override object GetValue(int ordinal)
		{
			VerifyRead();
			if (ordinal < 0 || ordinal > FieldCount)
				throw new ArgumentOutOfRangeException(nameof(ordinal), "value must be between 0 and {0}.".FormatInvariant(FieldCount - 1));

			var sqliteType = GetSqliteColumnType(ordinal);
			var dbType = GetDbType(ordinal);
			if (dbType == DbType.Object)
				dbType = s_sqliteTypeToDbType[sqliteType];

			switch (sqliteType)
			{
			case SQLiteColumnType.Null:
				return DBNull.Value;

			case SQLiteColumnType.Blob:
				var bytePointer = NativeMethods.sqlite3_column_blob(m_currentStatement, ordinal);
				var byteCount = NativeMethods.sqlite3_column_bytes(m_currentStatement, ordinal);
				var bytes = new byte[byteCount];
				if (byteCount > 0)
					Marshal.Copy(bytePointer, bytes, 0, byteCount);
				return dbType == DbType.Guid && byteCount == 16 ? (object) new Guid(bytes) : (object) bytes;

			case SQLiteColumnType.Double:
				var doubleValue = NativeMethods.sqlite3_column_double(m_currentStatement, ordinal);
				return dbType == DbType.Single ? (object) (float) doubleValue : doubleValue;

			case SQLiteColumnType.Integer:
				var integerValue = NativeMethods.sqlite3_column_int64(m_currentStatement, ordinal);
				return dbType == DbType.Int32 ? (object) (int) integerValue :
					dbType == DbType.Boolean ? (object) (integerValue != 0) :
					dbType == DbType.Int16 ? (object) (short) integerValue :
					dbType == DbType.Byte ? (object) (byte) integerValue :
					dbType == DbType.Single ? (object) (float) integerValue :
					dbType == DbType.Double ? (object) (double) integerValue :
					(object) integerValue;

			case SQLiteColumnType.Text:
				var stringPointer = NativeMethods.sqlite3_column_text(m_currentStatement, ordinal);
				var stringLength = NativeMethods.sqlite3_column_bytes(m_currentStatement, ordinal);
				var stringValue = SQLiteConnection.FromUtf8(stringPointer, stringLength);
				return dbType == DbType.DateTime ? (object) DateTime.ParseExact(stringValue, s_dateTimeFormats, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.AdjustToUniversal) :
					(object) stringValue;

			default:
				throw new InvalidOperationException();
			}
		}

		public override T GetFieldValue<T>(int ordinal)
		{
			if (typeof(T) == typeof(bool))
				return (T) (object) GetBoolean(ordinal);
			if (typeof(T) == typeof(byte))
				return (T) (object) GetByte(ordinal);
			if (typeof(T) == typeof(short))
				return (T) (object) GetInt16(ordinal);
			if (typeof(T) == typeof(int))
				return (T) (object) GetInt32(ordinal);
			if (typeof(T) == typeof(long))
				return (T) (object) GetInt64(ordinal);
			if (typeof(T) == typeof(char))
				return (T) (object) GetChar(ordinal);
			if (typeof(T) == typeof(decimal))
				return (T) (object) GetDecimal(ordinal);
			if (typeof(T) == typeof(double))
				return (T) (object) GetDouble(ordinal);
			if (typeof(T) == typeof(float))
				return (T) (object) GetFloat(ordinal);
			if (typeof(T) == typeof(string))
				return (T) (object) GetString(ordinal);
			if (typeof(T) == typeof(DateTime))
				return (T) (object) GetDateTime(ordinal);
			if (typeof(T) == typeof(Guid))
				return (T) (object) GetGuid(ordinal);
			return (T) GetValue(ordinal);
		}

		public override IEnumerator GetEnumerator() => throw new NotSupportedException();

		public override DataTable GetSchemaTable() => throw new NotSupportedException();

		public override int Depth => throw new NotSupportedException();

		protected override DbDataReader GetDbDataReader(int ordinal) => throw new NotSupportedException();

		public override Type GetProviderSpecificFieldType(int ordinal) => throw new NotSupportedException();

		public override object GetProviderSpecificValue(int ordinal) => throw new NotSupportedException();

		public override int GetProviderSpecificValues(object[] values) => throw new NotSupportedException();

		public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken) => throw new NotSupportedException();

		public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken) => throw new NotSupportedException();

		public override Task<bool> ReadAsync(CancellationToken cancellationToken)
		{
			VerifyNotDisposed();
			try
			{
				return ReadAsyncCore(cancellationToken) ? s_trueTask : s_falseTask;
			}
			catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
			{
				return Task.FromCanceled<bool>(cancellationToken);
			}
		}

		public override int VisibleFieldCount => FieldCount;

		// Determine (and cache) the declared type of the column (e.g., from the SQL schema)
		internal DbType GetDbType(int ordinal)
		{
			if (m_columnType[ordinal].HasValue)
				return m_columnType[ordinal].Value;

			DbType dbType;
			IntPtr declType = NativeMethods.sqlite3_column_decltype(m_currentStatement, ordinal);
			if (declType != IntPtr.Zero)
			{
#if NET5_0
				if (GetDbType(declType) is DbType dbTypeValue)
					dbType = dbTypeValue;
				else
					throw new NotSupportedException("The data type name '{0}' is not supported.".FormatInvariant(SQLiteConnection.FromUtf8(declType)));
#else
				string type = SQLiteConnection.FromUtf8(declType);
				if (!s_sqlTypeToDbType.TryGetValue(type, out dbType))
					throw new NotSupportedException("The data type name '{0}' is not supported.".FormatInvariant(type));
#endif
			}
			else
			{
				dbType = DbType.Object;
			}
			m_columnType[ordinal] = dbType;
			return dbType;
		}

		private SQLiteColumnType GetSqliteColumnType(int ordinal)
		{
			if (m_sqliteColumnType[ordinal] == default)
				m_sqliteColumnType[ordinal] = NativeMethods.sqlite3_column_type(m_currentStatement, ordinal);
			return m_sqliteColumnType[ordinal];
		}

		private SqliteDatabaseHandle DatabaseHandle => ((SQLiteConnection) m_command.Connection).Handle;

		private static readonly Action<object> s_interrupt = obj => NativeMethods.sqlite3_interrupt((SqliteDatabaseHandle) obj);

		private void Reset()
		{
			if (m_currentStatement is not null)
				NativeMethods.sqlite3_reset(m_currentStatement);
			m_currentStatement = null;
			m_columnNames = null;
			m_columnType = null;
			m_hasRead = false;
		}

		private void VerifyHasResult()
		{
			VerifyNotDisposed();
			if (m_currentStatement is null)
				throw new InvalidOperationException("There is no current result set.");
		}

		private void VerifyRead()
		{
			VerifyHasResult();
			if (!m_hasRead)
				throw new InvalidOperationException("Read must be called first.");
		}

		private void VerifyNotDisposed()
		{
			if (m_command is null)
				throw new ObjectDisposedException(GetType().Name);
		}

#if NET5_0
		private unsafe void BindBlob(int ordinal, ReadOnlySpan<byte> blob)
		{
			if (blob.Length == 0)
			{
				byte temp;
				ThrowOnError(NativeMethods.sqlite3_bind_blob(m_currentStatement, ordinal, &temp, 0, s_sqliteTransient));
			}
			else
			{
				fixed (byte* p = blob)
					ThrowOnError(NativeMethods.sqlite3_bind_blob(m_currentStatement, ordinal, p, blob.Length, s_sqliteTransient));
			}
		}
#else
		private unsafe void BindBlob(int ordinal, byte[] blob, int offset, int length)
		{
			if (length == 0)
			{
				byte temp;
				ThrowOnError(NativeMethods.sqlite3_bind_blob(m_currentStatement, ordinal, &temp, 0, s_sqliteTransient));
			}
			else
			{
				fixed (byte* p = &blob[offset])
					ThrowOnError(NativeMethods.sqlite3_bind_blob(m_currentStatement, ordinal, p, length, s_sqliteTransient));
			}
		}
#endif

		private unsafe void BindText(int ordinal, string text)
		{
#if NET5_0
			// add one for a NULL terminator so that an empty string is converted to a non-empty array, so that a NULL pointer isn't passed to native code
			var utf8Length = Encoding.UTF8.GetByteCount(text) + 1;

			const int c_stackAllocationThreshold = 1024;
			byte[] pooled = null;
			Span<byte> span = utf8Length <= c_stackAllocationThreshold ?
				stackalloc byte[utf8Length] :
				(pooled = ArrayPool<byte>.Shared.Rent(utf8Length));

			var byteCount = Encoding.UTF8.GetBytes(text.AsSpan(), span);

			fixed (byte* spanBytes = span)
			{
				ThrowOnError(NativeMethods.sqlite3_bind_text(m_currentStatement, ordinal, spanBytes, byteCount, s_sqliteTransient));
			}

			if (pooled is not null)
				ArrayPool<byte>.Shared.Return(pooled);
#else
			var bytes = SQLiteConnection.ToUtf8(text);
			ThrowOnError(NativeMethods.sqlite3_bind_text(m_currentStatement, ordinal, bytes, bytes.Length, s_sqliteTransient));
#endif
		}

		private void ThrowOnError(SQLiteErrorCode errorCode)
		{
			if (errorCode != SQLiteErrorCode.Ok)
				throw new SQLiteException(errorCode, DatabaseHandle);
		}

		private static string ToString(DateTime dateTime)
		{
			// these are the System.Data.SQLite default format strings (from SQLiteConvert.cs)
			var formatString = dateTime.Kind == DateTimeKind.Utc ? "yyyy-MM-dd HH:mm:ss.FFFFFFFK" : "yyyy-MM-dd HH:mm:ss.FFFFFFF";
			return dateTime.ToString(formatString, CultureInfo.InvariantCulture);
		}

#if NET5_0
		// Must be kept in sync with s_sqlTypeToDbType.
		private static DbType? GetDbType(IntPtr declType)
		{
			var typeBytes = SQLiteConnection.GetUtf8Span(declType);
			if (typeBytes.Length >= 2)
			{
				switch (typeBytes[0])
				{
				case 0x42 or 0x62: // B
					switch (typeBytes[1])
					{
					case 0x49 or 0x69: // I
						if (typeBytes.Length == 3 && typeBytes[2] is 0x54 or 0x74) // BIT
							return DbType.Boolean;
						else if (typeBytes.Length == 6 && typeBytes[2] is 0x47 or 0x67 && typeBytes[3] is 0x49 or 0x69 && typeBytes[4] is 0x4E or 0x6E && typeBytes[5] is 0x54 or 0x74) // BIGINT
							return DbType.Int64;
						break;

					case 0x4F or 0x6F: // O
						if (typeBytes.Length == 4 && typeBytes[2] is 0x4F or 0x6F && typeBytes[3] is 0x4C or 0x6C) // BOOL
							return DbType.Boolean;
						if (typeBytes.Length == 7 && typeBytes[2] is 0x4F or 0x6F && typeBytes[3] is 0x4C or 0x6C && typeBytes[4] is 0x45 or 0x65 && typeBytes[5] is 0x41 or 0x61 && typeBytes[6] is 0x4E or 0x6E) // BOOLEAN
							return DbType.Boolean;
						break;

					case 0x4C or 0x6C: // L
						if (typeBytes.Length == 4 && typeBytes[2] is 0x4F or 0x6F && typeBytes[3] is 0x42 or 0x62) // BLOB
							return DbType.Binary;
						break;
					}
					break;

				case 0x44 or 0x64: // D
					switch (typeBytes[1])
					{
					case 0x41 or 0x61: // A
						if (typeBytes.Length == 8 && typeBytes[2] is 0x54 or 0x74 && typeBytes[3] is 0x45 or 0x65 && typeBytes[4] is 0x54 or 0x74 && typeBytes[5] is 0x49 or 0x69 && typeBytes[6] is 0x4D or 0x6D && typeBytes[7] is 0x45 or 0x65) // DATETIME
							return DbType.DateTime;
						break;

					case 0x4F or 0x6F: // O
						if (typeBytes.Length == 6 && typeBytes[2] is 0x55 or 0x75 && typeBytes[3] is 0x42 or 0x62 && typeBytes[4] is 0x4C or 0x6C && typeBytes[5] is 0x45 or 0x65) // DOUBLE
							return DbType.Double;
						break;
					}
					break;

				case 0x46 or 0x66: // F
					if (typeBytes.Length == 5 && typeBytes[1] is 0x4C or 0x6C && typeBytes[2] is 0x4F or 0x6F && typeBytes[3] is 0x41 or 0x61 && typeBytes[4] is 0x54 or 0x74) // FLOAT
						return DbType.Double;
					break;

				case 0x47 or 0x67: // G
					if (typeBytes.Length == 4 && typeBytes[1] is 0x55 or 0x75 && typeBytes[2] is 0x49 or 0x69 && typeBytes[3] is 0x44 or 0x64) // GUID
						return DbType.Guid;
					break;

				case 0x49 or 0x69: // I
					switch (typeBytes[1])
					{
					case 0x4E or 0x6E: // N
						if (typeBytes.Length == 3 && typeBytes[2] is 0x54 or 0x74) // INT
							return DbType.Int32;
						if (typeBytes.Length == 7 && typeBytes[2] is 0x54 or 0x74 && typeBytes[3] is 0x45 or 0x65 && typeBytes[4] is 0x47 or 0x67 && typeBytes[5] is 0x45 or 0x65 && typeBytes[6] is 0x52 or 0x72) // INTEGER
							return DbType.Int64;
						break;
					}
					break;

				case 0x4C or 0x6C: // L
					if (typeBytes.Length == 4 && typeBytes[1] is 0x4F or 0x6F && typeBytes[2] is 0x4E or 0x6E && typeBytes[3] is 0x47 or 0x67) // LONG
						return DbType.Int64;
					break;

				case 0x52 or 0x72: // R
					if (typeBytes.Length == 4 && typeBytes[1] is 0x45 or 0x65 && typeBytes[2] is 0x41 or 0x61 && typeBytes[3] is 0x4C or 0x6C) // REAL
						return DbType.Double;
					break;

				case 0x53 or 0x73: // S
					switch (typeBytes[1])
					{
					case 0x49 or 0x69: // I
						if (typeBytes.Length == 6 && typeBytes[2] is 0x4E or 0x6E && typeBytes[3] is 0x47 or 0x67 && typeBytes[4] is 0x4C or 0x6C && typeBytes[5] is 0x45 or 0x65) // SINGLE
							return DbType.Single;
						break;

					case 0x54 or 0x74: // T
						if (typeBytes.Length == 6 && typeBytes[2] is 0x52 or 0x72 && typeBytes[3] is 0x49 or 0x69 && typeBytes[4] is 0x4E or 0x6E && typeBytes[5] is 0x47 or 0x67) // STRING
							return DbType.String;
						break;
					}
					break;

				case 0x54 or 0x74: // T
					if (typeBytes.Length == 4 && typeBytes[1] is 0x45 or 0x65 && typeBytes[2] is 0x58 or 0x78 && typeBytes[3] is 0x54 or 0x74) // TEXT
						return DbType.String;
					break;
				}
			}
			
			return default;
		}
#endif

		// Must be kept in sync with GetDbType.
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
			{ "real", DbType.Double },
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
		static readonly Task<bool> s_falseTask = Task.FromResult(false);
		static readonly Task<bool> s_trueTask = Task.FromResult(true);

		SQLiteCommand m_command;
		readonly CommandBehavior m_behavior;
		readonly int m_startingChanges;
		SqliteStatementPreparer m_statementPreparer;
		int m_currentStatementIndex;
		SqliteStatementHandle m_currentStatement;
		bool m_hasRead;
		SQLiteColumnType[] m_sqliteColumnType;
		DbType?[] m_columnType;
		Dictionary<string, int> m_columnNames;
	}
}
