[CmdletBinding()]
param(
    [string]$Solution = "Reto.slnx"
)

$ErrorActionPreference = "Stop"

$reportText = & dotnet list $Solution package --vulnerable --include-transitive --format json
if ($LASTEXITCODE -ne 0) {
    throw "No se pudo auditar las dependencias NuGet (exit code $LASTEXITCODE)."
}

$report = $reportText | ConvertFrom-Json
$vulnerablePackages = @(
    foreach ($project in $report.projects) {
        foreach ($framework in $project.frameworks) {
            foreach ($package in @($framework.topLevelPackages) + @($framework.transitivePackages)) {
                if ($package.vulnerabilities.Count -gt 0) {
                    [PSCustomObject]@{
                        Project = $project.path
                        Framework = $framework.framework
                        Package = $package.id
                        Version = $package.resolvedVersion
                        Vulnerabilities = $package.vulnerabilities
                    }
                }
            }
        }
    }
)

if ($vulnerablePackages.Count -gt 0) {
    $vulnerablePackages | ConvertTo-Json -Depth 10
    throw "Se encontraron dependencias NuGet vulnerables. Revise el reporte anterior."
}

Write-Host "No se encontraron vulnerabilidades conocidas en las dependencias NuGet."