[CmdletBinding()]
param(
  [ValidateSet('Debug', 'Release')]
  [string]$Configuration = 'Debug',
  [Parameter(ValueFromRemainingArguments = $true)]
  [string[]]$ApplicationArguments = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$windowsRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $windowsRoot 'src\ChatUnpack.FixtureHost.Windows\ChatUnpack.FixtureHost.Windows.csproj'

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
  Write-Error "找不到 FixtureHost 项目：$projectPath"
  exit 1
}

$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnetCommand) {
  Write-Error '.NET 8 SDK 未找到，请在 Windows 11 x64 环境安装后重试。'
  exit 1
}

$arguments = @(
  'run'
  '--project'
  $projectPath
  '--configuration'
  $Configuration
  '-p:Platform=x64'
  '--no-launch-profile'
)

if ($ApplicationArguments.Count -gt 0) {
  $arguments += '--'
  $arguments += $ApplicationArguments
}

& $dotnetCommand.Source @arguments
$exitCode = $LASTEXITCODE
if ($exitCode -ne 0) {
  exit $exitCode
}

exit 0
