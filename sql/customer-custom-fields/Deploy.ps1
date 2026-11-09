param(
    [string]$Server = '127.0.0.1,1433',
    [string]$UserName,
    [ValidatePattern('^[A-Za-z0-9_]{1,128}$')][string]$DatabaseName = 'ERPCustomerCustomFields',
    [switch]$SeedOnly
)
# SQL authentication reads SQLCMDPASSWORD from the environment; never pass it in arguments.
$ErrorActionPreference = 'Stop'
$files = if ($SeedOnly) { @(Get-Item (Join-Path $PSScriptRoot '006_seed_demo_data.sql')) } else { @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.sql' | Sort-Object Name) }
foreach ($file in $files) {
    $auth = if ($UserName) { @('-U', $UserName) } else { @('-E') }
    & sqlcmd -S $Server @auth -C -b -v "DatabaseName=$DatabaseName" -i $file.FullName
    if ($LASTEXITCODE -ne 0) { throw "Deployment stopped at $($file.Name) (exit $LASTEXITCODE)." }
}
