param([switch]$Installer, [string]$Compiler)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$sdk = (Get-Command dotnet -ErrorAction Stop).Source
$python = if (Test-Path .venv/Scripts/python.exe) { Join-Path $PSScriptRoot '.venv/Scripts/python.exe' } else { 'python' }
[xml]$project = Get-Content VWP.csproj
$version = $project.Project.PropertyGroup.Version
& $python tools/validate_studio_library.py
if ($LASTEXITCODE -ne 0) { throw 'The complete rendered Studio library failed validation.' }
if (!(Test-Path tools/ffmpeg.exe)) {
    & $python tools/bootstrap_ffmpeg.py
    if ($LASTEXITCODE -ne 0) { throw 'FFmpeg bootstrap failed.' }
}
$output = Join-Path $PSScriptRoot "dist/Release-$version"
& $sdk publish VWP.csproj -c Release -r win-x64 --self-contained true -p:StudioLibraryOnly=true -o $output
if ($LASTEXITCODE -ne 0) { throw 'Self-contained build failed.' }
$outputRoot = [IO.Path]::GetFullPath($output)+[IO.Path]::DirectorySeparatorChar
foreach ($obsolete in @('assets/motion','assets/spatial','assets/vibes/characters','assets/living/commuter.png','assets/living/couple.png','assets/cinematic/train.png')) {
    $target = [IO.Path]::GetFullPath((Join-Path $output $obsolete))
    if (!$target.StartsWith($outputRoot)) { throw 'Invalid cleanup target.' }
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
}
foreach ($oldVideo in (Get-ChildItem -LiteralPath (Join-Path $output 'assets') -Filter '*.mp4' -File -Recurse)) {
    if (!$oldVideo.FullName.StartsWith($outputRoot)) { throw 'Invalid obsolete video target.' }
    Remove-Item -LiteralPath $oldVideo.FullName -Force
}
& $python tools/package_online_release.py --version $version --build $output --validate-only
if ($LASTEXITCODE -ne 0) { throw 'Published application is incomplete.' }
if ($Installer) {
    if (!$Compiler) {
        $candidates = @((Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'),(Join-Path $PSScriptRoot 'artifacts/inno/ISCC.exe'))
        $Compiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    }
    if (!$Compiler) { throw 'Install Inno Setup 6 or pass -Compiler.' }
    & $Compiler "/DAppVersion=$version" "/DBuildDir=$output" installer/VWP.iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
    $setup = Join-Path $PSScriptRoot "dist/VWP-$version-Setup.exe"
    $digest = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath ($setup+'.sha256') -Value ($digest+'  '+(Split-Path $setup -Leaf)) -Encoding ascii
}
