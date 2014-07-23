using System.Linq;
using System.Reflection;

namespace System.Data.SQLite.Tests
{
	internal static class TypeUtility
	{
		public static MethodInfo GetMethod(this Type type, string name)
		{
			return type.GetRuntimeMethods().FirstOrDefault(x => x.Name == name);
		}
	}
}
