namespace System.Data.SQLite
{
	public static class SQLiteLog
	{
		/// <summary>
		/// This event is raised whenever SQLite raises a logging event.
		/// Note that this should be set as one of the first things in the
		/// application.
		/// </summary>
		public static event SQLiteLogEventHandler Log
		{
			add
			{
				lock (s_lock)
					Handlers += value;
			}
			remove
			{
				lock (s_lock)
					Handlers -= value;
			}
		}

		internal static void Initialize()
		{
			// reference a static field to force the static constructor to run
			GC.KeepAlive(s_lock);
		}

		static SQLiteLog()
		{
			NativeMethods.sqlite3_config_log(SQLiteConfigOpsEnum.SQLITE_CONFIG_LOG, s_callback, IntPtr.Zero);
		}

#if MONOTOUCH
		[MonoTouch.MonoPInvokeCallback(typeof(SQLiteLogCallback))]
#elif XAMARIN_IOS
		[ObjCRuntime.MonoPInvokeCallback(typeof(SQLiteLogCallback))]
#endif
		static void LogCallback(IntPtr pUserData, int errorCode, IntPtr pMessage)
		{
			lock (s_lock)
			{
				if (Handlers != null)
					Handlers.Invoke(null, new LogEventArgs(pUserData, errorCode, SQLiteConnection.FromUtf8(pMessage), null));
			}
		}

		static readonly object s_lock = new object();
		static readonly SQLiteLogCallback s_callback = LogCallback;
		static event SQLiteLogEventHandler Handlers;
	}

	public delegate void SQLiteLogEventHandler(object sender, LogEventArgs e);
}
