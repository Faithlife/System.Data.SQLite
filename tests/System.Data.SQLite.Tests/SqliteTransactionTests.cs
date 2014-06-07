using System.IO;
using System.Threading;
#if NET45
using Dapper;
#endif
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
			m_csb = new SQLiteConnectionStringBuilder { DataSource = m_path, JournalMode = SQLiteJournalModeEnum.Truncate };

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

		[Test, Timeout(5000)]
		public void OverlappingTransactions()
		{
			using (var conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				conn.Open();
				conn.Execute("create table Test (Id int primary key);");
				conn.Execute("insert into Test(Id) values(1)");
			}

			using (var barrier = new Barrier(2))
			{
				var threads = new[]
				{
					new Thread(ThreadProc1),
					new Thread(ThreadProc2),
				};
				foreach (var thread in threads)
					thread.Start(barrier);
				foreach (var thread in threads)
					thread.Join();
			}
		}

		private void ThreadProc1(object state)
		{
			var barrier = (Barrier) state;
			using (var conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				conn.Open();
				using (var trans = conn.BeginTransaction())
				{
					conn.Execute("select Id from Test", transaction: trans);
					barrier.SignalAndWait();

					conn.Execute("update Test set Id = 2;", transaction: trans);
					barrier.SignalAndWait();

					// give the other thread time to attempt begin the transaction, which will hang if both threads
					// try to write concurrently
					Thread.Sleep(TimeSpan.FromSeconds(2));
					trans.Commit();
				}
			}
		}

		private void ThreadProc2(object state)
		{
			var barrier = (Barrier) state;
			using (var conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				barrier.SignalAndWait();

				conn.Open();
				barrier.SignalAndWait();

				using (var trans = conn.BeginTransaction())
				{
					conn.Execute("select Id from Test", transaction: trans);
					conn.Execute("update Test set Id = 3;", transaction: trans);
					trans.Commit();
				}
			}
		}

		string m_path;
		SQLiteConnectionStringBuilder m_csb;
	}
}
