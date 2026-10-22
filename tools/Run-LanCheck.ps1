param(
    [ValidateSet('host', 'client', 'overflow', 'rejoin')][string]$Role = 'client',
    [string]$Address = '127.0.0.1',
    [ValidateRange(1, 65535)][int]$Port = 17777,
    [ValidateRange(15, 3600)][int]$Duration = 30,
    [string]$PlayerPath = '',
    [string]$ReportDirectory = '',
    [switch]$ExpectReceiveTimeout,
    [ValidateRange(0, 3600)][int]$MinimumConnectedSeconds = 0,
    [switch]$RequireFinalConnected
)
$ErrorActionPreference = 'Stop'
if (!$PlayerPath) {
    $PlayerPath = Join-Path $PSScriptRoot 'Sandbox/QuestLANSandbox.exe'
    if (!(Test-Path -LiteralPath $PlayerPath)) {
        $PlayerPath = Join-Path $PSScriptRoot '../Build/M3Sandbox/QuestLANSandbox.exe'
    }
}
$PlayerPath = (Resolve-Path -LiteralPath $PlayerPath).Path
$parsedAddress = $null
if (![Net.IPAddress]::TryParse($Address, [ref]$parsedAddress) -or
    $parsedAddress.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork) {
    throw 'Address must be the Host IPv4 address.'
}
if (!$ReportDirectory) {
    $ReportDirectory = Join-Path $PSScriptRoot 'Reports'
    if (Test-Path -LiteralPath (Join-Path $PSScriptRoot '../ProjectSettings')) {
        $ReportDirectory = Join-Path $PSScriptRoot '../Logs/PCVR-LAN'
    }
}
$null = New-Item -ItemType Directory -Path $ReportDirectory -Force
$ReportDirectory = (Resolve-Path -LiteralPath $ReportDirectory).Path
$runName = '{0}-{1}-{2}' -f $env:COMPUTERNAME, $Role, (Get-Date -Format 'yyyyMMdd-HHmmss-fff')
$reportPath = Join-Path $ReportDirectory "$runName.json"
$logPath = Join-Path $ReportDirectory "$runName.log"
$playerArgs = @('-batchmode', '-nographics', '--lan-role', $Role, '--lan-address', $Address,
    '--lan-port', "$Port", '--lan-duration', "$Duration", '--lan-report', $reportPath, '-logFile', $logPath)
# Start-Process joins ArgumentList; quote each value so paths containing spaces survive.
$argumentLine = ($playerArgs | ForEach-Object { '"{0}"' -f $_ }) -join ' '
Write-Host "Starting $Role -> ${Address}:$Port for $Duration seconds. Host listens on all interfaces."
$process = Start-Process -FilePath $PlayerPath -ArgumentList $argumentLine -PassThru -WindowStyle Hidden
if (!$process.WaitForExit(($Duration + 45) * 1000)) {
    # Stop only the process started by this invocation.
    Stop-Process -Id $process.Id
    throw "Player timed out; inspect $logPath"
}
if ($process.ExitCode -ne 0) { throw "Player exit $($process.ExitCode); inspect $logPath" }
if (!(Test-Path -LiteralPath $reportPath)) { throw "Missing report; inspect $logPath" }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
Write-Host "Report: $reportPath"
Write-Host "Log:    $logPath"
if ($report.schemaVersion -ne 2 -or $report.completedNormally -ne $true) {
    throw 'Incomplete or old report; rebuild the current Sandbox and preserve the log.'
}
if (![string]::IsNullOrEmpty($report.metricsError)) { throw "Metrics failed: $($report.metricsError)" }
if ($report.finalWorkingSetBytes -le 0 -or $report.finalPrivateBytes -le 0) { throw 'Process memory measurements are unavailable.' }
if ($report.timeoutVisibleViolations -gt 0 -or $report.timeoutInvalidPoseViolations -gt 0) {
    throw 'A remote pose or visual stayed valid during a receive timeout.'
}
if ($report.connectedSeconds -lt $MinimumConnectedSeconds) { throw 'Connected duration below the requested minimum.' }
if ($RequireFinalConnected -and $report.finalStatus -notin @('Host', 'Connected')) {
    throw 'Session did not remain connected until the end.'
}
if ($ExpectReceiveTimeout -and ($report.receiveTimeoutCount -lt 1 -or $report.timeoutRecoveries -lt 1 -or
    $report.timeoutVisualChecks -lt 1 -or $report.timeoutHiddenSamples -ne $report.timeoutVisualChecks)) {
    throw 'Expected timeout, hidden visuals and traffic recovery were not all observed.'
}
if ($Role -eq 'overflow') {
    if ($report.connected -or $report.finalStatus -notmatch 'Room full') {
        throw 'Overflow check failed: expected Room full with no local player.'
    }
} else {
    $required = @('connected', 'sawTwoPlayers', 'sawRemoteTracked', 'sawRemoteMovement',
        'remoteOwnershipRejected', 'rootReconstructionCorrect', 'sawRemoteColor')
    if ($Role -ne 'rejoin') {
        $required += @('sawRemoteSingleHandLost', 'sawRemoteAllLost', 'sawRemoteRecovery')
    } else { $required += 'lateJoinColor' }
    if ($Role -ne 'host') { $required += 'rttMeasured' }
    foreach ($field in $required) {
        if ($report.$field -ne $true) { throw "Check failed: $field. Inspect JSON and log." }
    }
}
Write-Host 'Requested process checks passed. This report does not prove XR or a physical cross-PC link.'
