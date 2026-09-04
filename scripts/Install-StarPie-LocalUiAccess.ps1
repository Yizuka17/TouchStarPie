[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $SourceDirectory,

    [string] $DestinationDirectory = (Join-Path $env:ProgramFiles 'StarPie')
)

$ErrorActionPreference = 'Stop'
$expectedThumbprint = '8676591D3E2C471458A6471F044AD7272FA31893'
$identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [System.Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'UIAccess installation must run from an elevated PowerShell window.'
}

$source = (Resolve-Path -LiteralPath $SourceDirectory).Path
$sourceExecutable = Join-Path $source 'StarPie.exe'
$publicCertificate = Join-Path $source 'StarPie-Local-CodeSigning-2026.cer'
if (-not (Test-Path -LiteralPath $sourceExecutable)) {
    throw "StarPie.exe was not found in $source."
}
if (-not (Test-Path -LiteralPath $publicCertificate)) {
    throw "The local public signing certificate was not found in $source."
}

$signature = Get-AuthenticodeSignature -LiteralPath $sourceExecutable
if ($signature.Status -ne 'Valid' -or
    $signature.SignerCertificate.Thumbprint -ne $expectedThumbprint) {
    throw "StarPie.exe is not validly signed by the expected local certificate $expectedThumbprint."
}

$destination = [System.IO.Path]::GetFullPath($DestinationDirectory)
$programFilesRoot = [System.IO.Path]::GetFullPath($env:ProgramFiles).TrimEnd('\') + '\'
if (-not $destination.StartsWith($programFilesRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "UIAccess destination must remain under Program Files: $destination"
}

$running = Get-CimInstance Win32_Process | Where-Object {
    $_.ExecutablePath -and
    [System.IO.Path]::GetFullPath($_.ExecutablePath).StartsWith(
        $destination.TrimEnd('\') + '\',
        [System.StringComparison]::OrdinalIgnoreCase)
}
if ($running) {
    throw 'The installed StarPie process is running. Exit it from the tray and retry.'
}

Import-Certificate -FilePath $publicCertificate -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
Import-Certificate -FilePath $publicCertificate -CertStoreLocation 'Cert:\LocalMachine\TrustedPublisher' | Out-Null
New-Item -ItemType Directory -Force -Path $destination | Out-Null
Get-ChildItem -LiteralPath $source -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force
}

$installedExecutable = Join-Path $destination 'StarPie.exe'
$installedSignature = Get-AuthenticodeSignature -LiteralPath $installedExecutable
if ($installedSignature.Status -ne 'Valid' -or
    $installedSignature.SignerCertificate.Thumbprint -ne $expectedThumbprint) {
    throw 'Installed executable failed Authenticode verification.'
}

[pscustomobject]@{
    InstalledPath = $installedExecutable
    Signer = $installedSignature.SignerCertificate.Subject
    Thumbprint = $installedSignature.SignerCertificate.Thumbprint
    MachineRootTrusted = Test-Path "Cert:\LocalMachine\Root\$expectedThumbprint"
    MachinePublisherTrusted = Test-Path "Cert:\LocalMachine\TrustedPublisher\$expectedThumbprint"
}
