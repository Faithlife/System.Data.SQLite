using NUnit.Framework;

namespace System.Data.SQLite.Tests
{
	[SetUpFixture]
	public class MySetUpClass
	{
		[OneTimeSetUp]
		public void OneTimeSetUp()
		{
			// Access this property so that sqlite3_config is called, which needs to happen before any other sqlite3_* method calls.
			// This is necessary because SqliteTests.SubscribeUnsubscribeLog attaches an event handler to this property, which
			// won't work if it's not the first SQLite code that's run in a process.
			SQLiteLog.Log += Handler;
			SQLiteLog.Log -= Handler;

			static void Handler(object sender, LogEventArgs e)
			{
			}
		}
	}
}
