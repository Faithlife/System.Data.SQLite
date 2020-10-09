﻿using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace System.Data.SQLite
{
	public sealed class SQLiteDataReader : DbDataReader
	{
		private static readonly Regex PatternChar = new Regex(@"^(VAR)?CHAR\(([0-9]+)\)$");

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

		public override bool NextResult()
		{
			return NextResultAsyncCore(CancellationToken.None).Result;
		}

#if !PORTABLE
		public override Task<bool> NextResultAsync(CancellationToken cancellationToken)
		{
			return NextResultAsyncCore(cancellationToken);
		}
#endif

		private Task<bool> NextResultAsyncCore(CancellationToken cancellationToken)
		{
			VerifyNotDisposed();

			Reset();
			m_currentStatementIndex++;
			m_currentStatement = m_statementPreparer.Get(m_currentStatementIndex, cancellationToken);
			if (m_currentStatement == null)
				return s_falseTask;

			bool success = false;
			try
			{
				for (int i = 0; i < m_command.Parameters.Count; i++)
				{
					SQLiteParameter parameter = m_command.Parameters[i];
					string parameterName = parameter.ParameterName;
					int index;
					if (parameterName != null)
					{
						if (parameterName[0] != '@')
							parameterName = "@" + parameterName;
						index = NativeMethods.sqlite3_bind_parameter_index(m_currentStatement, SQLiteConnection.ToNullTerminatedUtf8(parameterName));
					}
					else
					{
						index = i + 1;
					}
					if (index > 0)
					{
						object value = parameter.Value;
						if (value == null || value.Equals(DBNull.Value))
							ThrowOnError(NativeMethods.sqlite3_bind_null(m_currentStatement, index));
						else if (value is int || (value is Enum && Enum.GetUnderlyingType(value.GetType()) == typeof(int)))
							ThrowOnError(NativeMethods.sqlite3_bind_int(m_currentStatement, index, (int) value));
						else if (value is bool)
							ThrowOnError(NativeMethods.sqlite3_bind_int(m_currentStatement, index, ((bool) value) ? 1 : 0));
						else if (value is string)
							BindText(index, (string) value);
						else if (value is byte[])
							BindBlob(index, (byte[]) value);
						else if (value is long)
							ThrowOnError(NativeMethods.sqlite3_bind_int64(m_currentStatement, index, (long) value));
						else if (value is float)
							ThrowOnError(NativeMethods.sqlite3_bind_double(m_currentStatement, index, (float) value));
						else if (value is double)
							ThrowOnError(NativeMethods.sqlite3_bind_double(m_currentStatement, index, (double) value));
						else if (value is DateTime)
							BindText(index, ToString((DateTime) value));
						else if (value is Guid)
							BindBlob(index, ((Guid) value).ToByteArray());
						else if (value is byte)
							ThrowOnError(NativeMethods.sqlite3_bind_int(m_currentStatement, index, (byte) value));
						else if (value is short)
							ThrowOnError(NativeMethods.sqlite3_bind_int(m_currentStatement, index, (short) value));
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

			return s_trueTask;
		}

		public override bool Read()
		{
			VerifyNotDisposed();
			return ReadAsyncCore(CancellationToken.None).Result;
		}

		internal static DbDataReader Create(SQLiteCommand command, CommandBehavior behavior)
		{
			DbDataReader dataReader = new SQLiteDataReader(command, behavior);
			dataReader.NextResult();
			return dataReader;
		}

#if !PORTABLE
		internal static async Task<DbDataReader> CreateAsync(SQLiteCommand command, CommandBehavior behavior, CancellationToken cancellationToken)
		{
			DbDataReader dataReader = new SQLiteDataReader(command, behavior);
			await dataReader.NextResultAsync(cancellationToken);
			return dataReader;
		}
#endif

		private SQLiteDataReader(SQLiteCommand command, CommandBehavior behavior)
		{
			m_command = command;
			m_behavior = behavior;
			m_statementPreparer = command.GetStatementPreparer();

			m_startingChanges = NativeMethods.sqlite3_total_changes(DatabaseHandle);
			m_currentStatementIndex = -1;
		}

		private Task<bool> ReadAsyncCore(CancellationToken cancellationToken)
		{
			using (cancellationToken.Register(s_interrupt, DatabaseHandle, useSynchronizationContext: false))
			{
				while (!cancellationToken.IsCancellationRequested)
				{
					SQLiteErrorCode errorCode = NativeMethods.sqlite3_step(m_currentStatement);

					switch (errorCode)
					{
					case SQLiteErrorCode.Done:
						Reset();
						return s_falseTask;

					case SQLiteErrorCode.Row:
						m_hasRead = true;
						if (m_columnType == null)
							m_columnType = new DbType?[NativeMethods.sqlite3_column_count(m_currentStatement)];
						return s_trueTask;

					case SQLiteErrorCode.Busy:
					case SQLiteErrorCode.Locked:
					case SQLiteErrorCode.CantOpen:
						if (cancellationToken.IsCancellationRequested)
							return s_canceledTask;
						Thread.Sleep(20);
						break;

					case SQLiteErrorCode.Interrupt:
						return s_canceledTask;

					default:
						throw new SQLiteException(errorCode, DatabaseHandle);
					}
				}
			}

			return cancellationToken.IsCancellationRequested ? s_canceledTask : s_trueTask;
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
			var sqliteType = NativeMethods.sqlite3_column_type(m_currentStatement, ordinal);
			if (sqliteType == SQLiteColumnType.Null)
				return 0;
			else if (sqliteType != SQLiteColumnType.Blob)
				throw new InvalidCastException("Cannot convert '{0}' to bytes.".FormatInvariant(sqliteType));

			int availableLength = NativeMethods.sqlite3_column_bytes(m_currentStatement, ordinal);
			if (buffer == null)
			{
				// this isn't required by the DbDataReader.GetBytes API documentation, but is what System.Data.SQLite does
				// (as does SqlDataReader: http://msdn.microsoft.com/en-us/library/system.data.sqlclient.sqldatareader.getbytes.aspx)
				return availableLength;
			}

			if (bufferOffset + length > buffer.Length)
				throw new ArgumentException("bufferOffset + length cannot exceed buffer.Length", "length");

			IntPtr ptr = NativeMethods.sqlite3_column_blob(m_currentStatement, ordinal);
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
			VerifyHasResult();
			if (ordinal < 0 || ordinal > FieldCount)
				throw new ArgumentOutOfRangeException("ordinal", "value must be between 0 and {0}.".FormatInvariant(FieldCount - 1));

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
			return NativeMethods.sqlite3_column_type(m_currentStatement, ordinal) == SQLiteColumnType.Null;
		}

		public override int FieldCount
		{
			get
			{
				VerifyNotDisposed();
				return m_hasRead ? m_columnType.Length : NativeMethods.sqlite3_column_count(m_currentStatement);
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
			VerifyHasResult();

			if (m_columnNames == null)
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
				IntPtr declType = NativeMethods.sqlite3_column_decltype(m_currentStatement, ordinal);
				if (declType != IntPtr.Zero)
				{
					string type = SQLiteConnection.FromUtf8(declType);

					// Fixed CHAR(n) or variable VARCHAR(n) type
					Match match = PatternChar.Match(type);
					if(match.Success)
					{
						dbType = DbType.String;
					}
					else if (!s_sqlTypeToDbType.TryGetValue(type, out dbType))
						throw new NotSupportedException("The data type name '{0}' is not supported.".FormatInvariant(type));
				}
				else
				{
					dbType = DbType.Object;
				}
				m_columnType[ordinal] = dbType;
			}

			var sqliteType = NativeMethods.sqlite3_column_type(m_currentStatement, ordinal);
			if (dbType == DbType.Object)
				dbType = s_sqliteTypeToDbType[sqliteType];

			switch (sqliteType)
			{
			case SQLiteColumnType.Null:
				return DBNull.Value;

			case SQLiteColumnType.Blob:
				int byteCount = NativeMethods.sqlite3_column_bytes(m_currentStatement, ordinal);
				byte[] bytes = new byte[byteCount];
				if (byteCount > 0)
				{
					IntPtr bytePointer = NativeMethods.sqlite3_column_blob(m_currentStatement, ordinal);
					Marshal.Copy(bytePointer, bytes, 0, byteCount);
				}
				return dbType == DbType.Guid && byteCount == 16 ? (object) new Guid(bytes) : (object) bytes;

			case SQLiteColumnType.Double:
				double doubleValue = NativeMethods.sqlite3_column_double(m_currentStatement, ordinal);
				return dbType == DbType.Single ? (object) (float) doubleValue : (object) doubleValue;

			case SQLiteColumnType.Integer:
				long integerValue = NativeMethods.sqlite3_column_int64(m_currentStatement, ordinal);
				return dbType == DbType.Int32 ? (object) (int) integerValue :
					dbType == DbType.Boolean ? (object) (integerValue != 0) :
					dbType == DbType.Int16 ? (object) (short) integerValue :
					dbType == DbType.Byte ? (object) (byte) integerValue :
					dbType == DbType.Single ? (object) (float) integerValue :
					dbType == DbType.Double ? (object) (double) integerValue :
					(object) integerValue;

			case SQLiteColumnType.Text:
				int stringLength = NativeMethods.sqlite3_column_bytes(m_currentStatement, ordinal);
				string stringValue = SQLiteConnection.FromUtf8(NativeMethods.sqlite3_column_text(m_currentStatement, ordinal), stringLength);
				return dbType == DbType.DateTime ? (object) DateTime.ParseExact(stringValue, s_dateTimeFormats, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.AdjustToUniversal) :
					(object) stringValue;

			default:
				throw new InvalidOperationException();
			}
		}

#if !PORTABLE
		public override IEnumerator GetEnumerator()
		{
			throw new NotSupportedException();
		}

		public override DataTable GetSchemaTable()
		{
			throw new NotSupportedException();
		}
#endif

		public override int Depth
		{
			get { throw new NotSupportedException(); }
		}

#if !PORTABLE
		protected override DbDataReader GetDbDataReader(int ordinal)
		{
			throw new NotSupportedException();
		}
#endif

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

#if !PORTABLE
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
			VerifyNotDisposed();

			return ReadAsyncCore(cancellationToken);
		}
#endif

		public override int VisibleFieldCount
		{
			get { return FieldCount; }
		}

		private SqliteDatabaseHandle DatabaseHandle
		{
			get { return ((SQLiteConnection) m_command.Connection).Handle; }
		}

		private static readonly Action<object> s_interrupt = obj => NativeMethods.sqlite3_interrupt((SqliteDatabaseHandle) obj);

		private void Reset()
		{
			if (m_currentStatement != null)
				NativeMethods.sqlite3_reset(m_currentStatement);
			m_currentStatement = null;
			m_columnNames = null;
			m_columnType = null;
			m_hasRead = false;
		}

		private void VerifyHasResult()
		{
			VerifyNotDisposed();
			if (m_currentStatement == null)
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
			if (m_command == null)
				throw new ObjectDisposedException(GetType().Name);
		}

		private void BindBlob(int ordinal, byte[] blob)
		{
			ThrowOnError(NativeMethods.sqlite3_bind_blob(m_currentStatement, ordinal, blob, blob.Length, s_sqliteTransient));
		}

		private void BindText(int ordinal, string text)
		{
			byte[] bytes = SQLiteConnection.ToUtf8(text);
			ThrowOnError(NativeMethods.sqlite3_bind_text(m_currentStatement, ordinal, bytes, bytes.Length, s_sqliteTransient));
		}

		private void ThrowOnError(SQLiteErrorCode errorCode)
		{
			if (errorCode != SQLiteErrorCode.Ok)
				throw new SQLiteException(errorCode, DatabaseHandle);
		}

		private static string ToString(DateTime dateTime)
		{
			// these are the System.Data.SQLite default format strings (from SQLiteConvert.cs)
			string formatString = dateTime.Kind == DateTimeKind.Utc ? "yyyy-MM-dd HH:mm:ss.FFFFFFFK" : "yyyy-MM-dd HH:mm:ss.FFFFFFF";
			return dateTime.ToString(formatString, CultureInfo.InvariantCulture);
		}

		private static Task<bool> CreateCanceledTask()
		{
			var source = new TaskCompletionSource<bool>();
			source.SetCanceled();
			return source.Task;
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
		static readonly Task<bool> s_canceledTask = CreateCanceledTask();
		static readonly Task<bool> s_falseTask = Task.FromResult(false);
		static readonly Task<bool> s_trueTask = Task.FromResult(true);

		SQLiteCommand m_command;
		readonly CommandBehavior m_behavior;
		readonly int m_startingChanges;
		SqliteStatementPreparer m_statementPreparer;
		int m_currentStatementIndex;
		SqliteStatementHandle m_currentStatement;
		bool m_hasRead;
		DbType?[] m_columnType;
		Dictionary<string, int> m_columnNames;
	}
}
