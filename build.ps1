param([switch]$Installer, [string]$Compiler)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$sdk = if ($dotnetCommand) { $dotnetCommand.Source } elseif (Test-Path 'C:\momp\dotnet\dotnet.exe') { 'C:\momp\dotnet\dotnet.exe' } else { throw 'Install .NET 8 SDK' }
[xml]$project = Get-Content VWP.csproj
$version = $project.Project.PropertyGroup.Version
$requiredAssets = @('assets/presets.json', 'assets/themes/themes.json', 'assets/themes/obsidian.png', 'assets/themes/glacier.png', 'assets/themes/pearl.png')
foreach ($id in @('vwp-sakura', 'vwp-neon', 'vwp-crimson', 'vwp-astral', 'vwp-abyss', 'vwp-cyber', 'vwp-aurora', 'vwp-ember', 'vwp-pearl', 'vwp-obsidian', 'vwp-rose', 'bibata-ice', 'bibata-classic', 'capitaine-dark')) {
    $manifest = "assets/cursors/$id/pack.json"
    $requiredAssets += $manifest
    if (Test-Path -LiteralPath $manifest) {
        $pack = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
        foreach ($file in $pack.Files.PSObject.Properties.Value) { $requiredAssets += "assets/cursors/$id/$file" }
        $requiredAssets += "assets/cursors/$id/LICENSE.txt"
        $requiredAssets += "assets/cursors/$id/preview.png"
        if ($pack.SourceArchive) { $requiredAssets += "assets/cursors/$id/$($pack.SourceArchive)" }
    }
}
foreach ($preset in (Get-Content 'assets/presets.json' -Raw | ConvertFrom-Json)) {
    foreach ($suffix in @('.mp4', '.jpg', '.cover.jpg', '.preview.mp4')) { $requiredAssets += "assets/$($preset.id)$suffix" }
}
if (Test-Path 'assets/expansion.json') {
    $collection = Get-Content 'assets/expansion.json' -Raw | ConvertFrom-Json
    foreach ($scene in $collection.spatial) {
        $requiredAssets += "assets/cinematic/$($scene.id).png"
        $requiredAssets += "assets/cinematic/$($scene.id).mp4"
    }
    $requiredAssets += 'assets/cinematic/train.png'
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
# Publish can reuse a previous output directory. Retired media must never re-enter an update.
foreach ($obsolete in @('assets/motion', 'assets/spatial')) {
    $target = [IO.Path]::GetFullPath((Join-Path $output $obsolete))
    if (!$target.StartsWith([IO.Path]::GetFullPath($output) + [IO.Path]::DirectorySeparatorChar)) { throw 'Invalid cleanup target' }
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
}
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
