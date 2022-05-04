namespace System.Data.SQLite
{
	/// <summary>
	/// Provides additional information for the <see cref="SQLiteConnection.StatementCompleted"/> event.
	/// </summary>
	public sealed class StatementCompletedEventArgs : EventArgs
	{
#if NET5_0
		/// <summary>
		/// The SQL of the statement that was executed.
		/// </summary>
		/// <remarks>This property is only valid to read during the event handler callback. (Once read, the string can be cached.)</remarks>
		public string Sql => m_sql ??= SQLiteConnection.FromUtf8(m_sqlPointer);
#else
		/// <summary>
		/// The SQL of the statement that was executed.
		/// </summary>
		public string Sql { get; }
#endif

		/// <summary>
		/// The time it took to execute the statement.
		/// </summary>
		public TimeSpan Time { get; }

#if NET5_0
		internal StatementCompletedEventArgs(IntPtr sql, TimeSpan time)
#else
		internal StatementCompletedEventArgs(string sql, TimeSpan time)
#endif
		{
#if NET5_0
			m_sqlPointer = sql;
#else
			Sql = sql;
#endif
			Time = time;
		}

#if NET5_0
		private readonly IntPtr m_sqlPointer;
		private string m_sql;
#endif
	}

	/// <summary>
	/// The delegate type for the event handlers of the <see cref="SQLiteConnection.StatementCompleted"/> event.
	/// </summary>
	/// <param name="sender">The source of the event.</param>
	/// <param name="e">The data for the event.</param>
	public delegate void StatementCompletedEventHandler(object sender, StatementCompletedEventArgs e);
}
