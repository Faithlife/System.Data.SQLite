properties {
  $configuration = "Release"
  $gitPath = "C:\Program Files (x86)\Git\bin\git.exe"
  $outputDir = "build"
  $apiKey = $null
  $nugetPackageSource = $null
}

$version = $null

Task Default -depends NuGetPack, NuGetPublish

Task Clean {
  Get-ChildItem "src\*\bin" | Remove-Item -force -recurse -ErrorAction Stop
  Get-ChildItem "src\*\obj" | Remove-Item -force -recurse -ErrorAction Stop
  if (Test-Path $outputDir) {
    Remove-Item $outputDir -force -recurse -ErrorAction Stop
  }
}

Task Build -depends Clean {
  Exec { tools\NuGet\NuGet restore }
  Exec { msbuild /m:4 /p:Configuration=$configuration /p:Platform="Any CPU" /p:VisualStudioVersion=12.0 System.Data.SQLite.sln }
  Exec { msbuild /m:4 /p:Configuration=$configuration /p:Platform="Xamarin iOS" /p:VisualStudioVersion=12.0 System.Data.SQLite.sln }
}

Task Tests -depends Build {
  mkdir $outputDir -force
  Exec { tools\NUnit\nunit-console.exe /nologo /framework=4.0 /xml=$outputDir\System.Data.SQLite.xml /config=$configuration tests\System.Data.SQLite.nunit }
}

Task SourceIndex -depends Tests {
  $headSha = & $gitPath rev-parse HEAD
  foreach ($project in @("System.Data.SQLite-Mac", "System.Data.SQLite-MonoAndroid", "System.Data.SQLite-MonoTouch", "System.Data.SQLite-Net45", "System.Data.SQLite-Portable")) {
    Exec { tools\SourceIndex\github-sourceindexer.ps1 -symbolsFolder src\$project\bin\$configuration -userId LogosBible -repository System.Data.SQLite -branch $headSha -sourcesRoot ${pwd} -dbgToolsPath "C:\Program Files (x86)\Windows Kits\8.1\Debuggers\x86" -gitHubUrl "https://raw.github.com" -serverIsRaw -ignoreUnknown -verbose }
  }
}

Task NuGetPack -depends SourceIndex {
  mkdir $outputDir -force
  $script:version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo("src\System.Data.SQLite-Net45\bin\$configuration\System.Data.SQLite.dll").FileVersion
  Exec { tools\NuGet\NuGet pack System.Data.SQLite.nuspec -Version $script:version -Prop Configuration=$configuration -Symbols -OutputDirectory $outputDir }
}

Task NuGetPublish -depends NuGetPack -precondition { return $apiKey -and $nugetPackageSource } {
  Exec { tools\NuGet\NuGet push $outputDir\Logos.System.Data.SQLite.$script:version.nupkg -ApiKey $apiKey -Source $nugetPackageSource }
}
