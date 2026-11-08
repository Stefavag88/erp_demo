param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [ValidatePattern('^[A-Za-z0-9_]{1,128}$')][string]$DatabaseName = 'ERPDiscounts',
    [switch]$SeedOnly
)
# Windows integrated authentication; use sqlcmd directly for other authentication modes.
$ErrorActionPreference = 'Stop'
$files = if ($SeedOnly) { @(Get-Item (Join-Path $PSScriptRoot '004_seed_demo_data.sql')) } else { @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.sql' | Sort-Object Name) }
foreach ($file in $files) {
    & sqlcmd -S $Server -E -C -b -v "DatabaseName=$DatabaseName" -i $file.FullName
    if ($LASTEXITCODE -ne 0) { throw "Deployment stopped at $($file.Name) (exit $LASTEXITCODE)." }
}
