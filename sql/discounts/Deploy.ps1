param(
    [string]$Server = '(localdb)\ERP2022',
    [string]$UserName,
    [ValidatePattern('^[A-Za-z0-9_]{1,128}$')][string]$DatabaseName = 'ERPDiscounts',
    [switch]$SeedOnly
)
# SQL authentication reads SQLCMDPASSWORD from the environment; never pass it in arguments.
$ErrorActionPreference = 'Stop'
$files = if ($SeedOnly) { @(Get-Item (Join-Path $PSScriptRoot '004_seed_demo_data.sql')) } else { @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.sql' | Sort-Object Name) }
foreach ($file in $files) {
    # Keep authentication arguments as an array: conditional single-item output
    # becomes a string, which native-command splatting handles differently.
    $auth = @('-E')
    if ($UserName) { $auth = @('-U', $UserName) }
    & sqlcmd -S $Server @auth -C -b -v "DatabaseName=$DatabaseName" -i $file.FullName
    if ($LASTEXITCODE -ne 0) { throw "Deployment stopped at $($file.Name) (exit $LASTEXITCODE)." }
}
