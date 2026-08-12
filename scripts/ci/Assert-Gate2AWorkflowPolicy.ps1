function Test-WorkflowDirectory {
    param(
        [Parameter(Mandatory)]
        [string]$WorkflowDirectory
    )

    $resolvedDirectory = (Resolve-Path -LiteralPath $WorkflowDirectory -ErrorAction Stop).Path
    $workflowFiles = Get-ChildItem -LiteralPath $resolvedDirectory -Recurse -File |
        Where-Object { $_.Extension -in '.yml', '.yaml' }

    if ($workflowFiles.Count -eq 0) {
        throw "No GitHub workflow files were found in '$resolvedDirectory'."
    }

    $permissionNames = 'actions|attestations|checks|contents|deployments|discussions|id-token|issues|models|packages|pages|pull-requests|security-events|statuses'
    $actionReferencePattern = '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@[a-f0-9]{40}$'
    $violations = [System.Collections.Generic.List[string]]::new()
    $repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

    foreach ($workflowFile in $workflowFiles) {
        $content = Get-Content -LiteralPath $workflowFile.FullName -Raw -ErrorAction Stop
        $relativePath = [System.IO.Path]::GetRelativePath($repositoryRoot, $workflowFile.FullName)

        if ($content -match '(?m)^\s*pull_request_target\s*:') {
            $violations.Add("$($relativePath): untrusted trigger 'pull_request_target' is forbidden.")
        }

        if ($content -match '(?m)^\s*workflow_run\s*:') {
            $violations.Add("$($relativePath): privileged follow-up trigger 'workflow_run' is forbidden in Gate 2A.")
        }

        if ($content -match '(?m)^\s*secrets\s*:\s*inherit\s*$') {
            $violations.Add("$($relativePath): 'secrets: inherit' is forbidden.")
        }

        if ($content -match '(?m)^\s*permissions\s*:\s*write-all\s*$') {
            $violations.Add("$($relativePath): 'permissions: write-all' violates least privilege.")
        }

        if ($content -match "(?m)^\s*(?:$permissionNames)\s*:\s*write\s*(?:#.*)?$") {
            $violations.Add("$($relativePath): a GitHub token permission requests write access.")
        }

        if ($content -notmatch '(?m)^permissions\s*:\s*\r?\n[ \t]+contents\s*:\s*read\s*(?:#.*)?$') {
            $violations.Add("$($relativePath): the top-level token policy must be exactly 'contents: read'.")
        }

        foreach ($match in [regex]::Matches($content, '(?m)^\s*(?:-\s*)?uses\s*:\s*([^\s#]+)')) {
            $reference = $match.Groups[1].Value.Trim('"', "'")
            if ($reference.StartsWith('./', [System.StringComparison]::Ordinal)) {
                continue
            }

            if ($reference -notmatch $actionReferencePattern) {
                $violations.Add("$($relativePath): action '$reference' is not pinned to a full immutable commit SHA.")
            }
        }
    }

    if ($violations.Count -gt 0) {
        $newline = [Environment]::NewLine
        throw ("GitHub workflow policy failed:$newline - " + ($violations -join "$newline - "))
    }
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Test-WorkflowDirectory -WorkflowDirectory (Join-Path $repositoryRoot '.github\workflows')

$temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("gate2a-workflow-policy-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryDirectory -ErrorAction Stop | Out-Null

try {
    $unsafeFixture = @(
        'name: Unsafe fixture',
        'on:',
        '  pull_request_target:',
        'permissions: write-all',
        'jobs:',
        '  unsafe:',
        '    runs-on: ubuntu-24.04',
        '    steps:',
        '      - uses: actions/checkout@v4'
    ) -join [Environment]::NewLine
    Set-Content -LiteralPath (Join-Path $temporaryDirectory 'unsafe.yml') -Value $unsafeFixture -NoNewline

    $detected = $false
    try {
        Test-WorkflowDirectory -WorkflowDirectory $temporaryDirectory
    }
    catch {
        $detected = $_.Exception.Message -match 'pull_request_target' -and
            $_.Exception.Message -match 'write-all' -and
            $_.Exception.Message -match 'not pinned'
    }

    if (-not $detected) {
        throw 'The workflow policy did not reject the unsafe adversarial fixture.'
    }
}
finally {
    Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host 'Workflow policy and adversarial fixture passed.'
