#Requires -Version 7
<#
.SYNOPSIS
    One-time developer setup: writes the Gemini API key to the git-ignored
    z-ai-com\gemini.api.key file and verifies the build succeeds.
.DESCRIPTION
    New developers run this script after cloning the repo.
    The key file is excluded from source control via the *z-ai-com gitignore rule.
    Existing developers who already have the file do not need to run this script.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot  = Split-Path -Parent $PSScriptRoot
$secretDir = Join-Path $repoRoot 'z-ai-com'
$keyFile   = Join-Path $secretDir 'gemini.api.key'
$slnFile   = Join-Path $repoRoot 'LifeGrid.slnx'

Write-Host ""
Write-Host "=== LifeGrid Developer Secret Setup ===" -ForegroundColor Cyan
Write-Host ""

# 1. Verify dotnet CLI is available
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "dotnet CLI not found on PATH. Install .NET 10 SDK from https://dot.net and retry."
    exit 1
}

# 2. Warn if file already exists
if (Test-Path $keyFile) {
    Write-Host "  [INFO] $keyFile already exists." -ForegroundColor Yellow
    $overwrite = Read-Host "  Overwrite with a new key? (y/N)"
    if ($overwrite -notmatch '^[Yy]$') {
        Write-Host "  Keeping existing key. Running build to verify..." -ForegroundColor Green
    } else {
        Remove-Item $keyFile -Force
    }
}

# 3. Prompt for key if file is absent (or was just removed)
if (-not (Test-Path $keyFile)) {
    $secureKey = Read-Host "  Enter your Gemini API key" -AsSecureString
    $bstr      = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
    try {
        $plainKey = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    } finally {
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }

    if ([string]::IsNullOrWhiteSpace($plainKey)) {
        Write-Error "API key cannot be empty."
        exit 1
    }

    # 4. Create directory and write key file (no trailing newline)
    if (-not (Test-Path $secretDir)) {
        New-Item -ItemType Directory -Path $secretDir | Out-Null
        Write-Host "  [OK] Created $secretDir" -ForegroundColor Green
    }

    [System.IO.File]::WriteAllText($keyFile, $plainKey.Trim(), [System.Text.Encoding]::UTF8)
    Write-Host "  [OK] Key written to $keyFile" -ForegroundColor Green

    # Zero plaintext from memory
    $plainKey = $null
    [System.GC]::Collect()
}

# 5. Build to verify the key satisfies the MSBuild hard-fail check
Write-Host ""
Write-Host "  Running: dotnet build $slnFile" -ForegroundColor Cyan
dotnet build $slnFile --nologo -v minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "  BUILD FAILED — check the output above." -ForegroundColor Red
    Write-Host "  Ensure the key you entered is valid and non-empty, then re-run this script." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "  SUCCESS — developer secrets configured and build verified." -ForegroundColor Green
Write-Host ""
