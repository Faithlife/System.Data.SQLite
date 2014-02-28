using System.IO;
using NUnit.Framework;

namespace System.Data.SQLite.Tests
{
	[TestFixture]
	public class SqliteCommandTests
	{
		[SetUp]
		public void SetUp()
		{
			m_path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			m_csb = new SQLiteConnectionStringBuilder { DataSource = m_path, JournalMode = SQLiteJournalModeEnum.Truncate };
		}

		[TearDown]
		public void TearDown()
		{
			File.Delete(m_path);
		}

		[Test]
		public void DependentCommands()
		{
			using (SQLiteConnection conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				conn.Open();
				conn.ExecuteNonQuery(@"create table Test(Id integer not null primary key autoincrement, Value text not null);
					create index Test_Value on Test(Value);");
			}
		}

		[Test]
		public void MultipleResultSets()
		{
			using (SQLiteConnection conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				conn.Open();
				conn.ExecuteNonQuery(@"create table Test(Id integer not null primary key autoincrement, Value text not null);");
				conn.ExecuteNonQuery(@"insert into Test(Id, Value) values(1, 'one'), (2, 'two');");

				using (var cmd = (SQLiteCommand) conn.CreateCommand())
				{
					cmd.CommandText = @"select Value from Test where Id = @First; select Value from Test where Id = @Second;";
					cmd.Parameters.Add("First", DbType.Int64).Value = 1L;
					cmd.Parameters.Add("Second", DbType.Int64).Value = 2L;

					using (var reader = cmd.ExecuteReader())
					{
						Assert.IsTrue(reader.Read());
						Assert.AreEqual("one", reader.GetString(0));
						Assert.IsFalse(reader.Read());

						Assert.IsTrue(reader.NextResult());

						Assert.IsTrue(reader.Read());
						Assert.AreEqual("two", reader.GetString(0));
						Assert.IsFalse(reader.Read());

						Assert.IsFalse(reader.NextResult());
					}
				}
			}
		}

		[Test]
		public void RerunCommand()
		{
			using (SQLiteConnection conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				conn.Open();
				conn.ExecuteNonQuery(@"create table Test(Id integer not null primary key autoincrement, Value text not null);");
				conn.ExecuteNonQuery(@"insert into Test(Id, Value) values(1, 'one'), (2, 'two');");

				using (var cmd = (SQLiteCommand) conn.CreateCommand())
				{
					cmd.CommandText = @"select Value from Test where Id = @Id;";
					cmd.Parameters.Add("Id", DbType.Int64).Value = 1L;

					using (var reader = cmd.ExecuteReader())
					{
						Assert.IsTrue(reader.Read());
						Assert.AreEqual("one", reader.GetString(0));
						Assert.IsFalse(reader.Read());
						Assert.IsFalse(reader.NextResult());
					}

					using (var reader = cmd.ExecuteReader())
					{
						Assert.IsTrue(reader.Read());
						Assert.AreEqual("one", reader.GetString(0));
						Assert.IsFalse(reader.Read());
						Assert.IsFalse(reader.NextResult());
					}

					cmd.Parameters[0].Value = 2L;
					using (var reader = cmd.ExecuteReader())
					{
						Assert.IsTrue(reader.Read());
						Assert.AreEqual("two", reader.GetString(0));
						Assert.IsFalse(reader.Read());
						Assert.IsFalse(reader.NextResult());
					}
				}
			}
		}

		string m_path;
		SQLiteConnectionStringBuilder m_csb;
	}
}
