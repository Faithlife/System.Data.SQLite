namespace System.Data.SQLite
{
	/// <summary>
	/// Provides additional information for the <see cref="SQLiteConnection.StatementCompleted"/> event.
	/// </summary>
	public sealed class StatementCompletedEventArgs : EventArgs
	{
		/// <summary>
		/// The SQL of the statement that was executed.
		/// </summary>
		/// <remarks>This property is only valid to read during the event handler callback. (Once read, the string can be cached.)</remarks>
		public string Sql => m_sql ??= SQLiteConnection.FromUtf8(m_sqlPointer);

		/// <summary>
		/// The time it took to execute the statement.
		/// </summary>
		public TimeSpan Time { get; }

		internal StatementCompletedEventArgs(IntPtr sql, TimeSpan time)
		{
			m_sqlPointer = sql;
			Time = time;
		}

		private readonly IntPtr m_sqlPointer;
		private string m_sql;
	}

	/// <summary>
	/// The delegate type for the event handlers of the <see cref="SQLiteConnection.StatementCompleted"/> event.
	/// </summary>
	/// <param name="sender">The source of the event.</param>
	/// <param name="e">The data for the event.</param>
	public delegate void StatementCompletedEventHandler(object sender, StatementCompletedEventArgs e);
}
