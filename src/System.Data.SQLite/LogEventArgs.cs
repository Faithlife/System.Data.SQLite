namespace System.Data.SQLite
{
	/// <summary>
	/// Event data for logging event handlers.
	/// </summary>
	public sealed class LogEventArgs : EventArgs
	{
		/// <summary>
		/// The error code.  The type of this object value should be
		/// <see cref="Int32" /> or <see cref="SQLiteErrorCode" />.
		/// </summary>
		public readonly object ErrorCode;

		/// <summary>
		/// SQL statement text as the statement first begins executing
		/// </summary>
		public readonly string Message;

		/// <summary>
		/// Extra data associated with this event, if any.
		/// </summary>
		public readonly object Data;

		/// <summary>
		/// Constructs the object.
		/// </summary>
		/// <param name="pUserData">Should be null.</param>
		/// <param name="errorCode">
		/// The error code.  The type of this object value should be
		/// <see cref="Int32" /> or <see cref="SQLiteErrorCode" />.
		/// </param>
		/// <param name="message">The error message, if any.</param>
		/// <param name="data">The extra data, if any.</param>
		internal LogEventArgs(IntPtr pUserData, object errorCode, string message, object data)
		{
			ErrorCode = errorCode;
			Message = message;
			Data = data;
		}
	}
}
