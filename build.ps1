param([switch]$Installer, [string]$Compiler)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$sdk = if ($dotnetCommand) { $dotnetCommand.Source } elseif (Test-Path 'C:\momp\dotnet\dotnet.exe') { 'C:\momp\dotnet\dotnet.exe' } else { throw 'Install .NET 8 SDK' }
[xml]$project = Get-Content VWP.csproj
$version = $project.Project.PropertyGroup.Version
$output = Join-Path $PSScriptRoot "dist\Release-$version"
& $sdk publish VWP.csproj -c Release -r win-x64 --self-contained true -o $output
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
if ($Installer) {
    if (!$Compiler) {
        $candidates = @((Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'), (Join-Path $PSScriptRoot 'artifacts\inno\ISCC.exe'))
        $Compiler = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    }
    if (!$Compiler) { throw 'Install Inno Setup 6 or pass ISCC.exe using -Compiler' }
    & $Compiler "/DAppVersion=$version" "/DBuildDir=$output" installer\VWP.iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
    $setupPath = Join-Path $PSScriptRoot "dist\VWP-$version-Setup.exe"
    $checksum = (Get-FileHash $setupPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content ($setupPath + '.sha256') -Value ($checksum + '  ' + (Split-Path $setupPath -Leaf)) -Encoding ascii
}
