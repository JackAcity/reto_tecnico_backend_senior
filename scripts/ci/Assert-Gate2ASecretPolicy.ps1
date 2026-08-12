$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$policyScript = Join-Path $PSScriptRoot 'Test-SyntheticSecretPolicy.ps1'

& $policyScript -Path $repositoryRoot

$temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("gate2a-secret-policy-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryDirectory -ErrorAction Stop | Out-Null

try {
    $syntheticToken = 'ghp_' + ('A' * 36)
    Set-Content -LiteralPath (Join-Path $temporaryDirectory 'synthetic.env') -Value $syntheticToken -NoNewline

    $detected = $false
    try {
        & $policyScript -Path $temporaryDirectory
    }
    catch {
        $detected = $_.Exception.Message -match 'GitHub personal access token'
    }

    if (-not $detected) {
        throw 'The synthetic-secret policy did not reject its adversarial fixture.'
    }
}
finally {
    Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host 'Synthetic secret policy and adversarial fixture passed.'
