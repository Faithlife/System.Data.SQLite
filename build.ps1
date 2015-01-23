properties {
  $configuration = "Release"
  $gitPath = "C:\Program Files (x86)\Git\bin\git.exe"
}

Task Default -depends NuGetPack

Task Build {
  Exec { nuget restore }
  Exec { msbuild /m:4 /p:Configuration=$configuration /p:Platform="Any CPU" /p:VisualStudioVersion=12.0 System.Data.SQLite.sln }
  Exec { msbuild /m:4 /p:Configuration=$configuration /p:Platform="Xamarin iOS" /p:VisualStudioVersion=12.0 System.Data.SQLite.sln }
}

Task Tests -depends Build {
  mkdir build -force
  Exec { tools\NUnit\nunit-console.exe /nologo /framework=4.0 /xml=build\System.Data.SQLite.xml /config=$configuration tests\System.Data.SQLite.nunit }
}

Task SourceIndex -depends Tests {
  $headSha = & $gitPath rev-parse HEAD
  foreach ($project in @("System.Data.SQLite-Mac", "System.Data.SQLite-MonoAndroid", "System.Data.SQLite-MonoTouch", "System.Data.SQLite-Net45", "System.Data.SQLite-Portable")) {
    Exec { tools\SourceIndex\github-sourceindexer.ps1 -symbolsFolder src\$project\bin\$configuration -userId LogosBible -repository System.Data.SQLite -branch $headSha -sourcesRoot ${pwd} -dbgToolsPath "C:\Program Files (x86)\Windows Kits\8.1\Debuggers\x86" -gitHubUrl "https://raw.github.com" -serverIsRaw -ignoreUnknown -verbose }
  }
}

Task NuGetPack -depends SourceIndex {
  mkdir build -force
  $version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo("src\System.Data.SQLite-Net45\bin\$configuration\System.Data.SQLite.dll").FileVersion
  Exec { nuget pack System.Data.SQLite.nuspec -Version $version -Prop Configuration=$configuration -Symbols -OutputDirectory build }
}
