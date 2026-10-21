$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$sandboxPath = Join-Path $projectRoot 'Build/M3Sandbox'
foreach ($file in @('QuestLANSandbox.exe', 'UnityPlayer.dll', 'QuestLANSandbox_Data')) {
    if (!(Test-Path -LiteralPath (Join-Path $sandboxPath $file))) {
        throw "Missing $file; run QuestLANIntegrationBuild.BuildWindowsBatch first."
    }
}
$packageName = 'PCVR-LAN-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')
$packagePath = Join-Path $projectRoot "Build/$packageName"
$null = New-Item -ItemType Directory -Path $packagePath
Copy-Item -LiteralPath $sandboxPath -Destination (Join-Path $packagePath 'Sandbox') -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Run-LanCheck.ps1') -Destination $packagePath
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/pcvr-lan-acceptance.md') -Destination (Join-Path $packagePath 'README.md')
$commit = & git -c "safe.directory=$($projectRoot.Replace('\', '/'))" -C $projectRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot read source commit.' }
$sourceStatus = & git -c "safe.directory=$($projectRoot.Replace('\', '/'))" -C $projectRoot status --porcelain
if ($LASTEXITCODE -ne 0) { throw 'Cannot read source status.' }
$manifest = [ordered]@{
    createdUtc = [DateTime]::UtcNow.ToString('O')
    unity = '6000.3.15f1'
    sourceCommit = $commit
    sourceDirty = [bool]$sourceStatus
    purpose = 'Windows Sandbox Mock LAN only; no XR hardware startup'
    files = @(Get-ChildItem -LiteralPath $packagePath -Recurse -File | ForEach-Object {
        [ordered]@{ path = $_.FullName.Substring($packagePath.Length + 1); bytes = $_.Length;
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $packagePath 'manifest.json') -Encoding UTF8
$zipPath = "$packagePath.zip"
Compress-Archive -LiteralPath $packagePath -DestinationPath $zipPath
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
"$hash  $packageName.zip" | Set-Content -LiteralPath "$zipPath.sha256.txt" -Encoding UTF8
Write-Host "Package: $zipPath"
Write-Host "SHA256:  $hash"
Write-Host "Source:  $commit; dirty=$([bool]$sourceStatus) (see manifest.json)"
