using System.Threading;

namespace System.Data.SQLite
{
	internal class Thread
	{
		public static void Sleep(int millisecondsTimeout)
		{
			using (var handle = new EventWaitHandle(false, EventResetMode.ManualReset))
			{
				handle.WaitOne(millisecondsTimeout);
			}
		}
	}
}
