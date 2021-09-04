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
				{
					InitializeWithLock();
					Handlers += value;
				}
			}
			remove
			{
				lock (s_lock)
					Handlers -= value;
			}
		}

		private static void InitializeWithLock()
		{
			if (s_callback is null)
			{
				s_callback = LogCallback;
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
		}

#if MONOTOUCH
		[MonoTouch.MonoPInvokeCallback(typeof(SQLiteLogCallback))]
#elif XAMARIN_IOS
		[ObjCRuntime.MonoPInvokeCallback(typeof(SQLiteLogCallback))]
#endif
		static void LogCallback(IntPtr pUserData, int errorCode, IntPtr pMessage)
		{
			lock (s_lock)
				Handlers?.Invoke(null, new LogEventArgs(pUserData, errorCode, SQLiteConnection.FromUtf8(pMessage), null));
		}

		static readonly object s_lock = new();
		static SQLiteLogCallback s_callback;
		static event SQLiteLogEventHandler Handlers;
	}

	public delegate void SQLiteLogEventHandler(object sender, LogEventArgs e);
}
