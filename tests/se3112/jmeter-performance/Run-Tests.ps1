# Run an EventPulse JMeter profile from PowerShell 7.
[CmdletBinding()]
param(
    [ValidateSet('smoke','baseline','normal','higher')] [string]$Profile = 'baseline',
    [ValidateSet('http','https')] [string]$TargetProto = 'http',
    [string]$TargetHost = 'localhost',
    [ValidateRange(1,65535)] [int]$TargetPort = 7000,
    [string]$ApiPath = '/api/events',
    [ValidateRange(1,2147483647)] [int]$Users,
    [ValidateRange(0,2147483647)] [int]$RampUp,
    [ValidateRange(1,2147483647)] [int]$Duration,
    [ValidateRange(1,2147483647)] [int]$ThinkTimeMs = 500,
    [switch]$AllowNonLocal,
    [string]$JMeterHome = $env:JMETER_HOME,
    [string]$OutputRoot = (Join-Path $PSScriptRoot 'results')
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$profileSettings = @{
    smoke    = @{ Users = 1;  RampUp = 1;  Duration = 10 }
    baseline = @{ Users = 5;  RampUp = 10; Duration = 30 }
    normal   = @{ Users = 20; RampUp = 20; Duration = 30 }
    higher   = @{ Users = 50; RampUp = 30; Duration = 30 }
}[$Profile]
if (!$PSBoundParameters.ContainsKey('Users')) { $Users = $profileSettings.Users }
if (!$PSBoundParameters.ContainsKey('RampUp')) { $RampUp = $profileSettings.RampUp }
if (!$PSBoundParameters.ContainsKey('Duration')) { $Duration = $profileSettings.Duration }

$jmeter = if ($JMeterHome) {
    Join-Path $JMeterHome 'bin/jmeter.bat'
} else {
    (Get-Command jmeter.bat -CommandType Application -ErrorAction SilentlyContinue)?.Source
}
if (!$jmeter -or !(Test-Path -LiteralPath $jmeter -PathType Leaf)) {
    throw 'JMeter not found. Set -JMeterHome, JMETER_HOME or add JMeter\bin to PATH.'
}
if ($env:JAVA_HOME) {
    $javaBin = Join-Path $env:JAVA_HOME 'bin'
    if (!(Test-Path -LiteralPath (Join-Path $javaBin 'java.exe') -PathType Leaf)) {
        throw 'JAVA_HOME must point to a JDK with bin\java.exe.'
    }
    # JMeter 5.6.3's Windows launcher resolves java.exe via PATH.
    $env:Path = $javaBin + [IO.Path]::PathSeparator + $env:Path
}

$python = if ($env:PYTHON_BIN) { $env:PYTHON_BIN } else { 'python' }
$checks = Join-Path $PSScriptRoot 'check_run.py'
$plan = Join-Path $PSScriptRoot 'eventpulse-events-load-test.jmx'
$readyArgs = @('ready', $TargetProto, $TargetHost, "$TargetPort", $ApiPath)
if ($AllowNonLocal) { $readyArgs += '--allow-nonlocal' }
& $python $checks @readyArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$runName = '{0}-{1}-{2}' -f $Profile, (Get-Date -Format 'yyyyMMdd_HHmmssfff'), ([Guid]::NewGuid().ToString('N'))
$output = (New-Item -ItemType Directory -Path (Join-Path $OutputRoot $runName)).FullName
$jtl = Join-Path $output 'results.jtl'
$report = Join-Path $output 'html'
$log = Join-Path $output 'jmeter.log'
Write-Host "$Profile on $TargetHost port $TargetPort with $Users users, ${RampUp}s ramp, ${Duration}s scheduled, ${ThinkTimeMs}ms pacing."
Write-Host "Artifacts: $output"

$jmeterArgs = @(
    '-n', '-t', $plan,
    "-Jprotocol=$TargetProto", "-Jhost=$TargetHost", "-Jport=$TargetPort", "-Japi_path=$ApiPath",
    "-Jusers=$Users", "-Jrampup=$RampUp", "-Jduration=$Duration", "-Jthink_time_ms=$ThinkTimeMs",
    '-Jjmeter.save.saveservice.output_format=csv', '-Jjmeter.save.saveservice.print_field_names=true',
    '-Jjmeter.save.saveservice.default_delimiter=,', '-Jjmeter.save.saveservice.timestamp_format=ms',
    '-l', $jtl, '-e', '-o', $report, '-j', $log
)
& $jmeter @jmeterArgs
if ($LASTEXITCODE -ne 0) {
    $jmeterExit = $LASTEXITCODE
    Write-Error "JMeter exited with code $jmeterExit. Available artifacts: $output" -ErrorAction Continue
    exit $jmeterExit
}
& $python $checks results $jtl (Join-Path $report 'index.html')
exit $LASTEXITCODE
