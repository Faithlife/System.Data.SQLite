properties {
  $configuration = "Release"
  $buildAllPlatforms = $false
  $gitPath = "C:\Program Files\Git\bin\git.exe"
  $outputDir = "build"
  $apiKey = $null
  $nugetPackageSource = $null
}

$version = $null

Task Default -depends Tests

Task Clean {
  Get-ChildItem "src\*\bin" | Remove-Item -force -recurse -ErrorAction Stop
  Get-ChildItem "src\*\obj" | Remove-Item -force -recurse -ErrorAction Stop
  if (Test-Path $outputDir) {
    Remove-Item $outputDir -force -recurse -ErrorAction Stop
  }
}

Task Build -depends Clean {
  Exec { tools\NuGet\NuGet restore }
  if ($buildAllPlatforms) {
	$platform = "Mixed Platforms"
  } else {
	$platform = "Any CPU"
  }
  Exec { msbuild /m:4 /p:Configuration=$configuration /p:Platform=$platform /p:VisualStudioVersion=12.0 System.Data.SQLite.sln }
}

Task Tests -depends Build {
  mkdir $outputDir -force
  Exec { tools\NUnit\nunit-console.exe /nologo /framework=4.0 /xml=$outputDir\System.Data.SQLite.xml /config=$configuration tests\System.Data.SQLite.nunit }
}

Task SourceIndex -depends Tests {
  $headSha = & $gitPath rev-parse HEAD
  if ($buildAllPlatforms) {
    $projects = @("System.Data.SQLite-Mac", "System.Data.SQLite-MonoAndroid", "System.Data.SQLite-Net45", "System.Data.SQLite-Portable", "System.Data.SQLite-Xamarin.iOS")
  }
  else
  {
    $projects = @("System.Data.SQLite-Mac", "System.Data.SQLite-Net45", "System.Data.SQLite-Portable")
  }

  foreach ($project in $projects) {
    Exec { tools\SourceIndex\github-sourceindexer.ps1 -symbolsFolder src\$project\bin\$configuration -userId Faithlife -repository System.Data.SQLite -branch $headSha -sourcesRoot ${pwd} -gitHubUrl "https://raw.github.com" -serverIsRaw -ignoreUnknown -verbose -dbgToolsPath "C:\Program Files (x86)\Windows Kits\8.1\Debuggers\x86\srcsrv\" }
  }
}

Task NuGetPack -depends SourceIndex {
  mkdir $outputDir -force
  $dll = (Resolve-Path "src\System.Data.SQLite-Net45\bin\$configuration\System.Data.SQLite.dll").ToString()
  $script:version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($dll).ProductVersion
  Exec { tools\NuGet\NuGet pack System.Data.SQLite.nuspec -Version $script:version -Prop Configuration=$configuration -Symbols -OutputDirectory $outputDir }
}

Task NuGetPublish -depends NuGetPack -precondition { return $apiKey -and $nugetPackageSource } {
  Exec { tools\NuGet\NuGet push $outputDir\Faithlife.System.Data.SQLite.$script:version.nupkg -ApiKey $apiKey -Source $nugetPackageSource }
}
