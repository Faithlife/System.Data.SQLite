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
				if (s_loggingDisabled)
					throw new InvalidOperationException("SQLite logging is disabled.");

				lock (s_lock)
					Handlers += value;
			}
			remove
			{
				if (s_loggingDisabled)
					throw new InvalidOperationException("SQLite logging is disabled.");

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
#if false //NET45
			string disableSqliteLogging = System.Configuration.ConfigurationManager.AppSettings["disableSqliteLogging"];
			bool settingValue;
			if (disableSqliteLogging != null && bool.TryParse(disableSqliteLogging, out settingValue) && settingValue)
			{
				s_loggingDisabled = true;
				return;
			}
#endif

#if XAMARIN_IOS
			// Workaround Mono limitation with AMD64 varargs methods - See https://bugzilla.xamarin.com/show_bug.cgi?id=30144
			if (IntPtr.Size == 8 && ObjCRuntime.Runtime.Arch == ObjCRuntime.Arch.DEVICE)
				NativeMethods.sqlite3_config_log_arm64(SQLiteConfigOpsEnum.SQLITE_CONFIG_LOG, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, s_callback, IntPtr.Zero);
			else
				NativeMethods.sqlite3_config_log(SQLiteConfigOpsEnum.SQLITE_CONFIG_LOG, s_callback, IntPtr.Zero);
#else
			NativeMethods.sqlite3_config_log(SQLiteConfigOpsEnum.SQLITE_CONFIG_LOG, s_callback, IntPtr.Zero);
#endif
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
		static readonly bool s_loggingDisabled = false;
		static event SQLiteLogEventHandler Handlers;
	}

	public delegate void SQLiteLogEventHandler(object sender, LogEventArgs e);
}
