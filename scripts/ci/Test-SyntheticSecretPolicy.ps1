[CmdletBinding()]
param(
    [Parameter()]
    [string]$Path = (Join-Path $PSScriptRoot '..\..')
)

$repositoryRoot = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
$allowedExtensions = @('.config', '.cs', '.csproj', '.env', '.json', '.md', '.props', '.ps1', '.sql', '.ts', '.tsx', '.xml', '.yaml', '.yml')
$ignoredSegments = @('.git', 'bin', 'dist', 'node_modules', 'obj')
$detectors = @(
    @{ Name = 'GitHub personal access token'; Pattern = '(?<![A-Za-z0-9_])gh[pousr]_[A-Za-z0-9]{36,}(?![A-Za-z0-9_])' },
    @{ Name = 'GitHub fine-grained token'; Pattern = '(?<![A-Za-z0-9_])github_pat_[A-Za-z0-9_]{20,}(?![A-Za-z0-9_])' },
    @{ Name = 'AWS access key'; Pattern = '(?<![A-Z0-9])AKIA[0-9A-Z]{16}(?![A-Z0-9])' },
    @{ Name = 'Azure Storage connection string'; Pattern = 'DefaultEndpointsProtocol=https;AccountName=[^;\s]+;AccountKey=[^;\s]+' }
)

$findings = [System.Collections.Generic.List[string]]::new()

foreach ($file in Get-ChildItem -LiteralPath $repositoryRoot -Recurse -File -Force) {
    if ($file.Extension -notin $allowedExtensions) {
        continue
    }

    $relativePath = [System.IO.Path]::GetRelativePath($repositoryRoot, $file.FullName)
    $segments = $relativePath -split '[\\/]'
    if ($segments | Where-Object { $_ -in $ignoredSegments }) {
        continue
    }

    foreach ($detector in $detectors) {
        foreach ($match in Select-String -LiteralPath $file.FullName -Pattern $detector.Pattern -AllMatches) {
            $findings.Add("$($relativePath):$($match.LineNumber) [$($detector.Name)]")
        }
    }
}

if ($findings.Count -gt 0) {
    $newline = [Environment]::NewLine
    throw ("Synthetic secret policy failed. Rotate and remove any real credential; the detector intentionally does not print the matched value.$newline - " + ($findings -join "$newline - "))
}

Write-Host 'Synthetic secret policy found no supported credential patterns.'
