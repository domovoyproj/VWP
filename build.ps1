$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
dotnet publish VWP.csproj -c Release -r win-x64 --self-contained true -o dist\VWP-v2
if ($LASTEXITCODE -ne 0) { throw 'Ошибка сборки' }
