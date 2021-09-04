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
		public string Sql { get; }

		/// <summary>
		/// The time it took to execute the statement.
		/// </summary>
		public TimeSpan Time { get; }

		internal StatementCompletedEventArgs(string sql, TimeSpan time)
		{
			Sql = sql;
			Time = time;
		}
	}

	/// <summary>
	/// The delegate type for the event handlers of the <see cref="SQLiteConnection.StatementCompleted"/> event.
	/// </summary>
	/// <param name="sender">The source of the event.</param>
	/// <param name="e">The data for the event.</param>
	public delegate void StatementCompletedEventHandler(object sender, StatementCompletedEventArgs e);
}
