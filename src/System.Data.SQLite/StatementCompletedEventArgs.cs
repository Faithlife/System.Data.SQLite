using System;

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
		public string Sql
		{
			get { return m_sql; }
		}

		/// <summary>
		/// The time it took to execute the statement.
		/// </summary>
		public TimeSpan Time
		{
			get { return m_time; }
		}

		internal StatementCompletedEventArgs(string sql, TimeSpan time)
		{
			m_sql = sql;
			m_time = time;
		}

		readonly string m_sql;
		readonly TimeSpan m_time;
	}

	/// <summary>
	/// The delegate type for the event handlers of the <see cref="SQLiteConnection.StatementCompleted"/> event.
	/// </summary>
	/// <param name="sender">The source of the event.</param>
	/// <param name="e">The data for the event.</param>
	public delegate void StatementCompletedEventHandler(object sender, StatementCompletedEventArgs e);
}
