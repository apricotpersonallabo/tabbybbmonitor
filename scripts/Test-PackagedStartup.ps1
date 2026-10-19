#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][string]$MakeAppxPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$scratch = [IO.Path]::GetFullPath((Join-Path $temporaryRoot ('BatteryTrayStartup-' + [guid]::NewGuid().ToString('N'))))
if (-not $scratch.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Unsafe startup test directory.'
}
$process = $null
try {
    & $MakeAppxPath unpack /p $PackagePath /d $scratch /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Unable to unpack the package for startup verification.' }
    $executable = Join-Path $scratch 'App\BatteryTray.exe'
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw 'Packaged executable not found.' }
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $executable
    $startInfo.WorkingDirectory = Split-Path -Parent $executable
    $startInfo.ArgumentList.Add('--startup-smoke-test')
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($startInfo)
    $outputTask = $process.StandardOutput.ReadToEndAsync()
    $errorTask = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(30000)) {
        $process.Kill($true)
        $process.WaitForExit()
        throw 'Packaged startup smoke test timed out after 30 seconds.'
    }
    $output = $outputTask.GetAwaiter().GetResult()
    $diagnostic = $errorTask.GetAwaiter().GetResult()
    if ($process.ExitCode -ne 0 -or $output.Trim() -ne 'Startup smoke test passed.') {
        throw "Packaged startup smoke test failed (exit $($process.ExitCode)). stdout: $output stderr: $diagnostic"
    }
    Write-Host 'Packaged x64 startup smoke test passed (all UI languages and notification menu actions).'
}
finally {
    if ($null -ne $process) {
        try {
            if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        }
        finally { $process.Dispose() }
    }
    # Delete only the unique directory created for this invocation, under the verified temp root.
    if ($scratch.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $scratch -PathType Container)) {
        Remove-Item -LiteralPath $scratch -Recurse -Force
    }
}
