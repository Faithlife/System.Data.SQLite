using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Data.SQLite
{
#if NET5_0
    /// <summary>
    /// Performs custom module initialization
    /// </summary>
    internal class ModuleInitializer
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
			var assembly = typeof(ModuleInitializer).Assembly;
			var configFile = assembly.Location + ".config";
			if (Environment.OSVersion.Platform == PlatformID.Unix && System.IO.File.Exists(configFile))
			{
				var dllMapConfig = System.Xml.Linq.XDocument.Load(configFile);
				foreach (var element in dllMapConfig.Root.Elements("dllmap"))
				{
					if (element.Attribute("dll")?.Value == NativeMethods.c_dllName)
					{
						s_mappedDllName = element.Attribute("target")?.Value;
						if (s_mappedDllName is not null)
							break;
					}
				}

				if (!string.IsNullOrEmpty(s_mappedDllName))
					NativeLibrary.SetDllImportResolver(assembly, DllImportResolver);
			}
        }

		private static IntPtr DllImportResolver(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
		{
			if (libraryName == NativeMethods.c_dllName && !string.IsNullOrEmpty(s_mappedDllName))
				return NativeLibrary.Load(s_mappedDllName);

			return IntPtr.Zero;
		}

		private static string s_mappedDllName;
    }
#endif
}
