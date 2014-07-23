using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace System.Data.SQLite.Tests
{
	internal class File
	{
		public static void Delete(string path)
		{
			GetFile(path).DeleteAsync().GetAwaiter().GetResult();
		}

		public static void SetAttributes(string path, FileAttributes newAttributes)
		{
			var file = GetFile(path);
			file.Properties.SavePropertiesAsync(new Dictionary<string, object> { { c_fileAttributesKey, newAttributes } }).GetAwaiter().GetResult();
		}

		private static StorageFile GetFile(string path)
		{
			return StorageFile.GetFileFromPathAsync(path).GetAwaiter().GetResult();
		}

		private const String c_fileAttributesKey = "System.FileAttributes";
	}

}
