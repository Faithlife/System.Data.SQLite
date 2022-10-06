using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
#if DAPPER
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
#if NETFX_CORE
			m_path = Path.GetRandomFileName();
#else
			m_path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
#endif
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

		[Test]
		public void Deferred()
		{
			using var conn1 = new SQLiteConnection(m_csb.ConnectionString);
			conn1.Open();
			using var conn2 = new SQLiteConnection(m_csb.ConnectionString);
			conn2.Open();

			// both transactions should be able to make forward progress
			using var trans1 = conn1.BeginTransaction(deferred: true);
			using (var cmd1 = new SQLiteCommand(@"select 1;", conn1, trans1))
				Assert.AreEqual(1L, cmd1.ExecuteScalar());

			using var trans2 = conn2.BeginTransaction(deferred: true);
			using (var cmd2 = new SQLiteCommand(@"select 1;", conn2, trans2))
				Assert.AreEqual(1L, cmd2.ExecuteScalar());

			using (var cmd1 = new SQLiteCommand(@"select 1;", conn1, trans1))
				Assert.AreEqual(1L, cmd1.ExecuteScalar());

			trans1.Rollback();
			trans2.Rollback();
		}

#if !NETFX_CORE
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

		[Test, Timeout(2000), Ignore("Fails on Appveyor")]
		public void ConcurrentReads()
		{
			using (var conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				conn.Open();
				conn.Execute("create table Test (Id int primary key);");
				conn.Execute("insert into Test(Id) values(1), (2), (3), (4), (5), (6), (7), (8), (9), (10);");
			}

			const int  c_threadCount = 8;
			using (var barrier = new Barrier(c_threadCount))
				Task.WaitAll(Enumerable.Range(0, c_threadCount).Select(x => Task.Run(() => ConcurrentReadTask(barrier))).ToArray());
		}

		private void ConcurrentReadTask(Barrier barrier)
		{
			using (var conn = new SQLiteConnection(m_csb.ConnectionString))
			{
				conn.Open();
				barrier.SignalAndWait();

				int count = 0;

				using (var cmd = new SQLiteCommand("select Id from Test;", conn))
				using (var reader = cmd.ExecuteReader())
				{
					while (reader.Read())
					{
						count++;
						Thread.Sleep(TimeSpan.FromMilliseconds(100));
					} 
				}

				Assert.AreEqual(10, count);
			}
		}
#endif

		string m_path;
		SQLiteConnectionStringBuilder m_csb;
	}
}
