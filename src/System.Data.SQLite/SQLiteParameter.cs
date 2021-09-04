using System.Data.Common;

namespace System.Data.SQLite
{
	public sealed class SQLiteParameter : DbParameter
	{
		public override DbType DbType { get; set; }

		public override ParameterDirection Direction { get; set; }

		public override bool IsNullable { get; set; }

		public override string ParameterName { get; set; }

		public override int Size { get; set; }

		public override string SourceColumn
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public override bool SourceColumnNullMapping
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public override DataRowVersion SourceVersion
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public override object Value { get; set; }

		public override void ResetDbType() => DbType = default(DbType);
	}
}
