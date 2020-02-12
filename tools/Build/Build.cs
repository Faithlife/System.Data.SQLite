using System;
using System.Collections.Generic;
using Faithlife.Build;
using static Faithlife.Build.BuildUtility;

internal static class Build
{
	public static int Main(string[] args) => BuildRunner.Execute(args, build =>
	{
		var dotNetBuildSettings = new DotNetBuildSettings
		{
			NuGetApiKey = Environment.GetEnvironmentVariable("NUGET_API_KEY"),
			DocsSettings = new DotNetDocsSettings
			{
				GitLogin = new GitLoginInfo("faithlifebuildbot", Environment.GetEnvironmentVariable("BUILD_BOT_PASSWORD") ?? ""),
				GitAuthor = new GitAuthorInfo("Faithlife Build Bot", "faithlifebuildbot@users.noreply.github.com"),
				SourceCodeUrl = "https://github.com/Faithlife/System.Data.SQLite/tree/master/src",
			},

			// 32-bit MSBuild is required for Xamarin builds
			MSBuildSettings = new MSBuildSettings { Version = MSBuildVersion.VS2019, Platform = MSBuildPlatform.X32 },

			// default to everything
			SolutionPlatform = "everything",

			// we need the build options below
			BuildOptions = new DotNetBuildOptions(),

			// dotnet test doesn't work on Mac, so we must specify the test assemblies directly (see below)
			TestSettings = new DotNetTestSettings(),
		};

		IReadOnlyList<string> findTestAssemblies()
		{
			var buildOptions = dotNetBuildSettings.BuildOptions;
			var configuration = buildOptions.ConfigurationOption.Value;
			var platform = buildOptions.PlatformOption.Value ?? dotNetBuildSettings.SolutionPlatform;
			var platformFolder = platform == "Any CPU" ? "" : $"{platform}/";
			var testAssembliesGlob = $"tests/**/bin/{platformFolder}{configuration}/net*/{(BuildEnvironment.IsMacOS() ? "osx*" : "win*")}/*Tests.dll";
			var testAssemblies = FindFiles(testAssembliesGlob);
			if (testAssemblies.Count == 0)
				throw new InvalidOperationException($"No test assemblies found: {testAssembliesGlob}");
			return testAssemblies;
		}

		dotNetBuildSettings.TestSettings.FindTestAssemblies = findTestAssemblies;

		build.AddDotNetTargets(dotNetBuildSettings);
	});
}
