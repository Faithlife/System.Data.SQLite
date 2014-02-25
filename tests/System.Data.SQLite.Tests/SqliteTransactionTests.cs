using System.IO;
using Dapper;
using NUnit.Framework;

namespace System.Data.SQLite.Tests
{
	[TestFixture]
	public class SqliteTransactionTests
	{
		[SetUp]
		public void SetUp()
		{
			m_path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			m_csb = new SQLiteConnectionStringBuilder { DataSource = m_path };

			using (SQLiteConnection conn = new SQLiteConnection(m_csb.ConnectionString))
			{
			}
		}

		[TearDown]
		public void TearDown()
		{
			File.Delete(m_path);
		}

		[Test]
		public void Commit()
		{
			using (var conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				conn.Open();
				conn.Execute("create table Test (Id int primary key);");

				using (var trans = conn.BeginTransaction())
				{
					conn.Execute("insert into Test(Id) values(1)", transaction: trans);
					trans.Commit();
				}

				using (var cmd = new SQLiteCommand(@"select count(Id) from Test", conn))
					Assert.AreEqual(1L, (long) cmd.ExecuteScalar());
			}
		}

		[Test]
		public void Rollback()
		{
			using (var conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				conn.Open();
				conn.Execute("create table Test (Id int primary key);");

				using (var trans = conn.BeginTransaction())
				{
					conn.Execute("insert into Test(Id) values(1)", transaction: trans);
					trans.Rollback();
				}

				using (var cmd = new SQLiteCommand(@"select count(Id) from Test", conn))
					Assert.AreEqual(0L, (long) cmd.ExecuteScalar());
			}
		}

		[Test]
		public void Dispose()
		{
			using (var conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				conn.Open();
				conn.Execute("create table Test (Id int primary key);");

				using (var trans = conn.BeginTransaction())
				{
					conn.Execute("insert into Test(Id) values(1)", transaction: trans);
				}

				using (var cmd = new SQLiteCommand(@"select count(Id) from Test", conn))
					Assert.AreEqual(0L, (long) cmd.ExecuteScalar());
			}
		}

		string m_path;
		SQLiteConnectionStringBuilder m_csb;
	}
}
