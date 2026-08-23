[CmdletBinding()]
param(
  [ValidateSet('Debug', 'Release')]
  [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$windowsRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $windowsRoot 'ChatUnpack.Windows.sln'

if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
  Write-Error "找不到解决方案：$solutionPath"
  exit 1
}

$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnetCommand) {
  Write-Error '.NET 8 SDK 未找到，请在 Windows 11 x64 环境安装后重试。'
  exit 1
}

$arguments = @(
  'build'
  $solutionPath
  '--configuration'
  $Configuration
  '-p:Platform=x64'
)

& $dotnetCommand.Source @arguments
$exitCode = $LASTEXITCODE
if ($exitCode -ne 0) {
  exit $exitCode
}

exit 0
