using System.Data.SQLite;
using System.Reflection;

namespace System.Data.SQLite.Tests
{
	internal static class TestUtility
	{
#if !NET45
		public static int Execute(this IDbConnection connection, string commandText, object parameters = null, IDbTransaction transaction = null)
		{
			using (var command = connection.CreateCommand())
			{
				SetupCommand(command, commandText, parameters, transaction);
				return command.ExecuteNonQuery();
			}
		}

		public static IDataReader ExecuteReader(this IDbConnection connection, string commandText, object parameters = null, IDbTransaction transaction = null)
		{
			using (var command = connection.CreateCommand())
			{
				SetupCommand(command, commandText, parameters, transaction);
				return command.ExecuteReader();
			}
		}

		static void SetupCommand(IDbCommand command, string commandText, object parameters, IDbTransaction transaction)
		{
			command.CommandText = commandText;
			command.Transaction = transaction;
			if (parameters != null)
			{
				foreach (var prop in parameters.GetType().GetTypeInfo().DeclaredProperties)
				{
					var param = command.CreateParameter();
					param.ParameterName = prop.Name;
					param.Value = prop.GetValue(parameters);
					command.Parameters.Add(param);
				}
			}
		}
#endif
	}
}
