param([Parameter(Mandatory)][int]$Scene)
$ErrorActionPreference='Stop'
$manifest=Get-Content (Join-Path $PSScriptRoot 'render-cache-0.7.0.json') -Raw | ConvertFrom-Json
$entry=$manifest.artifacts | Where-Object scene -eq $Scene
# This migration applies only to the immediate successor of the pinned source.
# Later changes render afresh; cached artwork cannot silently survive new inputs.
$parent=git rev-parse HEAD^
if($LASTEXITCODE -ne 0 -or $parent -ne $manifest.sha -or !$entry) { 'hit=false' | Add-Content $env:GITHUB_OUTPUT; return }
$collection=if($Scene -ge 1101){'anime'}else{'living'}
$inputs=@('CitySurface.cs','tools/live-check/LivingRender.cs','tools/live-check/Program.cs','VWP.csproj','tools/live-check/live-check.csproj','assets/city/vendor',"assets/$collection/catalog.json", "assets/$collection/$Scene.png")
if($collection -eq 'living'){$inputs+='assets/living'}
git diff --quiet $manifest.sha -- @inputs
if($LASTEXITCODE -ne 0) { 'hit=false' | Add-Content $env:GITHUB_OUTPUT; return }
if(!$env:GH_TOKEN) { throw 'Artifact read credential is unavailable' }
$handler=[Net.Http.HttpClientHandler]::new();$handler.AllowAutoRedirect=$false
$api=[Net.Http.HttpClient]::new($handler);$download=[Net.Http.HttpClient]::new()
$api.DefaultRequestHeaders.UserAgent.ParseAdd('VWP-render-cache')
$api.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$env:GH_TOKEN)
$archive=Join-Path ([IO.Path]::GetTempPath()) ('vwp-render-'+[guid]::NewGuid().ToString('N')+'.zip')
try {
    $endpoint="https://api.github.com/repos/domovoyproj/VWP/actions/artifacts/$($entry.id)"
    $info=$api.GetAsync($endpoint).GetAwaiter().GetResult()
    if(!$info.IsSuccessStatusCode){throw "Artifact metadata request failed: $($info.StatusCode)"}
    $metadata=$info.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
    $info.Dispose()
    if($metadata.expired -or $metadata.name -ne $entry.name -or $metadata.workflow_run.id -ne $manifest.run -or $metadata.workflow_run.head_sha -ne $manifest.sha -or $metadata.digest -ne ('sha256:'+$entry.digest)) { throw 'Artifact provenance does not match the pinned source' }
    $redirect=$api.GetAsync($endpoint+'/zip').GetAwaiter().GetResult()
    if([int]$redirect.StatusCode -ne 302){throw "Artifact download did not return a redirect: $($redirect.StatusCode)"}
    $uri=$redirect.Headers.Location;$redirect.Dispose()
    if(!$uri.IsAbsoluteUri -or $uri.Scheme -ne 'https' -or $uri.UserInfo){throw 'Invalid artifact download location'}
    # The redirect is fetched with a separate client, without GitHub credentials.
    $bytes=$download.GetAsync($uri,[Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
    if(!$bytes.IsSuccessStatusCode){throw "Artifact archive download failed: $($bytes.StatusCode)"}
    $file=[IO.File]::Create($archive)
    try{$bytes.Content.CopyToAsync($file).GetAwaiter().GetResult()}finally{$file.Dispose();$bytes.Dispose()}
    if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.digest){throw 'Artifact archive checksum mismatch'}
    $folder=[IO.Path]::GetFullPath((Join-Path (Get-Location) "assets/$collection"))
    $zip=[IO.Compression.ZipFile]::OpenRead($archive)
    try {
        $expected=@("$Scene.mp4","$Scene.png")
        if($zip.Entries.Count -ne 2 -or @($zip.Entries | Where-Object {$_.FullName -notin $expected}).Count -gt 0){throw 'Unexpected files in media archive'}
        foreach($item in $zip.Entries){[IO.Compression.ZipFileExtensions]::ExtractToFile($item,(Join-Path $folder $item.FullName),$true)}
    }finally{$zip.Dispose()}
    'hit=true' | Add-Content $env:GITHUB_OUTPUT
    Write-Output "Restored $Scene from verified artifact $($entry.id); SHA256 matched."
}finally{$api.Dispose();$handler.Dispose();$download.Dispose();if(Test-Path -LiteralPath $archive){Remove-Item -LiteralPath $archive}}
