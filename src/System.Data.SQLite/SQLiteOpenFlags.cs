namespace System.Data.SQLite
{
	[Flags]
	internal enum SQLiteOpenFlags
	{
		None = 0,
		ReadOnly = 0x01,
		ReadWrite = 0x02,
		Create = 0x04,
		Uri = 0x40,
		SharedCache = 0x01000000,
	}
}
