$ErrorActionPreference = 'Stop'
$runtimeKeys = @('HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'HKCU:\Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}')
$runtimeVersions = @($runtimeKeys | ForEach-Object { (Get-ItemProperty -LiteralPath $_ -Name pv -ErrorAction SilentlyContinue).pv } | Where-Object { $_ -and $_ -ne '0.0.0.0' })
if ($runtimeVersions.Count -eq 0) {
    $bootstrap = Join-Path $env:RUNNER_TEMP 'MicrosoftEdgeWebview2Setup.exe'
    Invoke-WebRequest 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile $bootstrap
    $signature = Get-AuthenticodeSignature -LiteralPath $bootstrap
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'CN=Microsoft Corporation') { throw 'Unverified WebView2 bootstrapper' }
    $setup = Start-Process -FilePath $bootstrap -ArgumentList '/silent','/install' -WindowStyle Hidden -PassThru -Wait
    if ($setup.ExitCode -ne 0) { throw "WebView2 setup failed: $($setup.ExitCode)" }
}
