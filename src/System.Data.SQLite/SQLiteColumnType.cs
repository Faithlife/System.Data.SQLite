namespace System.Data.SQLite
{
	internal enum SQLiteColumnType
	{
		/// <summary>
		/// Not used
		/// </summary>
		None = 0,

		/// <summary>
		/// All integers in SQLite default to Int64
		/// </summary>
		Integer = 1,

		/// <summary>
		/// All floating point numbers in SQLite default to double
		/// </summary>
		Double = 2,

		/// <summary>
		/// The default data type of SQLite is text
		/// </summary>
		Text = 3,

		/// <summary>
		/// Typically blob types are only seen when returned from a function
		/// </summary>
		Blob = 4,

		/// <summary>
		/// Null types can be returned from functions
		/// </summary>
		Null = 5,
	}
}