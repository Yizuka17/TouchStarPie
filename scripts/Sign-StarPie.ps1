[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0, ValueFromPipeline = $true)]
    [string[]] $Path,

    [string] $CertificateThumbprint = '8676591D3E2C471458A6471F044AD7272FA31893',

    [string] $TimestampUrl = 'http://timestamp.digicert.com',

    [switch] $NoTimestamp,

    [switch] $EnableUiAccess
)

begin {
    $ErrorActionPreference = 'Stop'
    $CertificateThumbprint = $CertificateThumbprint.Replace(' ', '').ToUpperInvariant()
    $signingDirectory = Join-Path $env:LOCALAPPDATA 'StarPie\Signing'
    $pfxPath = Join-Path $signingDirectory 'StarPie-Local-CodeSigning-2026.pfx'
    $passwordPath = Join-Path $signingDirectory 'StarPie-Local-CodeSigning-2026.password.dpapi.xml'
    $publicCertificatePath = Join-Path $signingDirectory 'StarPie-Local-CodeSigning-2026.cer'

    function Get-StarPieCertificate {
        $certificatePath = "Cert:\CurrentUser\My\$CertificateThumbprint"
        if (-not (Test-Path -LiteralPath $certificatePath)) {
            if (-not (Test-Path -LiteralPath $pfxPath) -or
                -not (Test-Path -LiteralPath $passwordPath)) {
                throw "StarPie signing certificate $CertificateThumbprint is not installed and its protected backup is unavailable."
            }

            $password = Import-Clixml -LiteralPath $passwordPath
            try {
                Import-PfxCertificate `
                    -FilePath $pfxPath `
                    -Password $password `
                    -CertStoreLocation 'Cert:\CurrentUser\My' `
                    -Exportable | Out-Null
            }
            finally {
                if ($password -is [System.IDisposable]) {
                    $password.Dispose()
                }
            }
        }

        $certificate = Get-Item -LiteralPath $certificatePath
        if (-not $certificate.HasPrivateKey) {
            throw "Certificate $CertificateThumbprint does not have an accessible private key."
        }
        if ($certificate.NotAfter -le (Get-Date)) {
            throw "Certificate $CertificateThumbprint expired on $($certificate.NotAfter.ToString('u'))."
        }
        $hasCodeSigningEku = $certificate.Extensions | Where-Object {
            $_.Oid.Value -eq '2.5.29.37' -and $_.Format($false) -match '1\.3\.6\.1\.5\.5\.7\.3\.3|Code Signing|代码签名'
        }
        if (-not $hasCodeSigningEku) {
            throw "Certificate $CertificateThumbprint is not valid for code signing."
        }

        if (Test-Path -LiteralPath $publicCertificatePath) {
            foreach ($storeName in 'Root', 'TrustedPublisher') {
                $trustedPath = "Cert:\CurrentUser\$storeName\$CertificateThumbprint"
                if (-not (Test-Path -LiteralPath $trustedPath)) {
                    Import-Certificate `
                        -FilePath $publicCertificatePath `
                        -CertStoreLocation "Cert:\CurrentUser\$storeName" | Out-Null
                }
            }
        }
        return $certificate
    }

    function Find-SignTool {
        $command = Get-Command signtool.exe -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($command) {
            return $command.Source
        }

        $kitRoot = 'C:\Program Files (x86)\Windows Kits\10\bin'
        if (Test-Path -LiteralPath $kitRoot) {
            $candidate = Get-ChildItem -LiteralPath $kitRoot -Filter signtool.exe -Recurse -File |
                Where-Object FullName -Match '\\x64\\signtool\.exe$' |
                Sort-Object FullName -Descending |
                Select-Object -First 1
            if ($candidate) {
                return $candidate.FullName
            }
        }
        throw 'SignTool.exe was not found. Install the Windows SDK signing tools.'
    }

    function Find-ManifestTool {
        $kitRoot = 'C:\Program Files (x86)\Windows Kits\10\bin'
        if (Test-Path -LiteralPath $kitRoot) {
            $candidate = Get-ChildItem -LiteralPath $kitRoot -Filter mt.exe -Recurse -File |
                Where-Object FullName -Match '\\x64\\mt\.exe$' |
                Sort-Object FullName -Descending |
                Select-Object -First 1
            if ($candidate) {
                return $candidate.FullName
            }
        }
        throw 'Mt.exe was not found. Install the Windows SDK manifest tools.'
    }

    $certificate = Get-StarPieCertificate
    $signTool = Find-SignTool
    $manifestTool = if ($EnableUiAccess) { Find-ManifestTool } else { $null }
}

process {
    foreach ($inputPath in $Path) {
        $resolvedPath = (Resolve-Path -LiteralPath $inputPath).Path
        if ((Get-Item -LiteralPath $resolvedPath).PSIsContainer) {
            throw "Signing target is a directory: $resolvedPath"
        }

        if ($EnableUiAccess) {
            $manifestPath = Join-Path ([System.IO.Path]::GetTempPath()) ("StarPie-manifest-{0}.xml" -f [guid]::NewGuid())
            $verificationPath = "$manifestPath.verify"
            try {
                # Preserve every Windows App SDK activation entry in the generated merged
                # manifest and change only the final uiAccess declaration.
                & $manifestTool -nologo "-inputresource:$resolvedPath;#1" "-out:$manifestPath"
                if ($LASTEXITCODE -ne 0) {
                    throw "Unable to extract the generated WinUI manifest from $resolvedPath."
                }
                $manifestText = [System.IO.File]::ReadAllText($manifestPath)
                if ($manifestText -notmatch 'uiAccess="true"') {
                    if ($manifestText -notmatch 'uiAccess="false"') {
                        throw 'The generated manifest does not contain an expected uiAccess declaration.'
                    }
                    $manifestText = $manifestText.Replace('uiAccess="false"', 'uiAccess="true"')
                    [System.IO.File]::WriteAllText(
                        $manifestPath,
                        $manifestText,
                        [System.Text.UTF8Encoding]::new($false))
                    & $manifestTool -nologo -manifest $manifestPath "-outputresource:$resolvedPath;#1"
                    if ($LASTEXITCODE -ne 0) {
                        throw "Unable to embed the UIAccess manifest into $resolvedPath."
                    }
                }

                & $manifestTool -nologo "-inputresource:$resolvedPath;#1" "-out:$verificationPath"
                if ($LASTEXITCODE -ne 0 -or
                    [System.IO.File]::ReadAllText($verificationPath) -notmatch 'uiAccess="true"') {
                    throw "UIAccess manifest verification failed for $resolvedPath."
                }
            }
            finally {
                Remove-Item -LiteralPath $manifestPath, $verificationPath -Force -ErrorAction SilentlyContinue
            }
        }

        $arguments = @(
            'sign',
            '/sha1', $certificate.Thumbprint,
            '/s', 'My',
            '/fd', 'SHA256',
            '/d', 'StarPie'
        )
        if (-not $NoTimestamp) {
            $arguments += @('/tr', $TimestampUrl, '/td', 'SHA256')
        }
        $arguments += $resolvedPath

        & $signTool @arguments
        if ($LASTEXITCODE -ne 0) {
            throw "SignTool failed for $resolvedPath with exit code $LASTEXITCODE."
        }

        & $signTool verify /pa /all $resolvedPath
        if ($LASTEXITCODE -ne 0) {
            throw "Signature verification failed for $resolvedPath with exit code $LASTEXITCODE."
        }

        $signature = Get-AuthenticodeSignature -LiteralPath $resolvedPath
        [pscustomobject]@{
            Path = $resolvedPath
            Status = $signature.Status
            Signer = $signature.SignerCertificate.Subject
            Thumbprint = $signature.SignerCertificate.Thumbprint
            Timestamped = $null -ne $signature.TimeStamperCertificate
        }
    }
}
