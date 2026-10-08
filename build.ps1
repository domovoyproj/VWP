param([switch]$Installer, [string]$Compiler)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$sdk = if ($dotnetCommand) { $dotnetCommand.Source } elseif (Test-Path 'C:\momp\dotnet\dotnet.exe') { 'C:\momp\dotnet\dotnet.exe' } else { throw 'Install .NET 8 SDK' }
[xml]$project = Get-Content VWP.csproj
$version = $project.Project.PropertyGroup.Version
$requiredAssets = @('assets/presets.json')
foreach ($preset in (Get-Content 'assets/presets.json' -Raw | ConvertFrom-Json)) {
    foreach ($suffix in @('.mp4', '.jpg', '.cover.jpg', '.preview.mp4')) { $requiredAssets += "assets/$($preset.id)$suffix" }
    $requiredAssets += "assets/spatial/$($preset.id).png"
}
if (Test-Path 'assets/expansion.json') {
    $collection = Get-Content 'assets/expansion.json' -Raw | ConvertFrom-Json
    foreach ($scene in $collection.spatial) { $requiredAssets += "assets/spatial/$($scene.id).png" }
    foreach ($scene in $collection.live) {
        foreach ($suffix in @('.mp4', '.jpg', '.cover.jpg', '.preview.mp4')) { $requiredAssets += "assets/motion/$($scene.id)$suffix" }
    }
}
$missingAssets = @($requiredAssets | Where-Object { !(Test-Path -LiteralPath $_ -PathType Leaf) })
if ($missingAssets.Count -gt 0) { throw "Incomplete wallpaper collection ($($missingAssets.Count) missing files). Run the Cloud wallpaper collection workflow, or render the media as described in README.md. First missing asset: $($missingAssets[0])" }
if (!(Test-Path tools\ffmpeg.exe)) {
    $python = if (Test-Path .venv\Scripts\python.exe) { Join-Path $PSScriptRoot '.venv\Scripts\python.exe' } else { 'python' }
    & $python tools\bootstrap_ffmpeg.py
    if ($LASTEXITCODE -ne 0) { throw 'Install imageio-ffmpeg==0.6.0, then run tools/bootstrap_ffmpeg.py' }
}
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
