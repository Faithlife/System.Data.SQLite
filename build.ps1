properties {
  $configuration = "Release"
}

Task Default -depends SourceIndex

Task Build {
  Exec { msbuild /m:4 /property:Configuration=$configuration System.Data.SQLite.sln }
}

Task Tests -depends Build {
  mkdir build -force
  Exec { tools\NUnit\nunit-console.exe /nologo /framework=4.0 /xml=build\System.Data.SQLite.xml /config=$configuration tests\System.Data.SQLite.nunit }
}

Task SourceIndex -depends Tests {
  $headSha = & "C:\Program Files (x86)\Git\bin\git.exe" rev-parse HEAD
  Exec { tools\SourceIndex\github-sourceindexer.ps1 -symbolsFolder src\System.Data.SQLite\bin\$configuration -userId LogosBible -repository System.Data.SQLite -branch $headSha -sourcesRoot ${pwd} -dbgToolsPath "C:\Program Files (x86)\Windows Kits\8.0\Debuggers\x86" -gitHubUrl "https://raw.github.com" -serverIsRaw -verbose }
}

Task NuGetPack -depends SourceIndex {
  Exec { nuget pack src\System.Data.SQLite\System.Data.SQLite.csproj -Prop Configuration=$configuration -Symbols }
}
