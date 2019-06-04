using System;
using NUnit.Framework;

namespace System.Data.SQLite.Tests
{
	[TestFixture]
	public class PlatformTests
	{
		[Test]
		public void CheckEnvironment()
		{
#if MONOANDROID
			Assert.AreEqual(PlatformID.Unix, Environment.OSVersion.Platform);
#elif XAMARIN_IOS
			Assert.AreEqual(PlatformID.Unix, Environment.OSVersion.Platform);
#elif WIN_X86
			Assert.AreEqual(PlatformID.Win32NT, Environment.OSVersion.Platform);
			Assert.IsFalse(Environment.Is64BitProcess);
#elif WIN_X64
			Assert.AreEqual(PlatformID.Win32NT, Environment.OSVersion.Platform);
			Assert.IsTrue(Environment.Is64BitProcess);
#elif OSX_X64
			Assert.AreEqual(PlatformID.Unix, Environment.OSVersion.Platform);
			Assert.IsTrue(Environment.Is64BitProcess);
#else
			Assert.Fail("Unexpected platform");
#endif
		}
	}
}
