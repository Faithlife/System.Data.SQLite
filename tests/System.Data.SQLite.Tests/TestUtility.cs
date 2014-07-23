using System.Collections.Generic;
using System.Data.SQLite;
using System.Reflection;
using NUnit.Framework;

namespace System.Data.SQLite.Tests
{
	internal static class TestUtility
	{
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

		public static IEnumerable<T> ReadAll<T>(this IDataReader reader)
		{
			while (reader.Read())
				yield return (T) reader.GetValue(0);
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
	}
}
