$ErrorActionPreference = 'Stop'
$destination = Join-Path $PSScriptRoot 'MicrosoftEdgeWebview2Setup.exe'
Invoke-WebRequest -Uri 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile $destination
$signature = Get-AuthenticodeSignature -LiteralPath $destination
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
    throw 'WebView2 bootstrapper does not have a valid Microsoft signature'
}
Write-Output 'Verified Microsoft-signed Evergreen WebView2 bootstrapper.'
