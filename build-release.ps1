param(
    [Parameter(Mandatory = $true)]
    [string]$TarkovDir,

    [string]$Version = "0.1.1"
)

$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$tarkovRoot = (Resolve-Path -LiteralPath $TarkovDir).Path.TrimEnd('\') + '\'
$serverProject = Join-Path $repoRoot 'Vault-server\Vault-server.csproj'
$clientProject = Join-Path $repoRoot 'Vault-client\Vault-client.csproj'
$serverOutput = Join-Path $repoRoot 'Vault-server\bin\Release\SharedStorageTrader'
$clientOutput = Join-Path $repoRoot 'Vault-client\bin\Release'
$stagingRoot = Join-Path $repoRoot 'release-staging'
$artifactRoot = Join-Path $repoRoot 'artifacts'
$archivePath = Join-Path $artifactRoot "Vault-v$Version.zip"
$checksumPath = "$archivePath.sha256"

if (-not (Test-Path -LiteralPath (Join-Path $tarkovRoot 'EscapeFromTarkov_Data\Managed\Assembly-CSharp.dll'))) {
    throw "TarkovDir is not an SPT game root: $tarkovRoot"
}

Remove-Item -LiteralPath $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $stagingRoot, $artifactRoot -Force | Out-Null

dotnet clean $serverProject -c Release
dotnet clean $clientProject -c Release -p:TarkovDir="$tarkovRoot"
dotnet build $serverProject -c Release
dotnet build $clientProject -c Release -p:TarkovDir="$tarkovRoot"

$serverStage = Join-Path $stagingRoot 'SPT\user\mods\Vault'
$clientStage = Join-Path $stagingRoot 'BepInEx\plugins\Vault'
New-Item -ItemType Directory -Path $serverStage, (Join-Path $serverStage 'assets'), $clientStage -Force | Out-Null

Copy-Item -LiteralPath (Join-Path $serverOutput 'Vault-server.dll') -Destination $serverStage
Copy-Item -LiteralPath (Join-Path $repoRoot 'Vault-server\assets\vault-door.png') -Destination (Join-Path $serverStage 'assets\vault-door.png')
Copy-Item -LiteralPath (Join-Path $repoRoot 'Vault-server\README.md') -Destination (Join-Path $serverStage 'README.md')
Copy-Item -LiteralPath (Join-Path $clientOutput 'Vault-client.dll') -Destination $clientStage
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $serverStage 'LICENSE')

Remove-Item -LiteralPath $archivePath, $checksumPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $stagingRoot '*') -DestinationPath $archivePath -CompressionLevel Optimal

$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $checksumPath -Value "$hash  $(Split-Path -Leaf $archivePath)" -Encoding ascii

Write-Output "Created $archivePath"
Write-Output "SHA256 $hash"
