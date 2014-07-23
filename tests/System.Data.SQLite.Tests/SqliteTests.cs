﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
#if NET45
using Dapper;
#endif
using NUnit.Framework;
#if NETFX_CORE
using Windows.Storage;
#endif

namespace System.Data.SQLite.Tests
{
	[TestFixture]
	public class SqliteTests
	{
		[SetUp]
		public void SetUp()
		{
#if NETFX_CORE
			m_path = Path.GetRandomFileName();
#else
			m_path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
#endif
			m_csb = new SQLiteConnectionStringBuilder { DataSource = m_path };
		}

		[TearDown]
		public void TearDown()
		{
			File.Delete(m_path);
		}

		[Test]
		public void ReadOnly()
		{
			using (SQLiteConnection conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				conn.Open();
				conn.Execute("create table Test (TestId int primary key);");
			}

			File.SetAttributes(m_path, FileAttributes.ReadOnly);

			using (SQLiteConnection conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				try
				{
					conn.Open();
					Assert.Fail("Didn't throw exception");
				}
				catch (SQLiteException ex)
				{
					Assert.AreEqual((int) SQLiteErrorCode.ReadOnly, ex.ErrorCode);
				}
			}

			m_csb.ReadOnly = true;
			using (SQLiteConnection conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				try
				{
					conn.Open();
				}
				catch (SQLiteException ex)
				{
					Assert.Fail("Threw exception: {0}", ex.Message);
				}
			}

			File.SetAttributes(m_path, FileAttributes.Normal);
			File.Delete(m_path);
		}

		[Test]
		public void TypeMapping()
		{
			using (SQLiteConnection conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				conn.Open();
				conn.Execute(@"create table Test (
Id integer primary key,
String text,
Int32 int not null,
NullableInt32 int,
Int64 integer not null,
NullableInt64 integer,
Bool bool not null,
NullableBool bool
);");
				conn.Execute(@"insert into Test (Id, String, Int32, NullableInt32, Int64, NullableInt64, Bool, NullableBool)
values(1, 'two', 3, 4, 5, 6, 1, 0);");

				using (var reader = conn.ExecuteReader(@"select * from Test"))
				{
					Assert.IsTrue(reader.Read());
					object[] values = new object[8];
					Assert.AreEqual(8, reader.GetValues(values));
					Assert.AreEqual(1L, (long) values[0]);
					Assert.AreEqual("two", (string) values[1]);
					Assert.AreEqual(3, (int) values[2]);
					Assert.AreEqual(4, (int) values[3]);
					Assert.AreEqual(5L, (long) values[4]);
					Assert.AreEqual(6L, (long) values[5]);
					Assert.AreEqual(true, (bool) values[6]);
					Assert.AreEqual(false, (bool) values[7]);
				}
			}
		}

		[Test]
		public void ExecuteNonQuery()
		{
			using (SQLiteConnection conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				conn.Open();
				conn.Execute(@"create table Test (Id integer primary key, String text);");
				using (var cmd = new SQLiteCommand(@"insert into Test(Id, String) values(1, 'test'); select last_insert_rowid()", conn))
					Assert.AreEqual(1L, (long) cmd.ExecuteScalar());
				using (var cmd = new SQLiteCommand(@"insert into Test(Id, String) values(123, 'test'); select last_insert_rowid()", conn))
					Assert.AreEqual(123L, (long) cmd.ExecuteScalar());
				Assert.AreEqual(1, conn.Execute(@"insert into Test(Id, String) values(2, 'two'); select last_insert_rowid()"));
				Assert.AreEqual(2, conn.Execute(@"insert into Test(Id, String) values(3, 'three'), (4, 'four'); select last_insert_rowid()"));
			}
		}

		[Test]
		public void GetNameAndOrdinal()
		{
			using (SQLiteConnection conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				conn.Open();
				conn.Execute(@"create table Test (Id integer primary key, String text); insert into Test(Id, String) values(1, 'one'), (2, 'two'), (3, 'three');");
				using (var cmd = new SQLiteCommand(@"select String, Id from Test", conn))
				using (var reader = cmd.ExecuteReader())
				{
					Assert.AreEqual(2, reader.FieldCount);
					Assert.AreEqual("String", reader.GetName(0));
					Assert.AreEqual("Id", reader.GetName(1));
					Assert.AreEqual(0, reader.GetOrdinal("String"));
					Assert.AreEqual(0, reader.GetOrdinal("string"));
					Assert.AreEqual(1, reader.GetOrdinal("ID"));
					Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetName(3));
					Assert.Throws<IndexOutOfRangeException>(() => reader.GetOrdinal("fail"));

					while (reader.Read())
					{
					}
				}
			}
		}

		[TestCase("test")]
		[TestCase("foo\0bar")]
		public void NamedParameterStringValue(string value)
		{
			using (SQLiteConnection conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				conn.Open();
				conn.Execute(@"create table Test (Id integer primary key, String text);");
				using (var cmd = new SQLiteCommand(@"insert into Test(Id, String) values(1, @value)", conn))
				{
					var param = cmd.Parameters.Add("value", DbType.String);
					param.Value = value;
					cmd.ExecuteNonQuery();
				}
				using (var reader = conn.ExecuteReader(@"select String from Test where Id = 1"))
				{
					Assert.IsTrue(reader.Read());
					Assert.AreEqual(value, reader.GetString(0));
				}
			}
		}

#if NET45
		[Test]
		public void IndexedParameters()
		{
			using (SQLiteConnection conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				conn.Open();
				conn.Execute(@"create table Test (Id integer primary key, String text);");
				using (var cmd = new SQLiteCommand(@"insert into Test(Id, String) values(?, ?)", conn))
				{
					cmd.Parameters.Add(new SQLiteParameter { DbType = DbType.Int32, Value = 1 });
					cmd.Parameters.Add(new SQLiteParameter { DbType = DbType.String, Value = "test" });
					cmd.ExecuteNonQuery();
				}
				using (var reader = conn.ExecuteReader(@"select String from Test where Id = 1"))
				{
					Assert.IsTrue(reader.Read());
					Assert.AreEqual("test", reader.GetString(0));
				}
			}
		}

		[Test]
		public void Dapper()
		{
			using (SQLiteConnection conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				conn.Open();
				conn.Execute(@"create table Test (Id integer primary key, String text); insert into Test(Id, String) values(1, 'one'), (2, 'two'), (3, 'three');");
				var results = conn.Query<long>("select Id from Test where length(String) = @len", new { len = 3 }).ToList();
				CollectionAssert.AreEqual(new long[] { 1, 2 }, results);
			}
		}
#endif

#if !NETFX_CORE
		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		[TestCase(10)]
		[TestCase(100)]
		[TestCase(250)]
		public void CancelExecuteReader(int milliseconds)
		{
			using (SQLiteConnection conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				conn.Open();
				conn.Execute(@"create table Test (Id integer primary key);");
				using (var trans = conn.BeginTransaction())
				{
					for (long value = 0; value < 1000; value++)
						conn.Execute(@"insert into Test(Id) values(@value)", new { value }, trans);
					trans.Commit();
				}
				CancellationTokenSource source = new CancellationTokenSource(TimeSpan.FromMilliseconds(milliseconds));
				using (var cmd = new SQLiteCommand(@"select a.Id, b.Id, c.Id, d.Id from Test a inner join Test b inner join Test c inner join Test d", conn))
				using (var reader = cmd.ExecuteReaderAsync(source.Token).Result)
				{
					Task<bool> task;
					do
					{
						task = reader.ReadAsync(source.Token);
						Assert.IsTrue(task.IsCanceled || task.Result);
					} while (!task.IsCanceled);
					Assert.IsTrue(task.IsCanceled);
				}
			}
		}
#endif

		[Test]
#if MONOANDROID
		[Ignore]
#endif
		public void SubscribeUnsubscribeLog()
		{
			int logCount = 0;
			SQLiteErrorCode lastErrorCode = SQLiteErrorCode.Ok;

			using (SQLiteConnection conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				conn.Open();

				try
				{
					conn.Execute(@"create table []");
				}
				catch (SQLiteException)
				{
				}

				SQLiteLogEventHandler handler = (s, e) =>
				{
					logCount++;
					lastErrorCode = (SQLiteErrorCode) e.ErrorCode;
				};
				SQLiteLog.Log += handler;

				try
				{
					conn.Execute(@"create table []");
				}
				catch (SQLiteException)
				{
				}

				Assert.AreEqual(1, logCount);
				Assert.AreEqual(SQLiteErrorCode.Error, lastErrorCode);

				SQLiteLog.Log -= handler;
				lastErrorCode = SQLiteErrorCode.Ok;

				try
				{
					conn.Execute(@"create table []");
				}
				catch (SQLiteException)
				{
				}

				Assert.AreEqual(1, logCount);
				Assert.AreEqual(SQLiteErrorCode.Ok, lastErrorCode);
			}
		}

		[Test]
		public void BackUpDatabase()
		{
			using (SQLiteConnection disk = new SQLiteConnection(m_csb.ConnectionString))
			using (SQLiteConnection memory = new SQLiteConnection("Data Source=:memory:"))
			{
				disk.Open();
				disk.Execute(@"create table Test (Id integer primary key, String text); insert into Test(Id, String) values(1, 'one'), (2, 'two'), (3, 'three');");

				memory.Open();
				disk.BackupDatabase(memory, "main", "main", -1, null, 0);

				using (var reader = memory.ExecuteReader("select Id from Test where length(String) = @len", new { len = 3 }))
				{
					var results = reader.ReadAll<long>().ToList();
					Assert.That(results, Is.EqualTo(new long[] { 1, 2 }));
				}
			}
		}

		// these test cases illustrate the type conversions allowed by this wrapper; the extra conversions permitted by the official System.Data.SQLite wrapper are given in the comments following each test case
		[TestCase("bool", false, "Boolean", false)] // System.Data.SQLite: "Byte", (byte) 0, "Double", 0.0, "Float", 0.0f, "Int16", (short) 0, "Int32", 0, "Int64", 0L
		[TestCase("bool", true, "Boolean", true)] // System.Data.SQLite: "Byte", (byte) 1, "Double", 1.0, "Float", 1.0f, "Int16", (short) 1, "Int32", 1, "Int64", 1L
		[TestCase("double", 1.0, "Double", 1.0)] // System.Data.SQLite: "Float", 1.0f
		[TestCase("double", 1.5, "Double", 1.5)] // System.Data.SQLite: "Float", 1.0f
		[TestCase("double", 4E+38, "Double", 4E+38)] // System.Data.SQLite: "Float", float.PositiveInfinity
		[TestCase("double", -4E+38, "Double", -4E+38)] // System.Data.SQLite: "Float", float.NegativeInfinity
		[TestCase("double", null)]
		[TestCase("int", 0, "Int32", 0, "Int64", 0L)] // System.Data.SQLite: "Boolean", false, "Byte", (byte) 0, "Double", 0.0, "Float", 0.0f, "Int16", (short) 0
		[TestCase("int", 1, "Int32", 1, "Int64", 1L)] // System.Data.SQLite: "Boolean", true, "Byte", (byte) 1, "Double", 1.0, "Float", 1.0f, "Int16", (short) 1
		[TestCase("int", -2147483648, "Int32", -2147483648, "Int64", -2147483648L)] // System.Data.SQLite: "Boolean", true, "Double", -2147483648.0, "Float", -2147483648.0f
		[TestCase("int", 2147483647, "Int32", 2147483647, "Int64", 2147483647L)] // System.Data.SQLite: "Boolean", true, "Double", 2147483647.0, "Float", 2147483647.0f
		[TestCase("int", null)]
		[TestCase("integer", 2L, "Int32", 2, "Int64", 2L)] // System.Data.SQLite: "Boolean", true, "Byte", (byte) 2, "Double", 2.0, "Float", 2.0f, "Int16", (short) 2, "Int32", 2
		[TestCase("integer", 9223372036854775807, "Int64", 9223372036854775807L)] // System.Data.SQLite: "Boolean", true, "Double", 9223372036854775807.0, "Float", 9223372036854775807.0f, "Int16", (short) -1, "Int32", -1, 
		[TestCase("integer", -9223372036854775808, "Int64", -9223372036854775808L)] // System.Data.SQLite: "Boolean", true, "Byte", (byte) 0, "Double", -9223372036854775808.0, "Float", -9223372036854775808.0f, "Int16", (short) 0, "Int32", 0, 
		[TestCase("integer", null)]
		[TestCase("single", 1.0f, "Double", 1.0, "Float", 1.0f)] // System.Data.SQLite: "Boolean", true, "Byte", (byte) 1, "Double", 1.0, "Float", 1.0f, "Int16", (short) 1, "Int32", 1, "Int64", 1L
		[TestCase("single", 1.5f, "Double", 1.5, "Float", 1.5f)]
		[TestCase("single", -1.0f, "Double", -1.0, "Float", -1.0f)]
		[TestCase("single", null)]
		[TestCase("text", "3", "String", "3")] // System.Data.SQLite: "Char", '\u0003', 
		[TestCase("text", "65", "String", "65")] // System.Data.SQLite: "Char", 'A', 
		[TestCase("text", "three", "String", "three")] // System.Data.SQLite: "Char", '\u0000', 
		[TestCase("text", null)]
		public void TypeConversion(string columnType, object value, params object[] typesAndValues)
		{
			using (SQLiteConnection conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				conn.Open();
				conn.Execute(@"create table Test (Id integer primary key autoincrement, Value {0});".FormatInvariant(columnType));
				conn.Execute(@"insert into Test (Value) values(@value);", new { value });

				// test that GetValue returns the right value
				using (var reader = conn.ExecuteReader(@"select Value from Test"))
				{
					Assert.IsTrue(reader.Read());
					object actual = reader.GetValue(0);
					Assert.AreEqual(value ?? DBNull.Value, actual);
				}

				// test that each specified GetX method returns the right value
				foreach (var typeAndValue in GetTypesAndValues(typesAndValues))
				{
					using (var reader = conn.ExecuteReader(@"select Value from Test"))
					{
						Assert.IsTrue(reader.Read());
						var methodInfo = reader.GetType().GetMethod("Get" + typeAndValue.Key);
						object actual = methodInfo.Invoke(reader, new object[] { 0 });
						object expected = typeAndValue.Value ?? DBNull.Value;
						if (expected == DBNull.Value)
						{
							Assert.IsNull(actual);
						}
						else
						{
							Assert.AreEqual(expected, actual);
							Assert.AreEqual(expected.GetType(), actual.GetType());
						}
					}
				}

				// test that all other GetX methods throw
				foreach (var type in s_availableTypes.Except(GetTypesAndValues(typesAndValues).Select(x => x.Key)))
				{
					using (var reader = conn.ExecuteReader(@"select Value from Test"))
					{
						Assert.IsTrue(reader.Read());
						var methodInfo = reader.GetType().GetMethod("Get" + type);
						try
						{
							methodInfo.Invoke(reader, new object[] { 0 });
							Assert.Fail("No exception thrown for {0}".FormatInvariant(type));
						}
						catch (TargetInvocationException ex)
						{
							Assert.IsTrue(new[] { typeof(InvalidCastException), typeof(FormatException), typeof(OverflowException) }.Contains(ex.InnerException.GetType()));
						}
					}
				}
			}
		}

		private IEnumerable<KeyValuePair<string, object>> GetTypesAndValues(object[] typesAndValues)
		{
			for (int i = 0; i < typesAndValues.Length; i += 2)
				yield return new KeyValuePair<string, object>((string) typesAndValues[i], typesAndValues[i + 1]);
		}

		static readonly string[] s_availableTypes =
		{
			"Boolean",
			"Byte",
			"Char",
			"DateTime",
			//// "Decimal",
			"Double",
			"Float",
			"Guid",
			"Int16",
			"Int32",
			"Int64",
			"String"
		};

		string m_path;
		SQLiteConnectionStringBuilder m_csb;
	}
}
