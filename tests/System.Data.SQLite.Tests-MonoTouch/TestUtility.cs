using System.Data.SQLite;

namespace System.Data.SQLite.Tests
{
	internal static class TestUtility
	{
		public static int Execute(this IDbConnection connection, string commandText, object parameters = null, IDbTransaction transaction = null)
		{
			using (var command = connection.CreateCommand())
			{
				command.CommandText = commandText;
				command.Transaction = transaction;
				if (parameters != null)
				{
					foreach (var prop in parameters.GetType().GetProperties())
					{
						var param = command.CreateParameter();
						param.ParameterName = prop.Name;
						param.Value = prop.GetValue(parameters);
						command.Parameters.Add(param);
					}
				}
				return command.ExecuteNonQuery();
			}
		}

		public static IDataReader ExecuteReader(this IDbConnection connection, string commandText, object parameters = null)
		{
			using (var command = connection.CreateCommand())
			{
				command.CommandText = commandText;
				return command.ExecuteReader();
			}
		}
	}
}
