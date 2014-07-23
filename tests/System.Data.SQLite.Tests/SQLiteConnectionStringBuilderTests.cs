using NUnit.Framework;

namespace System.Data.SQLite.Tests
{
	[TestFixture]
	public class SQLiteConnectionStringBuilderTests
	{
		[Test]
		public void Defaults()
		{
			var csb = new SQLiteConnectionStringBuilder();
			Assert.AreEqual(0, csb.CacheSize);
			Assert.AreEqual("", csb.DataSource);
			Assert.AreEqual(0, csb.DefaultTimeout);
			Assert.IsFalse(csb.FailIfMissing);
			Assert.IsFalse(csb.ForeignKeys);
			Assert.AreEqual(SQLiteJournalModeEnum.Default, csb.JournalMode);
			Assert.AreEqual(0, csb.PageSize);
			Assert.IsNull(csb.Password);
			Assert.IsFalse(csb.ReadOnly);
			Assert.AreEqual(SynchronizationModes.Normal, csb.SyncMode);
		}

		[Test]
		public void ParseConnectionString()
		{
			var csb = new SQLiteConnectionStringBuilder { ConnectionString = "Data Source=\"C:\\temp\\test.db\";Synchronous=full;Read Only=true;FailIfMissing=True;Foreign Keys=true;Cache Size=6000;Page Size=2048;default timeout=30;password=S3kr3t" };
			Assert.AreEqual(6000, csb.CacheSize);
			Assert.AreEqual(@"C:\temp\test.db", csb.DataSource);
			Assert.AreEqual(30, csb.DefaultTimeout);
			Assert.IsTrue(csb.FailIfMissing);
			Assert.IsTrue(csb.ForeignKeys);
			Assert.AreEqual(SQLiteJournalModeEnum.Default, csb.JournalMode);
			Assert.AreEqual(2048, csb.PageSize);
			Assert.AreEqual("S3kr3t", csb.Password);
			Assert.IsTrue(csb.ReadOnly);
			Assert.AreEqual(SynchronizationModes.Full, csb.SyncMode);
		}

		[TestCase("1000", 1000)]
		[TestCase("4000", 4000)]
		[TestCase("FALSE", 0, ExpectedException = typeof(FormatException))]
		[TestCase(null, 0)]
		[TestCase("", 0)]
		public void CacheSize(string text, int expected)
		{
			SQLiteConnectionStringBuilder csb = new SQLiteConnectionStringBuilder { ConnectionString = "Cache Size=" + text };
			Assert.AreEqual(expected, csb.CacheSize);
		}

		[TestCase("Data Source", "C:\\temp\\test.db")]
		[TestCase("DATA SOURCE", "database.sqlite")]
		[TestCase("data source", "semi;colon.db")]
		public void DataSource(string keyName, string dataSource)
		{
			SQLiteConnectionStringBuilder csb = new SQLiteConnectionStringBuilder { ConnectionString = keyName + "=\"" + dataSource + "\";Read Only=false" };
			Assert.AreEqual(dataSource, csb.DataSource);
		}

		[TestCase("30", 30)]
		[TestCase("86400", 86400)]
		[TestCase("FALSE", 0, ExpectedException = typeof(FormatException))]
		[TestCase(null, 0)]
		[TestCase("", 0)]
		public void DefaultTimeout(string text, int expected)
		{
			SQLiteConnectionStringBuilder csb = new SQLiteConnectionStringBuilder { ConnectionString = "Default Timeout=" + text };
			Assert.AreEqual(expected, csb.DefaultTimeout);
		}

		[TestCase("True", true)]
		[TestCase("true", true)]
		[TestCase("FALSE", false)]
		[TestCase("null", false, ExpectedException = typeof(FormatException))]
		public void FailIfMissing(string text, bool expected)
		{
			SQLiteConnectionStringBuilder csb = new SQLiteConnectionStringBuilder { ConnectionString = "FailIfMissing=" + text };
			Assert.AreEqual(expected, csb.FailIfMissing);
		}

		[TestCase("True", true)]
		[TestCase("true", true)]
		[TestCase("FALSE", false)]
		[TestCase("null", false, ExpectedException = typeof(FormatException))]
		public void ForeignKeys(string text, bool expected)
		{
			SQLiteConnectionStringBuilder csb = new SQLiteConnectionStringBuilder { ConnectionString = "Foreign Keys=" + text };
			Assert.AreEqual(expected, csb.ForeignKeys);
		}

		[TestCase("truncate", SQLiteJournalModeEnum.Truncate)]
		[TestCase("TRUNCATE", SQLiteJournalModeEnum.Truncate)]
		[TestCase("", SQLiteJournalModeEnum.Default)]
		[TestCase("default", SQLiteJournalModeEnum.Default)]
		[TestCase(null, SQLiteJournalModeEnum.Default)]
		[TestCase("off", SQLiteJournalModeEnum.Off)]
		[TestCase("Persist", SQLiteJournalModeEnum.Persist)]
		[TestCase("memory", SQLiteJournalModeEnum.Memory)]
		[TestCase("WAL", SQLiteJournalModeEnum.Wal)]
		public void JournalMode(string text, SQLiteJournalModeEnum expected)
		{
			SQLiteConnectionStringBuilder csb = new SQLiteConnectionStringBuilder { ConnectionString = "Journal Mode=" + text };
			Assert.AreEqual(expected, csb.JournalMode);
		}

		[TestCase("1048576", 1048576)]
		[TestCase("4294967296", 4294967296L)]
		[TestCase("FALSE", 0, ExpectedException = typeof(FormatException))]
		[TestCase(null, 0)]
		[TestCase("", 0)]
		public void MmapSize(string text, long expected)
		{
			SQLiteConnectionStringBuilder csb = new SQLiteConnectionStringBuilder { ConnectionString = "_MmapSize=" + text };
			Assert.AreEqual(expected, csb.MmapSize);
		}

		[TestCase("1024", 1024)]
		[TestCase("4096", 4096)]
		[TestCase("FALSE", 0, ExpectedException = typeof(FormatException))]
		[TestCase(null, 0)]
		[TestCase("", 0)]
		public void PageSize(string text, int expected)
		{
			SQLiteConnectionStringBuilder csb = new SQLiteConnectionStringBuilder { ConnectionString = "Page Size=" + text };
			Assert.AreEqual(expected, csb.PageSize);
		}

		[TestCase("True", true)]
		[TestCase("true", true)]
		[TestCase("FALSE", false)]
		[TestCase("null", false, ExpectedException = typeof(FormatException))]
		public void ReadOnly(string text, bool expected)
		{
			SQLiteConnectionStringBuilder csb = new SQLiteConnectionStringBuilder { ConnectionString = "Read Only=" + text };
			Assert.AreEqual(expected, csb.ReadOnly);
		}

		[TestCase("off", SynchronizationModes.Off)]
		[TestCase("OFF", SynchronizationModes.Off)]
		[TestCase("normal", SynchronizationModes.Normal)]
		[TestCase("full", SynchronizationModes.Full)]
		[TestCase("", SynchronizationModes.Normal)]
		[TestCase(null, SynchronizationModes.Normal)]
		public void Synchronous(string text, SynchronizationModes expected)
		{
			SQLiteConnectionStringBuilder csb = new SQLiteConnectionStringBuilder { ConnectionString = "Synchronous=" + text };
			Assert.AreEqual(expected, csb.SyncMode);
		}
	}
}
