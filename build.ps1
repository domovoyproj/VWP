param([switch]$Installer, [string]$Compiler)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$sdk = if ($dotnetCommand) { $dotnetCommand.Source } elseif (Test-Path 'C:\momp\dotnet\dotnet.exe') { 'C:\momp\dotnet\dotnet.exe' } else { throw 'Install .NET 8 SDK' }
[xml]$project = Get-Content VWP.csproj
$version = $project.Project.PropertyGroup.Version
$newCollections = Get-Content 'tools/collections-plan.json' -Raw | ConvertFrom-Json
$newMediaMissing = @($newCollections | ForEach-Object { $collectionFolder=$_.folder; $_.entries | ForEach-Object { foreach ($suffix in 'mp4','poster.png','preview.mp4') { if (!(Test-Path "assets/$collectionFolder/$($_.id).$suffix")) { $_.id } } } })
if ($newMediaMissing.Count -gt 0) {
    $electricPython = if (Test-Path .venv\Scripts\python.exe) { Join-Path $PSScriptRoot '.venv\Scripts\python.exe' } else { 'python' }
    & $electricPython tools\render_collections.py
    if ($LASTEXITCODE -ne 0) { throw 'New collections render failed; install Pillow, numpy and imageio-ffmpeg==0.6.0.' }
}
$requiredAssets = @('assets/presets.json', 'assets/themes/themes.json', 'assets/themes/palettes.json', 'assets/themes/obsidian.png', 'assets/themes/glacier.png', 'assets/themes/pearl.png', 'assets/anime/catalog.json', 'assets/anime/anime.js', 'assets/anime/index.html')
$requiredAssets += @('assets/city/index.html','assets/city/city.js','assets/city/cover.png','assets/city/vendor/three.module.js','assets/city/vendor/three.core.js','assets/city/vendor/Reflector.js','assets/city/vendor/BufferGeometryUtils.js','assets/city/vendor/LICENSE.txt','assets/living/catalog.json')
$requiredAssets += @('assets/city/city.css','assets/city/textures/apartment.png','assets/city/textures/townhouse.png','assets/city/textures/shop.png','assets/city/textures/tree.png','assets/city/textures/commuter.png','assets/city/textures/office-color.jpg')
$requiredAssets += 'assets/city/resources.json'
if (Test-Path -LiteralPath 'assets/city/resources.json') {
    foreach ($file in (Get-Content -LiteralPath 'assets/city/resources.json' -Raw | ConvertFrom-Json).files) { $requiredAssets += "assets/city/$file" }
}
foreach ($scene in 1001,1002,1003) { $requiredAssets += "assets/living/$scene.mp4"; $requiredAssets += "assets/living/$scene.png" }
foreach ($scene in 1101..1115) { $requiredAssets += "assets/anime/$scene.mp4"; $requiredAssets += "assets/anime/$scene.png" }
foreach ($scene in 1201..1220) { foreach ($extension in 'mp4','png','poster.png','preview.mp4') { $requiredAssets += "assets/vibes/$scene.$extension" } }
$requiredAssets += @('assets/vibes/catalog.json','assets/vibes/index.html','assets/vibes/vibes.js')
foreach ($scene in 1301..1306) { foreach ($extension in 'mp4','png','poster.png','preview.mp4') { $requiredAssets += "assets/seasons/$scene.$extension" } }
$requiredAssets += @('assets/seasons/catalog.json','assets/seasons/index.html','assets/seasons/seasons.js')
foreach ($collection in $newCollections) {
    $requiredAssets += "assets/$($collection.folder)/catalog.json"
    foreach ($scene in $collection.entries) { foreach ($extension in 'mp4','png','poster.png','preview.mp4') { $requiredAssets += "assets/$($collection.folder)/$($scene.id).$extension" } }
}
foreach ($folder in (Get-ChildItem assets/cursors -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'pack.json') })) {
    $id = $folder.Name
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
foreach ($obsolete in @('assets/motion', 'assets/spatial', 'assets/vibes/characters', 'assets/living/commuter.png', 'assets/living/couple.png', 'assets/cinematic/train.png')) {
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
